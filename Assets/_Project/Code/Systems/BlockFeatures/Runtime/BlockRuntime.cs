using System.Collections.Generic;
using UnityEngine;

namespace Project.BlockFeatures
{
    [DisallowMultipleComponent]
    public sealed class BlockRuntime : MonoBehaviour
    {
        [SerializeField, Range(1, 16)]
        private int maxDispatchPasses = 4;
        [SerializeField, Range(0, 512)]
        private int maxDebugEvents = 128;

        private readonly List<BlockFeature> features =
            new List<BlockFeature>();
        private readonly Dictionary<System.Type, object> state =
            new Dictionary<System.Type, object>();
        private readonly List<ISignalEnvelope> signals =
            new List<ISignalEnvelope>();
        private readonly List<ICommandEnvelope> commands =
            new List<ICommandEnvelope>();
        private readonly List<ISignalEnvelope> signalBatch =
            new List<ISignalEnvelope>();
        private readonly List<ICommandEnvelope> commandBatch =
            new List<ICommandEnvelope>();
        private readonly List<BlockDebugEvent> debugEvents =
            new List<BlockDebugEvent>();
        private bool initialized;

        public IBlockStateStore State { get; private set; }
        public IBlockQueryService Query { get; private set; }
        public IBlockSignalBus Signals { get; private set; }
        public IBlockCommandBus Commands { get; private set; }
        public IBlockLinkService Links { get; private set; }
        public IBlockPresentationService Presentation { get; private set; }
        public IBlockDebugService Debug { get; private set; }
        public IReadOnlyList<BlockFeature> Features => features;
        public IReadOnlyList<BlockDebugEvent> DebugEvents => debugEvents;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (!feature.isActiveAndEnabled)
                {
                    continue;
                }

                feature.Attach(new BlockContext(this, feature));
            }
        }

        private void OnDisable()
        {
            for (int index = 0; index < features.Count; index++)
            {
                if (features[index] != null &&
                    features[index].IsAttached)
                {
                    features[index].Detach();
                }
            }

            signals.Clear();
            commands.Clear();
        }

        private void Update()
        {
            if (!initialized || features.Count == 0)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (feature.isActiveAndEnabled)
                {
                    feature.Tick(deltaTime);
                }
            }

            DispatchQueues();
        }

        public bool TryGetCapability<T>(out T value) where T : class
        {
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (feature.isActiveAndEnabled &&
                    feature is T candidate)
                {
                    value = candidate;
                    return true;
                }
            }

            value = null;
            return false;
        }

        public void FlushPending()
        {
            EnsureInitialized();
            DispatchQueues();
        }

        public void RefreshFeatureSet(
            bool resetRuntimeState = false)
        {
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (feature != null && feature.IsAttached)
                {
                    feature.Detach();
                }
            }

            features.Clear();
            if (resetRuntimeState)
            {
                state.Clear();
                signals.Clear();
                commands.Clear();
                debugEvents.Clear();
            }

            initialized = false;
            EnsureInitialized();
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (feature.isActiveAndEnabled)
                {
                    feature.Attach(new BlockContext(this, feature));
                }
            }
        }

        public void ReceiveExternalSignal<T>(T signal)
            where T : class, IBlockSignal
        {
            EnsureInitialized();
            EnqueueSignal(signal);
            RecordDebug(
                BlockDebugEventKind.Signal,
                $"External {typeof(T).Name}");
        }

        internal void EnqueueSignal<T>(T signal)
            where T : class, IBlockSignal
        {
            signals.Add(new SignalEnvelope<T>(signal));
            RecordDebug(
                BlockDebugEventKind.Signal,
                $"Signal {typeof(T).Name}");
        }

        internal void EnqueueCommand<T>(T command)
            where T : class, IBlockCommand
        {
            commands.Add(new CommandEnvelope<T>(command));
            RecordDebug(
                BlockDebugEventKind.Command,
                $"Command {typeof(T).Name}");
        }

        internal void RecordDebug(
            BlockDebugEventKind kind,
            string message)
        {
            if (maxDebugEvents <= 0)
            {
                return;
            }

            if (debugEvents.Count >= maxDebugEvents)
            {
                debugEvents.RemoveAt(0);
            }

            debugEvents.Add(new BlockDebugEvent(
                kind,
                message,
                Time.realtimeSinceStartup));
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            features.Clear();
            GetComponents(features);
            features.RemoveAll(
                feature => feature == null ||
                           !feature.isActiveAndEnabled);
            features.Sort(CompareFeatures);
            State = new RuntimeStateStore(state);
            Query = new RuntimeQueryService(this);
            Signals = new RuntimeSignalBus(this);
            Commands = new RuntimeCommandBus(this);
            Links = new NullLinkService(this);
            Presentation = new NullPresentationService(this);
            Debug = new RuntimeDebugService(this);
            initialized = true;
        }

        private static int CompareFeatures(
            BlockFeature left,
            BlockFeature right)
        {
            int phase = left.Metadata.Phase.CompareTo(
                right.Metadata.Phase);
            if (phase != 0)
            {
                return phase;
            }

            int order = left.Metadata.Order.CompareTo(
                right.Metadata.Order);
            if (order != 0)
            {
                return order;
            }

            return left.GetInstanceID().CompareTo(
                right.GetInstanceID());
        }

        private void DispatchQueues()
        {
            int pass = 0;
            while ((signals.Count > 0 || commands.Count > 0) &&
                   pass < maxDispatchPasses)
            {
                signalBatch.Clear();
                signalBatch.AddRange(signals);
                signals.Clear();
                commandBatch.Clear();
                commandBatch.AddRange(commands);
                commands.Clear();

                for (int index = 0; index < signalBatch.Count; index++)
                {
                    signalBatch[index].Dispatch(this);
                }

                for (int index = 0; index < commandBatch.Count; index++)
                {
                    commandBatch[index].Dispatch(this);
                }

                pass++;
            }

            if (signals.Count > 0 || commands.Count > 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"BlockRuntime 达到最大派发次数，剩余 " +
                    $"Signals={signals.Count}, Commands={commands.Count}",
                    this);
                signals.Clear();
                commands.Clear();
            }
        }

        private void DispatchSignal<T>(T signal)
            where T : class, IBlockSignal
        {
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (feature.isActiveAndEnabled)
                {
                    feature.DispatchSignal(signal);
                }
            }
        }

        private void DispatchCommand<T>(T command)
            where T : class, IBlockCommand
        {
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (feature.isActiveAndEnabled)
                {
                    feature.DispatchCommand(command);
                }
            }
        }

        private interface ISignalEnvelope
        {
            void Dispatch(BlockRuntime runtime);
        }

        private interface ICommandEnvelope
        {
            void Dispatch(BlockRuntime runtime);
        }

        private sealed class SignalEnvelope<T> : ISignalEnvelope
            where T : class, IBlockSignal
        {
            private readonly T signal;

            internal SignalEnvelope(T value)
            {
                signal = value;
            }

            public void Dispatch(BlockRuntime runtime)
            {
                runtime.DispatchSignal(signal);
            }
        }

        private sealed class CommandEnvelope<T> : ICommandEnvelope
            where T : class, IBlockCommand
        {
            private readonly T command;

            internal CommandEnvelope(T value)
            {
                command = value;
            }

            public void Dispatch(BlockRuntime runtime)
            {
                runtime.DispatchCommand(command);
            }
        }

        private sealed class RuntimeStateStore : IBlockStateStore
        {
            private readonly Dictionary<System.Type, object> values;

            internal RuntimeStateStore(
                Dictionary<System.Type, object> storage)
            {
                values = storage;
            }

            public bool TryGet<T>(out T value) where T : class
            {
                if (values.TryGetValue(typeof(T), out object stored))
                {
                    value = stored as T;
                    return value != null;
                }

                value = null;
                return false;
            }

            public void Set<T>(T value) where T : class
            {
                values[typeof(T)] = value;
            }

            public bool Remove<T>() where T : class
            {
                return values.Remove(typeof(T));
            }
        }

        private sealed class RuntimeQueryService : IBlockQueryService
        {
            private readonly BlockRuntime runtime;

            internal RuntimeQueryService(BlockRuntime owner)
            {
                runtime = owner;
            }

            public bool TryGet<T>(out T value) where T : class
            {
                return runtime.TryGetCapability(out value);
            }
        }

        private sealed class RuntimeSignalBus : IBlockSignalBus
        {
            private readonly BlockRuntime runtime;

            internal RuntimeSignalBus(BlockRuntime owner)
            {
                runtime = owner;
            }

            public void Emit<T>(T signal)
                where T : class, IBlockSignal
            {
                runtime.EnqueueSignal(signal);
            }
        }

        private sealed class RuntimeCommandBus : IBlockCommandBus
        {
            private readonly BlockRuntime runtime;

            internal RuntimeCommandBus(BlockRuntime owner)
            {
                runtime = owner;
            }

            public void Send<T>(T command)
                where T : class, IBlockCommand
            {
                runtime.EnqueueCommand(command);
            }
        }

        private sealed class NullLinkService : IBlockLinkService
        {
            private readonly BlockRuntime runtime;

            internal NullLinkService(BlockRuntime owner)
            {
                runtime = owner;
            }

            public void Add(IBlockLink link)
            {
                runtime.RecordDebug(
                    BlockDebugEventKind.Link,
                    link != null
                        ? $"Link {link.Kind}"
                        : "Link null");
            }

            public bool TryGetAll<T>(List<T> results)
                where T : class, IBlockLink
            {
                results?.Clear();
                return false;
            }

            public bool Remove(string kind)
            {
                runtime.RecordDebug(
                    BlockDebugEventKind.Link,
                    $"Remove {kind}");
                return false;
            }

            public void Clear()
            {
                runtime.RecordDebug(
                    BlockDebugEventKind.Link,
                    "Clear links");
            }
        }

        private sealed class NullPresentationService :
            IBlockPresentationService
        {
            private readonly BlockRuntime runtime;

            internal NullPresentationService(BlockRuntime owner)
            {
                runtime = owner;
            }

            public void Request(IBlockPresentationRequest request)
            {
                runtime.RecordDebug(
                    BlockDebugEventKind.Presentation,
                    request != null
                        ? request.GetType().Name
                        : "Presentation null");
            }
        }

        private sealed class RuntimeDebugService : IBlockDebugService
        {
            private readonly BlockRuntime runtime;

            internal RuntimeDebugService(BlockRuntime owner)
            {
                runtime = owner;
            }

            public void Record(
                BlockDebugEventKind kind,
                string message)
            {
                runtime.RecordDebug(kind, message);
            }
        }
    }
}

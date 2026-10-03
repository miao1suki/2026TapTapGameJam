using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Project.BlockFeatures
{
    public enum BlockFeaturePhase
    {
        [InspectorName("输入")] Input = 0,
        [InspectorName("意图")] Intent = 10,
        [InspectorName("模拟")] Simulation = 20,
        [InspectorName("反应")] Reaction = 30,
        [InspectorName("联动")] Link = 40,
        [InspectorName("移动")] Movement = 50,
        [InspectorName("表现")] Presentation = 60,
        [InspectorName("清理")] Cleanup = 70
    }

    public enum BlockChannel
    {
        [InspectorName("自定义")] Custom = 0,
        [InspectorName("状态")] State = 1,
        [InspectorName("热量")] Heat = 2,
        [InspectorName("动力")] Power = 3,
        [InspectorName("运动")] Motion = 4,
        [InspectorName("颜色")] Color = 5,
        [InspectorName("光照")] Light = 6,
        [InspectorName("水")] Water = 7,
        [InspectorName("生长")] Growth = 8,
        [InspectorName("空气")] Air = 9,
        [InspectorName("阴影")] Shadow = 10,
        [InspectorName("信号")] Signal = 11,
        [InspectorName("表现")] Presentation = 12
    }

    [AttributeUsage(
        AttributeTargets.Class,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class BlockFeatureAttribute : Attribute
    {
        public string DisplayName { get; set; }
        public BlockFeaturePhase Phase { get; set; } =
            BlockFeaturePhase.Simulation;
        public int Order { get; set; }
        public int MaxPerBlock { get; set; } = 1;
        public Type[] Provides { get; set; } = Array.Empty<Type>();
        public Type[] Requires { get; set; } = Array.Empty<Type>();
        public Type[] Conflicts { get; set; } = Array.Empty<Type>();
        public Type[] Emits { get; set; } = Array.Empty<Type>();
        public Type[] Handles { get; set; } = Array.Empty<Type>();
        public Type[] SendsCommands { get; set; } = Array.Empty<Type>();
        public Type[] OwnsPorts { get; set; } = Array.Empty<Type>();
        public BlockChannel[] Writes { get; set; } =
            Array.Empty<BlockChannel>();
    }

    [AttributeUsage(
        AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class BlockParameterAttribute : Attribute
    {
        public string Label { get; set; }
        public string Group { get; set; }
        public int Order { get; set; }
        public string Tooltip { get; set; }
        public string VisibleWhenField { get; set; }
        public int VisibleWhenValue { get; set; }
    }

    public sealed class BlockFeatureMetadata
    {
        internal BlockFeatureMetadata(
            Type featureType,
            BlockFeatureAttribute attribute)
        {
            FeatureType = featureType;
            DisplayName = string.IsNullOrWhiteSpace(
                attribute.DisplayName)
                ? string.Empty
                : attribute.DisplayName.Trim();
            Phase = attribute.Phase;
            Order = attribute.Order;
            MaxPerBlock = Mathf.Max(1, attribute.MaxPerBlock);
            Provides = attribute.Provides ?? Array.Empty<Type>();
            Requires = attribute.Requires ?? Array.Empty<Type>();
            Conflicts = attribute.Conflicts ?? Array.Empty<Type>();
            Emits = attribute.Emits ?? Array.Empty<Type>();
            Handles = attribute.Handles ?? Array.Empty<Type>();
            SendsCommands =
                attribute.SendsCommands ?? Array.Empty<Type>();
            OwnsPorts = attribute.OwnsPorts ?? Array.Empty<Type>();
            Writes = attribute.Writes ?? Array.Empty<BlockChannel>();
        }

        public Type FeatureType { get; }
        public string DisplayName { get; }
        public BlockFeaturePhase Phase { get; }
        public int Order { get; }
        public int MaxPerBlock { get; }
        public IReadOnlyList<Type> Provides { get; }
        public IReadOnlyList<Type> Requires { get; }
        public IReadOnlyList<Type> Conflicts { get; }
        public IReadOnlyList<Type> Emits { get; }
        public IReadOnlyList<Type> Handles { get; }
        public IReadOnlyList<Type> SendsCommands { get; }
        public IReadOnlyList<Type> OwnsPorts { get; }
        public IReadOnlyList<BlockChannel> Writes { get; }
    }

    public static class BlockFeatureMetadataCache
    {
        private static readonly Dictionary<Type, BlockFeatureMetadata>
            Cache = new Dictionary<Type, BlockFeatureMetadata>();

        public static BlockFeatureMetadata Get(Type featureType)
        {
            if (featureType == null)
            {
                throw new ArgumentNullException(nameof(featureType));
            }

            if (Cache.TryGetValue(featureType, out var metadata))
            {
                return metadata;
            }

            BlockFeatureAttribute attribute =
                featureType.GetCustomAttribute<BlockFeatureAttribute>(
                    true) ?? new BlockFeatureAttribute();
            metadata = new BlockFeatureMetadata(
                featureType,
                attribute);
            Cache[featureType] = metadata;
            return metadata;
        }
    }

    public interface IBlockSignal
    {
    }

    public interface IBlockCommand
    {
    }

    public interface IBlockLink
    {
        string Kind { get; }
    }

    public interface IBlockPresentationRequest
    {
    }

    public enum BlockDebugEventKind
    {
        [InspectorName("信号")] Signal = 0,
        [InspectorName("命令")] Command = 1,
        [InspectorName("联动")] Link = 2,
        [InspectorName("表现")] Presentation = 3,
        [InspectorName("错误")] Error = 4
    }

    public sealed class BlockDebugValue
    {
        public BlockDebugValue(string label, object value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public object Value { get; }
    }

    public sealed class BlockDebugAction
    {
        public BlockDebugAction(
            string label,
            Action action,
            bool enabled = true)
        {
            Label = label;
            Action = action;
            Enabled = enabled;
        }

        public string Label { get; }
        public Action Action { get; }
        public bool Enabled { get; }
    }

    public sealed class BlockDebugEvent
    {
        public BlockDebugEvent(
            BlockDebugEventKind kind,
            string message,
            float time)
        {
            Kind = kind;
            Message = message;
            Time = time;
        }

        public BlockDebugEventKind Kind { get; }
        public string Message { get; }
        public float Time { get; }
    }

    public interface IBlockDebugService
    {
        void Record(BlockDebugEventKind kind, string message);
    }

    public interface IBlockSignalHandler<in T>
        where T : class, IBlockSignal
    {
        void HandleSignal(T signal);
    }

    public interface IBlockCommandHandler<in T>
        where T : class, IBlockCommand
    {
        void HandleCommand(T command);
    }

    public interface IBlockStateStore
    {
        bool TryGet<T>(out T value) where T : class;
        void Set<T>(T value) where T : class;
        bool Remove<T>() where T : class;
    }

    public interface IBlockQueryService
    {
        bool TryGet<T>(out T value) where T : class;
    }

    public interface IBlockSignalBus
    {
        void Emit<T>(T signal) where T : class, IBlockSignal;
    }

    public interface IBlockCommandBus
    {
        void Send<T>(T command) where T : class, IBlockCommand;
    }

    public interface IBlockLinkService
    {
        void Add(IBlockLink link);
        bool TryGetAll<T>(List<T> results) where T : class, IBlockLink;
        bool Remove(string kind);
        void Clear();
    }

    public interface IBlockPresentationService
    {
        void Request(IBlockPresentationRequest request);
    }

    public sealed class BlockContext
    {
        internal BlockContext(
            BlockRuntime runtime,
            BlockFeature owner)
        {
            Runtime = runtime;
            Owner = owner;
        }

        public BlockRuntime Runtime { get; }
        public BlockFeature Owner { get; }
        public GameObject Root => Runtime.gameObject;
        public Transform Transform => Runtime.transform;
        public IBlockStateStore State => Runtime.State;
        public IBlockQueryService Query => Runtime.Query;
        public IBlockSignalBus Signals => Runtime.Signals;
        public IBlockCommandBus Commands => Runtime.Commands;
        public IBlockLinkService Links => Runtime.Links;
        public IBlockPresentationService Presentation =>
            Runtime.Presentation;
        public IBlockDebugService Debug => Runtime.Debug;
    }

    [RequireComponent(typeof(BlockRuntime))]
    public abstract class BlockFeature : MonoBehaviour
    {
        private BlockContext context;
        private BlockFeatureMetadata metadata;

        public BlockContext Context => context;
        public BlockFeatureMetadata Metadata =>
            metadata ?? (metadata = BlockFeatureMetadataCache.Get(
                GetType()));
        public bool IsAttached => context != null;

        public virtual void CollectDebugValues(
            List<BlockDebugValue> values)
        {
        }

        public virtual void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
        }

        internal void Attach(BlockContext value)
        {
            if (context != null)
            {
                return;
            }

            context = value;
            OnAttach();
        }

        internal void Tick(float deltaTime)
        {
            if (context != null)
            {
                OnTick(deltaTime);
            }
        }

        internal void Detach()
        {
            if (context == null)
            {
                return;
            }

            OnDetach();
            context = null;
        }

        internal void DispatchSignal<T>(T signal)
            where T : class, IBlockSignal
        {
            if (context != null &&
                this is IBlockSignalHandler<T> handler)
            {
                handler.HandleSignal(signal);
            }
        }

        internal void DispatchCommand<T>(T command)
            where T : class, IBlockCommand
        {
            if (context != null &&
                this is IBlockCommandHandler<T> handler)
            {
                handler.HandleCommand(command);
            }
        }

        protected virtual void OnAttach()
        {
        }

        protected virtual void OnTick(float deltaTime)
        {
        }

        protected virtual void OnDetach()
        {
        }
    }
}

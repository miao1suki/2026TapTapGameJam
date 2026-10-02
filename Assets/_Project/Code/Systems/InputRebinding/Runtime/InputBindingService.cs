using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.InputRebinding
{
    public sealed class InputBindingService : IDisposable
    {
        private sealed class HistorySnapshot
        {
            public string AssetJson;
            public string PolicySnapshot;
        }

        private const string GameplayMapName = "Gameplay";
        private const int MaxHistory = 50;
        private readonly InputActionAsset asset;
        private readonly IInputBindingStorage storage;
        private readonly bool ownsAsset;
        private readonly string defaultJson;
        private readonly List<HistorySnapshot> undoHistory =
            new List<HistorySnapshot>();
        private readonly List<HistorySnapshot> redoHistory =
            new List<HistorySnapshot>();

        public event Action Changed;

        public InputBindingService(
            InputActionAsset actionAsset,
            IInputBindingStorage bindingStorage,
            string resetJson = null,
            bool ownsAsset = false)
        {
            asset = actionAsset
                ?? throw new ArgumentNullException(
                    nameof(actionAsset));
            storage = bindingStorage
                ?? throw new ArgumentNullException(
                    nameof(bindingStorage));
            this.ownsAsset = ownsAsset;
            defaultJson = string.IsNullOrWhiteSpace(resetJson)
                ? asset.ToJson()
                : resetJson;
            Load();
        }

        public static InputBindingService CreateFromInputService()
        {
            InputService service =
                InputService.EnsureInstance();
            InputActionAsset resetAsset =
                service.ConfiguredActionAsset != null
                    ? UnityEngine.Object.Instantiate(
                        service.ConfiguredActionAsset)
                    : InputActionAssetFactory
                        .CreateDefaultGameplayAsset();
            string resetJson = resetAsset.ToJson();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(resetAsset);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(resetAsset);
            }

            return new InputBindingService(
                service.RuntimeActionAsset,
                new PlayerPrefsInputBindingStorage(),
                resetJson,
                false);
        }

        public static InputBindingService CreateForEditor()
        {
            InputActionAsset actionAsset = null;
            InputService sceneService =
                UnityEngine.Object.FindFirstObjectByType<InputService>(
                    FindObjectsInactive.Include);
            if (sceneService != null &&
                sceneService.ConfiguredActionAsset != null)
            {
                actionAsset = UnityEngine.Object.Instantiate(
                    sceneService.ConfiguredActionAsset);
            }

            if (actionAsset == null)
            {
                actionAsset =
                    InputActionAssetFactory
                        .CreateDefaultGameplayAsset();
            }

            return new InputBindingService(
                actionAsset,
                new PlayerPrefsInputBindingStorage(),
                null,
                true);
        }

        public InputActionAsset Asset => asset;
        public bool CanUndo => undoHistory.Count > 0;
        public bool CanRedo => redoHistory.Count > 0;

        public InputActionTrigger GetActionTrigger(
            InputActionId actionId)
        {
            InputActionTriggerPolicy policy =
                InputActionInteractionPolicy.GetPolicy(actionId);
            if (policy == InputActionTriggerPolicy.ClickOnly)
            {
                return InputActionTrigger.Press;
            }

            if (policy == InputActionTriggerPolicy.HoldOnly)
            {
                return InputActionTrigger.Hold;
            }

            if (!TryGetAction(actionId, out InputAction action))
            {
                return InputActionTrigger.Press;
            }

            for (int index = 0;
                 index < action.bindings.Count;
                 index++)
            {
                InputBinding binding = action.bindings[index];
                if (binding.isPartOfComposite)
                {
                    continue;
                }

                InputBindingTrigger trigger =
                    ResolveTrigger(binding.effectiveInteractions);
                if (trigger == InputBindingTrigger.Hold)
                {
                    return InputActionTrigger.Hold;
                }
            }

            return InputActionTrigger.Press;
        }

        public IReadOnlyList<InputBindingInfo> GetBindings(
            InputActionId actionId)
        {
            List<InputBindingInfo> result =
                new List<InputBindingInfo>();
            if (!TryGetAction(actionId, out InputAction action))
            {
                return result;
            }

            for (int index = 0;
                 index < action.bindings.Count;
                 index++)
            {
                InputBinding binding = action.bindings[index];
                action.GetBindingDisplayString(
                    index,
                    out _,
                    out string controlPath,
                    InputBinding.DisplayStringOptions
                        .DontUseShortDisplayNames);
                result.Add(new InputBindingInfo(
                    action,
                    index,
                    GetBindingDisplayName(action, index),
                    controlPath,
                    ResolveDevice(binding.effectivePath),
                    ResolveTrigger(binding.effectiveInteractions),
                    action.type == InputActionType.Button,
                    binding.isComposite,
                    binding.isPartOfComposite));
            }

            return result;
        }

        public InputActionRebindingExtensions.RebindingOperation
            StartRebind(
                InputActionId actionId,
                int bindingIndex,
                Action completed = null,
                Action canceled = null)
        {
            if (!TryGetAction(actionId, out InputAction action) ||
                bindingIndex < 0 ||
                bindingIndex >= action.bindings.Count)
            {
                return null;
            }

            HistorySnapshot before = CaptureSnapshot();
            bool wasEnabled = action.enabled;
            if (wasEnabled)
            {
                action.Disable();
            }

            try
            {
                InputActionRebindingExtensions.RebindingOperation
                    operation = action
                        .PerformInteractiveRebinding(bindingIndex)
                        .WithCancelingThrough("<Keyboard>/escape")
                        .OnMatchWaitForAnother(0.08f);
                if (action.name != InputActionId.Look.ToString())
                {
                    operation
                        .WithControlsExcluding("<Mouse>/position")
                        .WithControlsExcluding("<Mouse>/delta");
                }

                operation
                    .OnComplete(result =>
                    {
                        result.Dispose();
                        if (wasEnabled)
                        {
                            action.Enable();
                        }

                        BakeBindingPath(action, bindingIndex);
                        RecordHistory(before);
                        Save();
                        Changed?.Invoke();
                        completed?.Invoke();
                    })
                    .OnCancel(result =>
                    {
                        result.Dispose();
                        if (wasEnabled)
                        {
                            action.Enable();
                        }

                        canceled?.Invoke();
                    });
                operation.Start();
                return operation;
            }
            catch
            {
                if (wasEnabled)
                {
                    action.Enable();
                }

                throw;
            }
        }

        public void SetTrigger(
            InputActionId actionId,
            int bindingIndex,
            InputBindingTrigger trigger)
        {
            if (!InputActionInteractionPolicy.CanConfigureTrigger(
                    actionId) ||
                !TryGetAction(actionId, out InputAction action) ||
                bindingIndex < 0 ||
                bindingIndex >= action.bindings.Count)
            {
                return;
            }

            if (trigger == InputBindingTrigger.Tap)
            {
                trigger = InputBindingTrigger.Press;
            }

            HistorySnapshot before = CaptureSnapshot();
            InputActionMap map = action.actionMap;
            bool mapWasEnabled = map != null && map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            for (int index = 0;
                 index < action.bindings.Count;
                 index++)
            {
                if (action.bindings[index].isPartOfComposite)
                {
                    continue;
                }

                action.RemoveBindingOverride(index);
                InputBinding binding =
                    action.bindings[index];
                binding.interactions =
                    GetInteraction(trigger);
                binding.overrideInteractions = null;
                action.ChangeBinding(index).To(binding);
            }
            if (mapWasEnabled)
            {
                map.Enable();
            }

            ApplyChange(before);
        }

        public void SetTriggerPolicy(
            InputActionId actionId,
            InputActionTriggerPolicy policy)
        {
            if (!TryGetAction(actionId, out _))
            {
                return;
            }

            HistorySnapshot before = CaptureSnapshot();
            InputActionInteractionPolicy.SetPolicy(
                actionId,
                policy);
            InputActionInteractionPolicy.Normalize(asset);
            string afterJson = asset.ToJson();
            if (before.AssetJson != afterJson ||
                before.PolicySnapshot !=
                InputActionInteractionPolicy.CapturePolicies())
            {
                PushUndo(before);
                Save();
            }

            Changed?.Invoke();
        }

        public void ResetBinding(
            InputActionId actionId,
            int bindingIndex)
        {
            if (!TryGetAction(actionId, out InputAction action) ||
                bindingIndex < 0 ||
                bindingIndex >= action.bindings.Count)
            {
                return;
            }

            HistorySnapshot before = CaptureSnapshot();
            InputActionMap map = action.actionMap;
            bool mapWasEnabled = map != null && map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            action.RemoveBindingOverride(bindingIndex);
            if (!RestoreDefaultBinding(action, bindingIndex))
            {
                action.ChangeBinding(bindingIndex).Erase();
            }

            if (mapWasEnabled)
            {
                map.Enable();
            }

            ApplyChange(before);
        }

        public int AddBinding(
            InputActionId actionId,
            InputBindingDevice device,
            InputBindingTrigger trigger)
        {
            return AddBinding(
                actionId,
                GetDefaultPath(device),
                device,
                trigger);
        }

        public int AddBinding(
            InputActionId actionId,
            string path,
            InputBindingDevice device,
            InputBindingTrigger trigger)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !TryGetAction(actionId, out InputAction action))
            {
                return -1;
            }

            if (!InputActionInteractionPolicy.CanConfigureTrigger(
                    actionId))
            {
                trigger = InputBindingTrigger.Press;
            }
            else if (trigger == InputBindingTrigger.Tap)
            {
                trigger = InputBindingTrigger.Press;
            }

            HistorySnapshot before = CaptureSnapshot();
            InputActionMap map =
                asset.FindActionMap(GameplayMapName, false);
            bool mapWasEnabled = map != null && map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            action.AddBinding(new InputBinding
            {
                path = path,
                interactions = GetInteraction(trigger),
                groups = GetGroup(device),
                action = action.name
            });
            InputActionInteractionPolicy.Normalize(asset);

            if (mapWasEnabled)
            {
                map.Enable();
            }

            int bindingIndex = action.bindings.Count - 1;
            ApplyChange(before);
            return bindingIndex;
        }

        public void Dispose()
        {
            if (!ownsAsset || asset == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(asset);
        }

        public void RemoveBinding(
            InputActionId actionId,
            int bindingIndex)
        {
            if (!TryGetAction(actionId, out InputAction action) ||
                bindingIndex < 0 ||
                bindingIndex >= action.bindings.Count)
            {
                return;
            }

            HistorySnapshot before = CaptureSnapshot();
            InputActionMap map =
                asset.FindActionMap(GameplayMapName, false);
            bool mapWasEnabled = map != null && map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            int lastIndex = bindingIndex;
            if (action.bindings[bindingIndex].isComposite)
            {
                while (lastIndex + 1 < action.bindings.Count &&
                       action.bindings[lastIndex + 1]
                           .isPartOfComposite)
                {
                    lastIndex++;
                }
            }

            for (int index = lastIndex;
                 index >= bindingIndex;
                 index--)
            {
                action.ChangeBinding(index).Erase();
            }

            if (mapWasEnabled)
            {
                map.Enable();
            }

            ApplyChange(before);
        }

        public void ClearBindings(InputActionId actionId)
        {
            if (!TryGetAction(actionId, out InputAction action) ||
                action.bindings.Count == 0)
            {
                return;
            }

            HistorySnapshot before = CaptureSnapshot();
            InputActionMap map =
                asset.FindActionMap(GameplayMapName, false);
            bool mapWasEnabled = map != null && map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            for (int index = action.bindings.Count - 1;
                 index >= 0;
                 index--)
            {
                action.ChangeBinding(index).Erase();
            }

            if (mapWasEnabled)
            {
                map.Enable();
            }

            ApplyChange(before);
        }

        public void ResetAll()
        {
            HistorySnapshot before = CaptureSnapshot();
            InputActionInteractionPolicy.ResetPolicies();
            LoadAssetJson(defaultJson);
            InputActionInteractionPolicy.Normalize(asset);
            storage.Clear();
            string afterJson = asset.ToJson();
            if (before.AssetJson != afterJson ||
                before.PolicySnapshot !=
                InputActionInteractionPolicy.CapturePolicies())
            {
                PushUndo(before);
            }

            Changed?.Invoke();
        }

        public void Undo()
        {
            if (undoHistory.Count == 0)
            {
                return;
            }

            HistorySnapshot before = CaptureSnapshot();
            HistorySnapshot target =
                undoHistory[undoHistory.Count - 1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            PushRedo(before);
            RestoreSnapshot(target);
            Save();
            Changed?.Invoke();
        }

        public void Redo()
        {
            if (redoHistory.Count == 0)
            {
                return;
            }

            HistorySnapshot before = CaptureSnapshot();
            HistorySnapshot target =
                redoHistory[redoHistory.Count - 1];
            redoHistory.RemoveAt(redoHistory.Count - 1);
            PushUndo(before);
            RestoreSnapshot(target);
            Save();
            Changed?.Invoke();
        }

        public void Save()
        {
            storage.Save(asset.ToJson());
        }

        public void Load()
        {
            string json = storage.Load();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    LoadAssetJson(json);
                    NormalizeActionTriggers();
                }
                catch
                {
                    storage.Clear();
                }
            }
        }

        private static void BakeBindingPath(
            InputAction action,
            int bindingIndex)
        {
            if (bindingIndex < 0 ||
                bindingIndex >= action.bindings.Count)
            {
                return;
            }

            string reboundPath =
                action.bindings[bindingIndex].overridePath;
            if (string.IsNullOrWhiteSpace(reboundPath))
            {
                return;
            }

            InputActionMap map = action.actionMap;
            bool mapWasEnabled = map != null && map.enabled;
            if (mapWasEnabled)
            {
                map.Disable();
            }

            action.RemoveBindingOverride(bindingIndex);
            InputBinding binding =
                action.bindings[bindingIndex];
            binding.path = reboundPath;
            binding.overridePath = null;
            action.ChangeBinding(bindingIndex).To(binding);

            if (mapWasEnabled)
            {
                map.Enable();
            }
        }

        private bool RestoreDefaultBinding(
            InputAction action,
            int bindingIndex)
        {
            InputActionAsset defaultAsset =
                InputActionAsset.FromJson(defaultJson);
            try
            {
                InputAction defaultAction =
                    defaultAsset
                        .FindActionMap(
                            GameplayMapName,
                            false)
                        ?.FindAction(
                            action.name,
                            false);
                if (defaultAction == null)
                {
                    return false;
                }

                string bindingId =
                    action.bindings[bindingIndex].id
                        .ToString();
                for (int index = 0;
                     index < defaultAction.bindings.Count;
                     index++)
                {
                    InputBinding original =
                        defaultAction.bindings[index];
                    if (original.id.ToString() != bindingId)
                    {
                        continue;
                    }

                    InputBinding restored = original;
                    restored.overridePath = null;
                    restored.overrideInteractions = null;
                    restored.overrideProcessors = null;
                    action.ChangeBinding(bindingIndex)
                        .To(restored);
                    return true;
                }
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(defaultAsset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(defaultAsset);
                }
            }

            return false;
        }

        public static string GetActionDisplayName(
            InputActionId actionId)
        {
            switch (actionId)
            {
                case InputActionId.Move:
                    return "移动";
                case InputActionId.Look:
                    return "视角";
                case InputActionId.Navigate:
                    return "导航";
                case InputActionId.Jump:
                    return "跳跃";
                case InputActionId.Interact:
                    return "交互";
                case InputActionId.Cancel:
                    return "取消";
                case InputActionId.Submit:
                    return "确认";
                case InputActionId.Pause:
                    return "暂停";
                case InputActionId.Crouch:
                    return "蹲下";
                case InputActionId.Sprint:
                    return "冲刺";
                case InputActionId.Attack:
                    return "攻击";
                case InputActionId.CameraModeSwitch:
                    return "切换视角";
                case InputActionId.PointerPrimary:
                    return "主指针";
                case InputActionId.PointerSecondary:
                    return "次指针";
                default:
                    return actionId.ToString();
            }
        }

        private HistorySnapshot CaptureSnapshot()
        {
            return new HistorySnapshot
            {
                AssetJson = asset.ToJson(),
                PolicySnapshot =
                    InputActionInteractionPolicy
                        .CapturePolicies()
            };
        }

        private void RestoreSnapshot(
            HistorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            LoadAssetJson(snapshot.AssetJson);
            InputActionInteractionPolicy.RestorePolicies(
                snapshot.PolicySnapshot);
            NormalizeActionTriggers();
        }

        private void ApplyChange(HistorySnapshot before)
        {
            string afterJson = asset.ToJson();
            string afterPolicies =
                InputActionInteractionPolicy.CapturePolicies();
            if (before.AssetJson == afterJson &&
                before.PolicySnapshot == afterPolicies)
            {
                return;
            }

            PushUndo(before);
            Save();
            Changed?.Invoke();
        }

        private void RecordHistory(HistorySnapshot before)
        {
            if (before.AssetJson != asset.ToJson() ||
                before.PolicySnapshot !=
                InputActionInteractionPolicy.CapturePolicies())
            {
                PushUndo(before);
            }
        }

        private void PushUndo(HistorySnapshot snapshot)
        {
            undoHistory.Add(snapshot);
            if (undoHistory.Count > MaxHistory)
            {
                undoHistory.RemoveAt(0);
            }

            redoHistory.Clear();
        }

        private void PushRedo(HistorySnapshot snapshot)
        {
            redoHistory.Add(snapshot);
            if (redoHistory.Count > MaxHistory)
            {
                redoHistory.RemoveAt(0);
            }
        }

        private void NormalizeActionTriggers()
        {
            string beforeJson = asset.ToJson();
            InputActionInteractionPolicy.Normalize(asset);
            if (beforeJson == asset.ToJson())
            {
                return;
            }

            Save();
        }

        private void LoadAssetJson(string json)
        {
            List<string> enabledMaps = new List<string>();
            for (int index = 0;
                 index < asset.actionMaps.Count;
                 index++)
            {
                InputActionMap map = asset.actionMaps[index];
                if (!map.enabled)
                {
                    continue;
                }

                enabledMaps.Add(map.name);
                map.Disable();
            }

            asset.LoadFromJson(json);
            for (int index = 0;
                 index < enabledMaps.Count;
                 index++)
            {
                asset.FindActionMap(
                        enabledMaps[index],
                        false)
                    ?.Enable();
            }
        }

        private bool TryGetAction(
            InputActionId actionId,
            out InputAction action)
        {
            InputActionMap map =
                asset.FindActionMap(GameplayMapName, false);
            action = map?.FindAction(
                actionId.ToString(),
                false);
            return action != null;
        }

        private static string GetBindingDisplayName(
            InputAction action,
            int bindingIndex)
        {
            InputBinding binding = action.bindings[bindingIndex];
            string name = action.GetBindingDisplayString(
                bindingIndex,
                InputBinding.DisplayStringOptions
                    .DontUseShortDisplayNames);
            if (!binding.isPartOfComposite)
            {
                return name;
            }

            string part = string.IsNullOrWhiteSpace(
                    binding.name)
                ? "方向"
                : binding.name;
            return $"{part}: {name}";
        }

        private static InputBindingDevice ResolveDevice(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return InputBindingDevice.Other;
            }

            if (path.Contains("<Keyboard>"))
            {
                return InputBindingDevice.Keyboard;
            }

            if (path.Contains("<Mouse>"))
            {
                return InputBindingDevice.Mouse;
            }

            if (path.Contains("<Gamepad>"))
            {
                return InputBindingDevice.Gamepad;
            }

            if (path.Contains("<Touchscreen>"))
            {
                return InputBindingDevice.Touch;
            }

            return InputBindingDevice.Other;
        }

        private static InputBindingTrigger ResolveTrigger(
            string interactions)
        {
            if (!string.IsNullOrWhiteSpace(interactions) &&
                interactions.Contains("Hold"))
            {
                return InputBindingTrigger.Hold;
            }

            if (!string.IsNullOrWhiteSpace(interactions) &&
                interactions.Contains("Tap"))
            {
                return InputBindingTrigger.Tap;
            }

            return InputBindingTrigger.Press;
        }

        private static string GetInteraction(
            InputBindingTrigger trigger)
        {
            switch (trigger)
            {
                case InputBindingTrigger.Hold:
                    return "Hold";
                case InputBindingTrigger.Tap:
                    return "Tap";
                default:
                    return string.Empty;
            }
        }

        private static string GetDefaultPath(
            InputBindingDevice device)
        {
            switch (device)
            {
                case InputBindingDevice.Keyboard:
                    return "<Keyboard>/space";
                case InputBindingDevice.Mouse:
                    return "<Mouse>/leftButton";
                case InputBindingDevice.Gamepad:
                    return "<Gamepad>/buttonSouth";
                case InputBindingDevice.Touch:
                    return "<Touchscreen>/primaryTouch/tap";
                default:
                    return "<Keyboard>/space";
            }
        }

        private static string GetGroup(
            InputBindingDevice device)
        {
            switch (device)
            {
                case InputBindingDevice.Gamepad:
                    return "Gamepad";
                case InputBindingDevice.Touch:
                    return "Touch";
                default:
                    return "Keyboard&Mouse";
            }
        }
    }
}

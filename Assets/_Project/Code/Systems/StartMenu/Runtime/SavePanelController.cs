using System;
using System.Collections.Generic;
using Project.Saving;
using UnityEngine;
using UnityEngine.UI;

namespace Project.StartMenu
{
    [DisallowMultipleComponent]
    public sealed class SavePanelController : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Transform slotList;
        [SerializeField] private Button backButton;
        [SerializeField] private Button createSlotButton;
        [SerializeField] private bool allowSaving;
        private bool buttonsBound;

        public event Action BackRequested;
        public event Action<string> LoadRequested;
        public event Action<string> SaveRequested;

        public void SetBackButtonVisible(bool value)
        {
            if (backButton != null)
            {
                backButton.gameObject.SetActive(value);
            }
        }

        public void SetAllowSaving(bool value)
        {
            allowSaving = value;
        }

        public void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }

        public string LoadSlot(string slotId)
        {
            SaveGameData data = SaveGameService.Load(slotId);
            if (data == null)
            {
                SetStatus("读取失败：存档不存在。");
                return null;
            }

            SetStatus($"已读取存档：{data.levelLabel}");
            return data.levelLabel;
        }

        public bool SaveCurrentToSlot(
            string slotId,
            string levelLabel,
            string customDataJson)
        {
            SaveGameData data = SaveGameService.Load(slotId);
            if (data == null)
            {
                data = new SaveGameData
                {
                    slotId = slotId,
                    schemaVersion = 1,
                };
            }

            data.levelLabel = levelLabel;
            data.customDataJson = customDataJson ?? "{}";
            bool saved = SaveGameService.Save(data);
            SetStatus(saved
                ? $"已保存到：{data.displayName}"
                : "保存失败。");
            Refresh();
            return saved;
        }

        public void BuildStructure()
        {
            if (slotList != null)
            {
                return;
            }

            Image root = gameObject.GetComponent<Image>();
            if (root == null)
            {
                root = gameObject.AddComponent<Image>();
            }

            root.color = new Color(.045f, .065f, .085f, .94f);

            titleText = StartMenuUiFactory.CreateText(
                "Title",
                transform,
                "选择存档",
                30,
                TextAnchor.MiddleLeft,
                new Color(.92f, .98f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                titleText.rectTransform,
                new Vector2(54f, -42f),
                new Vector2(420f, 48f));

            statusText = StartMenuUiFactory.CreateText(
                "Status",
                transform,
                "存档数据暂未接入游戏进度，这里先提供空壳管理。",
                14,
                TextAnchor.MiddleLeft,
                new Color(.62f, .74f, .82f, 1f));
            StartMenuUiFactory.SetTopLeft(
                statusText.rectTransform,
                new Vector2(56f, -96f),
                new Vector2(780f, 28f));

            ScrollRect scroll = StartMenuUiFactory.CreateVerticalScroll(
                "Save Slots",
                transform,
                new Color(.025f, .04f, .055f, .72f),
                out RectTransform content);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            StartMenuUiFactory.SetTopLeft(
                scrollRect,
                new Vector2(48f, -142f),
                new Vector2(830f, 360f));
            slotList = content;

            createSlotButton = StartMenuUiFactory.CreateButton(
                "Create Empty Save",
                transform,
                "新建空存档",
                new Vector2(180f, 42f),
                null,
                new Color(.24f, .56f, .42f, .96f));
            StartMenuUiFactory.SetTopLeft(
                createSlotButton.GetComponent<RectTransform>(),
                new Vector2(48f, -526f),
                new Vector2(180f, 42f));

            backButton = StartMenuUiFactory.CreateButton(
                "Back",
                transform,
                "返回",
                new Vector2(120f, 42f),
                null,
                new Color(.22f, .29f, .37f, .96f));
            StartMenuUiFactory.SetTopLeft(
                backButton.GetComponent<RectTransform>(),
                new Vector2(246f, -526f),
                new Vector2(120f, 42f));
        }

        public void Refresh()
        {
            if (slotList == null)
            {
                return;
            }

            ClearChildren(slotList);
            IReadOnlyList<SaveSlotInfo> slots = SaveGameService.GetSlots();
            if (slots.Count == 0)
            {
                Text empty = StartMenuUiFactory.CreateText(
                    "Empty",
                    slotList,
                    "暂无存档。",
                    18,
                    TextAnchor.MiddleCenter,
                    new Color(.72f, .82f, .9f, .8f));
                LayoutElement element = empty.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 80f;
                return;
            }

            for (int index = 0; index < slots.Count; index++)
            {
                CreateSlotRow(slots[index]);
            }
        }

        private void Awake()
        {
            if (slotList == null)
            {
                BuildStructure();
            }

            BindButtons();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Refresh();
            }
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            backButton?.onClick.AddListener(() => BackRequested?.Invoke());
            createSlotButton?.onClick.AddListener(CreateEmptySlot);
            buttonsBound = true;
        }

        private void CreateEmptySlot()
        {
            SaveGameService.CreateEmptySlot();
            statusText.text = "已建立一个空存档；具体存档字段后续再定。";
            Refresh();
        }

        private void CreateSlotRow(SaveSlotInfo slot)
        {
            Image row = StartMenuUiFactory.CreateImage(
                "Save Slot",
                slotList,
                new Color(.1f, .16f, .21f, .92f));
            VerticalLayoutGroup layout =
                row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 7, 7);
            layout.spacing = 3f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            LayoutElement rowElement =
                row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 92f;

            Text name = StartMenuUiFactory.CreateText(
                "Name",
                row.transform,
                slot.displayName,
                18,
                TextAnchor.MiddleLeft,
                new Color(.92f, .98f, 1f, 1f));
            name.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            RectTransform actions = StartMenuUiFactory.CreateRect(
                "Actions",
                row.transform);
            HorizontalLayoutGroup actionLayout =
                actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 8f;
            actionLayout.childControlHeight = true;
            actionLayout.childControlWidth = true;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;
            actions.gameObject.AddComponent<LayoutElement>()
                .preferredHeight = 30f;

            Button load = StartMenuUiFactory.CreateButton(
                "Load",
                actions,
                "读取",
                new Vector2(80f, 28f),
                () => RequestLoad(slot),
                new Color(.19f, .48f, .72f, .96f));
            LayoutElement loadElement =
                load.gameObject.AddComponent<LayoutElement>();
            loadElement.preferredWidth = 80f;
            loadElement.preferredHeight = 28f;

            if (allowSaving)
            {
                Button save = StartMenuUiFactory.CreateButton(
                    "Save",
                    actions,
                    "保存",
                    new Vector2(80f, 28f),
                    () => RequestSave(slot),
                    new Color(.24f, .56f, .42f, .96f));
                LayoutElement saveElement =
                    save.gameObject.AddComponent<LayoutElement>();
                saveElement.preferredWidth = 80f;
                saveElement.preferredHeight = 28f;
            }

            Button delete = StartMenuUiFactory.CreateButton(
                "Delete",
                actions,
                "删除",
                new Vector2(80f, 28f),
                () =>
                {
                    SaveGameService.Delete(slot.slotId);
                    statusText.text = $"已删除存档：{slot.displayName}";
                    Refresh();
                },
                new Color(.58f, .2f, .22f, .96f));
            LayoutElement deleteElement =
                delete.gameObject.AddComponent<LayoutElement>();
            deleteElement.preferredWidth = 80f;
            deleteElement.preferredHeight = 28f;
        }

        private void RequestLoad(SaveSlotInfo slot)
        {
            if (LoadRequested != null)
            {
                LoadRequested.Invoke(slot.slotId);
                return;
            }

            SetStatus($"已选择存档：{slot.displayName}（游戏进度接入待定）");
        }

        private void RequestSave(SaveSlotInfo slot)
        {
            if (SaveRequested != null)
            {
                SaveRequested.Invoke(slot.slotId);
                return;
            }

            SetStatus($"已选择存档：{slot.displayName}（当前进度接入待定）");
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}

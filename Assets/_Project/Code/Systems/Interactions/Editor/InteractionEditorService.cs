using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEngine;

namespace Project.Interactions.Editor
{
    [InitializeOnLoad]
    internal static class InteractionEditorAutoSync
    {
        static InteractionEditorAutoSync()
        {
            EditorApplication.delayCall += Synchronize;
        }

        private static void Synchronize()
        {
            EditorApplication.delayCall -= Synchronize;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            InteractionEditorService.SynchronizeWithLevelEditor();
        }
    }

    internal static class InteractionEditorService
    {
        internal const string CatalogPath =
            "Assets/_Project/Resources/Interactions/InteractionObjectCatalog.asset";
        internal const string DefinitionFolder =
            "Assets/_Project/Content/Interactions/Definitions";

        internal static InteractionObjectCatalog EnsureCatalog()
        {
            InteractionObjectCatalog catalog =
                AssetDatabase.LoadAssetAtPath<InteractionObjectCatalog>(
                    CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder("Assets/_Project/Resources/Interactions");
            EnsureFolder("Assets/_Project/Content");
            EnsureFolder("Assets/_Project/Content/Interactions");
            EnsureFolder(DefinitionFolder);
            catalog = ScriptableObject.CreateInstance<InteractionObjectCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        internal static InteractionObjectDefinition FindForPrefab(
            InteractionObjectCatalog catalog,
            GameObject prefab)
        {
            if (catalog == null || prefab == null) return null;
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            for (int index = 0; index < catalog.Objects.Count; index++)
            {
                InteractionObjectDefinition item = catalog.Objects[index];
                if (item == null) continue;
                if (item.Prefab == prefab) return item;

                // Unity can deserialize the same prefab through two different
                // object handles while the asset pipeline is refreshing. Compare
                // the stable asset path as a fallback so synchronization does not
                // create a duplicate definition for one prefab.
                if (!string.IsNullOrWhiteSpace(prefabPath) &&
                    string.Equals(
                        AssetDatabase.GetAssetPath(item.Prefab),
                        prefabPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            // A catalog entry can temporarily lose its object reference during
            // domain reload. Recover an existing definition asset before creating
            // a new one, and let the caller re-add it to the catalog.
            string[] definitionGuids = AssetDatabase.FindAssets(
                "t:InteractionObjectDefinition",
                new[] { DefinitionFolder });
            for (int index = 0; index < definitionGuids.Length; index++)
            {
                InteractionObjectDefinition item = AssetDatabase.LoadAssetAtPath<
                    InteractionObjectDefinition>(
                    AssetDatabase.GUIDToAssetPath(definitionGuids[index]));
                if (item != null &&
                    string.Equals(
                        AssetDatabase.GetAssetPath(item.Prefab),
                        prefabPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        internal static InteractionObjectDefinition CreateOrGetDefinition(
            InteractionObjectCatalog catalog,
            GameObject prefab,
            string displayName,
            string baseColorTypeId)
        {
            if (catalog == null) return null;
            InteractionObjectDefinition existing = FindForPrefab(catalog, prefab);
            if (existing != null)
            {
                if (!catalog.EditableObjects.Contains(existing))
                {
                    Undo.RecordObject(catalog, "恢复物体交互定义目录引用");
                    catalog.EditableObjects.Add(existing);
                    EditorUtility.SetDirty(catalog);
                }
                Undo.RecordObject(existing, "更新物体交互定义");
                existing.Configure(displayName, prefab, baseColorTypeId);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            InteractionObjectDefinition definition =
                ScriptableObject.CreateInstance<InteractionObjectDefinition>();
            definition.Configure(displayName, prefab, baseColorTypeId);
            string safeName = MakeSafeAssetName(
                string.IsNullOrWhiteSpace(displayName) ? "InteractionObject" : displayName);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                DefinitionFolder + "/" + safeName + ".asset");
            AssetDatabase.CreateAsset(definition, path);
            Undo.RecordObject(catalog, "添加物体交互定义");
            catalog.EditableObjects.Add(definition);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return definition;
        }

        internal static void BindDefinitionToPrefab(
            GameObject prefab,
            InteractionObjectDefinition definition)
        {
            if (prefab == null || definition == null) return;
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(
                    AssetDatabase.GetAssetPath(prefab));
                try
                {
                    BindDefinitionToSceneObject(contents, definition);
                    PrefabUtility.SaveAsPrefabAsset(contents,
                        AssetDatabase.GetAssetPath(prefab));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                return;
            }

            BindDefinitionToSceneObject(prefab, definition);
            EditorUtility.SetDirty(prefab);
        }

        internal static void BindDefinitionToSceneObject(
            GameObject target,
            InteractionObjectDefinition definition)
        {
            if (target == null || definition == null) return;
            ColorBlock colorBlock = target.GetComponent<ColorBlock>();
            if (colorBlock != null)
            {
                Undo.RecordObject(colorBlock, "绑定物体交互定义");
                colorBlock.EditorConfigureInteraction(definition);
                EditorUtility.SetDirty(colorBlock);
                return;
            }

            InteractionObject source = target.GetComponent<InteractionObject>();
            if (source == null)
            {
                source = Undo.AddComponent<InteractionObject>(target);
            }

            Undo.RecordObject(source, "绑定物体交互定义");
            source.Configure(definition);
            EditorUtility.SetDirty(source);
        }

        internal static InteractionObjectDefinition EnsureDefinitionForPrefab(
            GameObject prefab,
            string displayName = null,
            string baseColorTypeId = null)
        {
            if (prefab == null) return null;
            InteractionObjectCatalog catalog = EnsureCatalog();
            ColorBlock colorBlock = prefab.GetComponent<ColorBlock>();
            ColorKeyPickup colorKey = prefab.GetComponent<ColorKeyPickup>();
            string color = baseColorTypeId;
            if (string.IsNullOrWhiteSpace(color) && colorBlock != null)
            {
                color = colorBlock.BaseColorTypeId;
            }
            if (string.IsNullOrWhiteSpace(color) && colorKey != null)
            {
                color = colorKey.ColorTypeId;
            }

            InteractionObjectDefinition definition = CreateOrGetDefinition(
                catalog,
                prefab,
                string.IsNullOrWhiteSpace(displayName) ? prefab.name : displayName,
                color);
            EnsureDefaultGraph(definition);
            ColorBlock colorBlockForBinding = prefab.GetComponent<ColorBlock>();
            InteractionObject interactionObjectForBinding =
                prefab.GetComponent<InteractionObject>();
            if ((colorBlockForBinding != null &&
                 colorBlockForBinding.Definition != definition) ||
                (colorBlockForBinding == null &&
                 interactionObjectForBinding != null &&
                 interactionObjectForBinding.Definition != definition) ||
                (colorBlockForBinding == null && interactionObjectForBinding == null))
            {
                BindDefinitionToPrefab(prefab, definition);
            }
            AssetDatabase.SaveAssets();
            return definition;
        }

        /// <summary>
        /// 让交互目录与关卡编辑器栏目保持双向同步。打开交互管理器时会自动执行，
        /// 因此策划不需要先在层级面板选中某个物体。
        /// </summary>
        internal static int SynchronizeWithLevelEditor()
        {
            InteractionObjectCatalog catalog = EnsureCatalog();
            LevelEditorPalette palette = LevelEditorPaletteService.GetOrCreate();
            int synchronizedCount = 0;
            var knownPrefabs = new HashSet<GameObject>();

            string[] colorPrefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/_Project/Content/ColorBlocks/Prefabs" });
            for (int index = 0; index < colorPrefabGuids.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(colorPrefabGuids[index]));
                ColorBlock block = prefab != null
                    ? prefab.GetComponent<ColorBlock>()
                    : null;
                if (block == null)
                {
                    continue;
                }

                InteractionObjectDefinition definition = EnsureDefinitionForPrefab(
                    prefab,
                    prefab.name.Replace("ColorBlock_", string.Empty) + "方块",
                    block.BaseColorTypeId);
                if (definition != null)
                {
                    knownPrefabs.Add(prefab);
                    synchronizedCount++;
                }
            }

            for (int index = 0; index < palette.Entries.Count; index++)
            {
                LevelEditorBlockEntry entry = palette.Entries[index];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                ColorBlock block = entry.Prefab.GetComponent<ColorBlock>();
                InteractionObjectDefinition definition = EnsureDefinitionForPrefab(
                    entry.Prefab,
                    entry.DisplayName,
                    block != null ? block.BaseColorTypeId : null);
                if (definition == null)
                {
                    continue;
                }

                knownPrefabs.Add(entry.Prefab);
                if (entry.InteractionDefinition != definition)
                {
                    Undo.RecordObject(palette, "同步方块交互定义");
                    entry.SetInteractionDefinition(definition);
                    EditorUtility.SetDirty(palette);
                }
                synchronizedCount++;
            }

            for (int index = 0; index < palette.PropEntries.Count; index++)
            {
                LevelEditorPropEntry entry = palette.PropEntries[index];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                InteractionObjectDefinition definition = EnsureDefinitionForPrefab(
                    entry.Prefab,
                    entry.DisplayName);
                if (definition == null)
                {
                    continue;
                }

                knownPrefabs.Add(entry.Prefab);
                if (entry.InteractionDefinition != definition)
                {
                    Undo.RecordObject(palette, "同步道具交互定义");
                    entry.SetInteractionDefinition(definition);
                    EditorUtility.SetDirty(palette);
                }
                synchronizedCount++;
            }

            for (int index = 0; index < catalog.Objects.Count; index++)
            {
                InteractionObjectDefinition definition = catalog.Objects[index];
                if (definition == null || definition.Prefab == null ||
                    knownPrefabs.Contains(definition.Prefab))
                {
                    continue;
                }

                EnsureDefaultGraph(definition);
                ImportToLevelEditor(definition);
                knownPrefabs.Add(definition.Prefab);
                synchronizedCount++;
            }

            EditorUtility.SetDirty(palette);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            LevelEditorState.MarkPaletteChanged();
            return synchronizedCount;
        }

        internal static void ImportToLevelEditor(
            InteractionObjectDefinition definition)
        {
            if (definition == null || definition.Prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "无法导入",
                    "物体交互定义需要先指定一个预制体。",
                    "确定");
                return;
            }

            LevelEditorPalette palette = LevelEditorPaletteService.GetOrCreate();
            int existing = -1;
            for (int index = 0; index < palette.PropEntries.Count; index++)
            {
                if (palette.PropEntries[index] != null &&
                    palette.PropEntries[index].InteractionDefinition == definition)
                {
                    existing = index;
                    break;
                }
            }

            if (existing < 0)
            {
                existing = LevelEditorPaletteService.AddProp(
                    palette,
                    definition.DisplayName,
                    definition.Prefab);
            }

            if (existing >= 0)
            {
                LevelEditorPropEntry entry = palette.PropEntries[existing];
                Undo.RecordObject(palette, "绑定物体交互定义到关卡编辑器");
                entry.SetInteractionDefinition(definition);
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssetIfDirty(palette);
                LevelEditorState.SelectedPropIndex = existing;
                LevelEditorState.MarkPaletteChanged();
            }
        }

        private static void EnsureDefaultGraph(InteractionObjectDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (definition.Nodes.Count > 0)
            {
                EnsureRestoreChain(definition);
                return;
            }

            string colorId = definition.BaseColorTypeId ?? string.Empty;
            AddDefaultChain(definition, InteractionNodeKind.PlayerEntered,
                InteractionNodeKind.RequirePlayer, "OnInteractionPlayerEntered",
                new Vector2(40f, 60f));
            AddDefaultChain(definition, InteractionNodeKind.PlayerLeft,
                InteractionNodeKind.RequirePlayer, "OnInteractionPlayerLeft",
                new Vector2(40f, 280f));
            AddDefaultChain(definition, InteractionNodeKind.ObjectTouched,
                InteractionNodeKind.RequireOtherObject, "OnInteractionObjectTouched",
                new Vector2(420f, 60f));
            AddDefaultChain(definition, InteractionNodeKind.ObjectStay,
                InteractionNodeKind.RequireOtherObject, "OnInteractionObjectStay",
                new Vector2(420f, 280f));
            AddDefaultChain(definition, InteractionNodeKind.ObjectStay,
                InteractionNodeKind.RequirePlayer, "OnInteractionPlayerStay",
                new Vector2(800f, 280f));

            InteractionGraphNode manual = AddNode(
                definition,
                InteractionNodeKind.Manual,
                new Vector2(800f, 60f));
            InteractionGraphNode restore = AddNode(
                definition,
                InteractionNodeKind.Restore,
                new Vector2(1080f, 60f));
            restore.value = colorId;
            restore.number = 1.5f;
            Connect(definition, manual, restore);
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureRestoreChain(
            InteractionObjectDefinition definition)
        {
            string colorId = definition.BaseColorTypeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(colorId))
            {
                return;
            }

            InteractionGraphNode restore = definition.EditableNodes.FirstOrDefault(
                node => node != null && node.kind == InteractionNodeKind.Restore);
            if (restore == null)
            {
                InteractionGraphNode manual = definition.EditableNodes.FirstOrDefault(
                    node => node != null && node.kind == InteractionNodeKind.Manual);
                if (manual == null)
                {
                    manual = AddNode(
                        definition,
                        InteractionNodeKind.Manual,
                        new Vector2(800f, 60f));
                }

                restore = AddNode(
                    definition,
                    InteractionNodeKind.Restore,
                    new Vector2(1080f, 60f));
                Connect(definition, manual, restore);
            }

            if (string.IsNullOrWhiteSpace(restore.value))
            {
                restore.value = colorId;
                restore.number = restore.number <= 0f ? 1.5f : restore.number;
                EditorUtility.SetDirty(definition);
            }
        }

        private static void AddDefaultChain(
            InteractionObjectDefinition definition,
            InteractionNodeKind trigger,
            InteractionNodeKind condition,
            string method,
            Vector2 position)
        {
            InteractionGraphNode triggerNode = AddNode(
                definition,
                trigger,
                position);
            InteractionGraphNode conditionNode = AddNode(
                definition,
                condition,
                position + new Vector2(260f, 0f));
            InteractionGraphNode invokeNode = AddNode(
                definition,
                InteractionNodeKind.InvokeMethod,
                position + new Vector2(520f, 0f));
            invokeNode.value = method;
            Connect(definition, triggerNode, conditionNode);
            Connect(definition, conditionNode, invokeNode);
        }

        private static InteractionGraphNode AddNode(
            InteractionObjectDefinition definition,
            InteractionNodeKind kind,
            Vector2 position)
        {
            InteractionGraphNode node = new InteractionGraphNode
            {
                kind = kind,
                position = position
            };
            definition.EditableNodes.Add(node);
            return node;
        }

        private static void Connect(
            InteractionObjectDefinition definition,
            InteractionGraphNode from,
            InteractionGraphNode to)
        {
            definition.EditableEdges.Add(new InteractionGraphEdge
            {
                fromId = from.id,
                toId = to.id
            });
        }

        internal static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static string MakeSafeAssetName(string value)
        {
            foreach (char character in Path.GetInvalidFileNameChars())
                value = value.Replace(character.ToString(), "_");
            return value.Trim().Replace(' ', '_');
        }
    }
}

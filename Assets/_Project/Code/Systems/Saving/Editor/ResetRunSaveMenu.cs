using UnityEditor;
using UnityEngine;

namespace Project.Saving.Editor
{
    internal static class ResetRunSaveMenu
    {
        [MenuItem("Tools/2026TapTap/存档/重置当前运行存档")]
        private static void ResetCurrentRunSave()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "重置运行存档",
                    "请先退出 Play Mode，再重置运行存档。",
                    "确定");
                return;
            }

            if (!RunSaveService.HasActiveRun)
            {
                EditorUtility.DisplayDialog(
                    "重置运行存档",
                    "当前没有运行存档。",
                    "确定");
                return;
            }

            bool confirmed =
                EditorUtility.DisplayDialog(
                    "重置运行存档",
                    "将删除当前单轮存档。\n" +
                    "下次开始游戏会从头播放开场演出。",
                    "删除存档",
                    "取消");
            if (!confirmed)
            {
                return;
            }

            RunSaveService.Delete();
            Debug.Log(
                "[RunSaveService] 已删除当前运行存档：" +
                RunSaveService.RunFilePath);
        }
    }
}

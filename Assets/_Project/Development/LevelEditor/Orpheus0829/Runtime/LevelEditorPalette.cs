using System.Collections.Generic;
using UnityEngine;

namespace Project.LevelEditor
{
    [CreateAssetMenu(
        fileName = "LevelEditorPalette",
        menuName = "2026TapTap/Level Editor/Block Palette")]
    public sealed class LevelEditorPalette : ScriptableObject
    {
        [SerializeField]
        private List<LevelEditorBlockEntry> entries =
            new List<LevelEditorBlockEntry>();

        public IReadOnlyList<LevelEditorBlockEntry> Entries => entries;

        public void Add(LevelEditorBlockEntry entry)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < entries.Count)
            {
                entries.RemoveAt(index);
            }
        }
    }
}

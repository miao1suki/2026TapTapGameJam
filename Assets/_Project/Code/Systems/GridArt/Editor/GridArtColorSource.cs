using System;
using System.Collections.Generic;

namespace Project.GridArt.Editor
{
    public readonly struct GridArtColorDefinition
    {
        public readonly string id, label;
        public readonly int unityLayer;
        public GridArtColorDefinition(string value, string name, int layer) { id=value; label=name; unityLayer=layer; }
    }

    // The planning adapter resolves ColorCatalog, which lives in Assembly-CSharp.
    // Keep this reusable editor assembly independent of gameplay scripts.
    public static class GridArtColorSource
    {
        public static Func<IReadOnlyList<GridArtColorDefinition>> Read;
        public static IReadOnlyList<GridArtColorDefinition> Colors => Read?.Invoke() ?? Array.Empty<GridArtColorDefinition>();
        public static int UnityLayer(string id)
        {
            if (string.IsNullOrEmpty(id) || id=="none") return 0;
            foreach (var color in Colors) if (color.id==id && color.unityLayer>=0 && color.unityLayer<32) return color.unityLayer;
            throw new InvalidOperationException("颜色目录没有可用的美术层："+id);
        }
    }
}

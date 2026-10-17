using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    public static class GridArtSupplementLibrary
    {
        private const string SourcePath = "Assets/_Project/Resources/26TAPTAP/物体道具/471dca30b9bcb2527867589ad677e3e3.png";

        // Static patterns, not animation strips. Sprite subassets belong to the art
        // library so rebuilding gameplay artwork cannot delete these selections.
        public static int Import(GridArtPalette library)
        {
            if (library == null || !AssetDatabase.Contains(library) || EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (texture == null) return 0;
            if (texture.width < 432 || texture.height < 336) throw new InvalidOperationException("场景装饰原图尺寸与切片方案不符，请检查源素材。");
            string guid = AssetDatabase.AssetPathToGUID(SourcePath);
            var existing = new Dictionary<string, GridArtBrush>();
            foreach (var brush in library.brushes) if (!string.IsNullOrEmpty(brush.sourceKey)) existing[brush.sourceKey]=brush;
            int count=0;
            void Add(string key, string label, string color, string category, int x, int top)
            {
                key=guid+"#"+key;
                if (existing.TryGetValue(key,out var prior) && prior.sprite != null) return;
                // The sheet is 447px wide. The final column really has 15 pixels;
                // preserve 15/16 of a cell instead of stretching or reading past it.
                int width=Mathf.Min(16,texture.width-x),height=Mathf.Min(16,texture.height-top);
                if (width<=0 || height<=0) throw new InvalidOperationException("场景装饰切片超出源图："+label);
                var sprite=Sprite.Create(texture,new Rect(x,texture.height-top-height,width,height),Vector2.one*.5f,
                    Project.ObjectArtwork.GridArtworkMetrics.PixelsPerCell,0,SpriteMeshType.FullRect);
                sprite.name=key.Substring(guid.Length+1);
                AssetDatabase.AddObjectToAsset(sprite,library);
                Vector2 display=GridArtPixelLayout.NativeSize(sprite,new Rect(0,0,1,1));
                if (prior != null) { prior.sprite=sprite; EditorUtility.SetDirty(library); return; }
                var entry=new GridArtBrush { sourceKey=key,label=label,category=category,sprite=sprite,colorId=color,
                    visualSize=display,size=new Vector2Int(Mathf.Max(1,Mathf.CeilToInt(display.x-.0001f)),Mathf.Max(1,Mathf.CeilToInt(display.y-.0001f))) };
                library.brushes.Add(entry); existing[key]=entry; count++;
            }
            string[] colors={"red","blue","green"},names={"红色","蓝色","绿色"};
            int[] columns={224,256,288,320,352,384,400,416,432};
            for (int c=0;c<3;c++) for(int variant=0;variant<2;variant++) for(int x=0;x<columns.Length;x++)
                Add($"Decoration_{colors[c]}_{variant}_{x}",$"{names[c]}装饰 {variant*columns.Length+x+1:00}",colors[c],"颜色装饰",columns[x],160+c*64+variant*32);
            for (int c=0;c<3;c++) for(int row=0;row<2;row++) for(int x=0;x<4;x++)
                Add($"RainRaw_{colors[c]}_{row}_{x}",$"{names[c]}雨滴图案 {row*4+x+1:00}",colors[c],"雨滴原图",96+x*32,144+c*32+row*16);
            if (count>0) EditorUtility.SetDirty(library);
            return count;
        }

        [MenuItem("Tools/2026TapTap/关卡美术/汇入颜色装饰素材")]
        public static void ImportShared()
        {
            var library=GridArtLibraryService.Shared();
            Undo.RecordObject(library,"汇入颜色装饰"); int count=Import(library); AssetDatabase.SaveAssetIfDirty(library);
            EditorGUIUtility.PingObject(library);
            Debug.Log($"颜色装饰素材库已同步，新增{count}项。",library);
        }
    }
}

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
            void Add(string key, string label, string color, string category, int x, int top,
                int requestedWidth=16, int requestedHeight=16, string oldLabel=null, string oldCategory=null)
            {
                key=guid+"#"+key;
                if (existing.TryGetValue(key,out var prior))
                {
                    // Correct only the old generated defaults; retain artists' custom names.
                    if (prior.label==oldLabel) { prior.label=label; EditorUtility.SetDirty(library); }
                    if (prior.category==oldCategory) { prior.category=category; EditorUtility.SetDirty(library); }
                    if (prior.sprite != null) return;
                }
                // The sheet is 447px wide. The final column really has 15 pixels;
                // preserve 15/16 of a cell instead of stretching or reading past it.
                int width=Mathf.Min(requestedWidth,texture.width-x),height=Mathf.Min(requestedHeight,texture.height-top);
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
            {
                bool rain=x==3 || x==4;
                string group=rain ? "雨滴原图" : x<3 ? "壁饰团簇" : "装饰立柱";
                Add($"Decoration_{colors[c]}_{variant}_{x}",$"{names[c]}{group} {variant+1}-{(rain?x-2:x<3?x+1:x-4)}",colors[c],group,
                    columns[x],160+c*64+variant*32,oldLabel:$"{names[c]}装饰 {variant*columns.Length+x+1:00}",oldCategory:"颜色装饰");
            }
            for (int c=0;c<3;c++) for(int row=0;row<2;row++) for(int x=0;x<4;x++)
            {
                if(existing.TryGetValue(guid+$"#RainRaw_{colors[c]}_{row}_{x}",out var grass) &&
                    grass.label==$"{names[c]}地面草饰 {x+1}·{(row==0?"上半":"下半")}")
                { grass.label=$"{names[c]}地面草饰 {x+1}·款{row+1}"; EditorUtility.SetDirty(library); }
                Add($"RainRaw_{colors[c]}_{row}_{x}",$"{names[c]}地面草饰 {x+1}·款{row+1}",colors[c],"地面草饰·拆分片",
                    96+x*32,144+c*32+row*16,oldLabel:$"{names[c]}雨滴图案 {row*4+x+1:00}",oldCategory:"雨滴原图");
            }
            // Append after all 78 legacy slots. Their indices/UVs/sizes must not move.
            for (int c=0;c<3;c++) for(int x=0;x<4;x++)
                Add($"GroundGrass_{colors[c]}_{x}",$"{names[c]}地面草饰 {x+1}·双款原排",colors[c],"地面草饰·组合",96+x*32,144+c*32,16,32,
                    oldLabel:$"{names[c]}地面草饰 {x+1}·完整",oldCategory:"地面草饰");
            string[] waveColors={"blue","red","green"},waveNames={"蓝色","红色","绿色"};
            for(int c=0;c<3;c++) for(int row=0;row<2;row++)
                Add($"WallWave_{waveColors[c]}_{row}",$"{waveNames[c]}波纹 {row+1}",waveColors[c],"墙面波纹",c*32,160+row*32);
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

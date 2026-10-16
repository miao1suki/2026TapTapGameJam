using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public sealed partial class GridArtWindow
    {
        private Foldout BuildBrushSettings()
        {
            var section = Section("当前素材设置", false); section.name = "art-brush-settings";
            var brush = Brush;
            void Edit(Action operation)
            {
                if (!CanEditMap || EditorApplication.isPlayingOrWillChangePlaymode) return;
                Undo.RecordObject(map.palette,"设置美术素材"); operation(); EditorUtility.SetDirty(map.palette);
                foreach (var layer in map.layers) layer.InvalidateLookup();
                ArtChanged();
            }
            section.Add(new HelpBox("素材定义由所有关卡共享；摆放位置仍属于各自关卡。",HelpBoxMessageType.Info));
            var label = new TextField("名称") { value=brush.label, isDelayed=true }; label.RegisterValueChangedCallback(e => Edit(()=>brush.label=e.newValue)); section.Add(label);
            var categoryField = new TextField("分类") { value=brush.category, isDelayed=true }; categoryField.RegisterValueChangedCallback(e=>Edit(()=>brush.category=e.newValue)); section.Add(categoryField);
            var size = new Vector2IntField("占格") { value=brush.Size }; size.RegisterValueChangedCallback(e=>Edit(()=>brush.size=Vector2Int.Max(Vector2Int.one,e.newValue))); section.Add(size);
            var display = new Vector2Field("显示尺寸") { value=brush.DisplaySize }; display.RegisterValueChangedCallback(e=>Edit(()=>brush.visualSize=Vector2.Max(Vector2.one*.01f,e.newValue))); section.Add(display);
            var normal = new ObjectField("法线图集") { objectType=typeof(Texture2D),allowSceneObjects=false,value=brush.normalMap }; normal.RegisterValueChangedCallback(e=>Edit(()=>brush.normalMap=e.newValue as Texture2D)); section.Add(normal);
            var auto = new Toggle("自动边角") { value=brush.autoTerrain }; auto.RegisterValueChangedCallback(e=> { Edit(()=>brush.autoTerrain=e.newValue); Refresh(); }); section.Add(auto);
            if (brush.autoTerrain)
            {
                string[] names={ "左上","上边","右上","左边","中心","右边","左下","下边","右下","内左下","内右下","内左上","内右上" };
                for(int i=0;i<13;i++)
                {
                    int slot=i; var field=new ObjectField(names[i]) { objectType=typeof(Sprite),allowSceneObjects=false,value=brush.pieces!=null && i<brush.pieces.Length ? brush.pieces[i] : null };
                    field.RegisterValueChangedCallback(e=>Edit(()=>{ if(brush.pieces==null || brush.pieces.Length!=13) Array.Resize(ref brush.pieces,13); brush.pieces[slot]=e.newValue as Sprite; })); section.Add(field);
                }
            }
            section.SetEnabled(CanEditMap && !EditorApplication.isPlayingOrWillChangePlaymode); return section;
        }
    }
}

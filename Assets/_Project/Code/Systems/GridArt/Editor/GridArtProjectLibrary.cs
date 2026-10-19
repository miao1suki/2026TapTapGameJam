using System;
using System.Linq;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    /// <summary>Explicit, audited selections: never import whole sheets containing labels or animation strips.</summary>
    public static class GridArtProjectLibrary
    {
        public const string BackgroundPath="Assets/_Project/Resources/26TAPTAP/场景背景/tail.png";
        public static int Import(GridArtPalette library)
        {
            if (library==null || !AssetDatabase.Contains(library) || EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            int added=0;
            void Add(string key, string label, string category, Sprite sprite, string color="", int exportScale=1)
            {
                if (sprite==null) return;
                var prior=library.brushes.FirstOrDefault(b=>b.sourceKey==key);
                if (prior!=null)
                {
                    // The formal artwork may be regenerated. Keep the selection's stable slot.
                    if (prior.sprite!=sprite) { prior.sprite=sprite; EditorUtility.SetDirty(library); }
                    return;
                }
                Vector2 native=GridArtPixelLayout.NativeSize(sprite,new Rect(0,0,1,1),exportScale);
                library.brushes.Add(new GridArtBrush { sourceKey=key,label=label,category=category,sprite=sprite,
                    colorId=color,sourcePixelScale=exportScale,visualSize=native,
                    size=new Vector2Int(Mathf.Max(1,Mathf.CeilToInt(native.x-.0001f)),Mathf.Max(1,Mathf.CeilToInt(native.y-.0001f))) });
                EditorUtility.SetDirty(library); added++;
            }
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (texture!=null)
            {
                if (texture.width!=1408 || texture.height!=704) throw new InvalidOperationException("tail原图尺寸已变化，请重新核对4倍导出与切片。");
                string guid=AssetDatabase.AssetPathToGUID(BackgroundPath);
                void Crop(string id,string label,string category,int x,int top,int width=64,int height=64)
                {
                    string key=guid+"#scene:"+id;
                    var rect=new Rect(x,texture.height-top-height,width,height);
                    var prior=library.brushes.FirstOrDefault(b=>b.sourceKey==key);
                    if(prior?.sprite!=null) return;
                    // Reuse the original 20 wall selections rather than duplicating their slots.
                    if(library.brushes.Any(b=>!b.autoTerrain && b.sprite!=null && b.sprite.texture==texture && b.sprite.rect==rect)) return;
                    if(x<0 || top<0 || rect.xMax>texture.width || top+height>texture.height || x%4!=0 || top%4!=0 || width%4!=0 || height%4!=0)
                        throw new InvalidOperationException("背景选区越界或切断有效像素："+label);
                    var sprite=Sprite.Create(texture,rect,Vector2.one*.5f,GridArtworkMetrics.PixelsPerCell*4,0,SpriteMeshType.FullRect);
                    sprite.name="Scene_"+id; AssetDatabase.AddObjectToAsset(sprite,library);
                    if(prior!=null) { prior.sprite=sprite; EditorUtility.SetDirty(library); }
                    else Add(key,label,category,sprite,exportScale:4);
                }
                for(int row=0;row<5;row++) for(int col=0;col<4;col++)
                    Crop($"wall_{row}_{col}",$"墙地纹样 {row*4+col+1:00}","墙地纹样",64+col*128,64+row*128);
                string[] direction={"左上","上","右上","左","中","右","左下","下","右下"};
                for(int row=0;row<3;row++) for(int col=0;col<3;col++) if(row!=1 || col!=1)
                    Crop($"frame_{row}_{col}","白线边框·"+direction[row*3+col],"边框·拆分片",640+col*128,320+row*128);
                foreach(var part in new[]{(1,0),(0,1),(2,1),(1,2)})
                    Crop($"ornament_{part.Item2}_{part.Item1}","纹饰边框·"+direction[part.Item2*3+part.Item1],"边框·拆分片",1024+part.Item1*128,320+part.Item2*128);
                Crop("frame_whole","白线边框·完整5×5","边框·完整",640,320,320,320);
                Crop("ornament_whole","纹饰边框·完整5×5","边框·完整",1024,320,320,320);
            }
            var artwork=ObjectArtworkLibrary.Load();
            if(artwork!=null)
            {
                foreach(var kind in Enum.GetValues(typeof(MechanismArtworkKind)).Cast<MechanismArtworkKind>())
                {
                    var field=typeof(MechanismArtworkKind).GetField(kind.ToString());
                    var attribute=field.GetCustomAttributes(typeof(InspectorNameAttribute),false).FirstOrDefault() as InspectorNameAttribute;
                    string label=attribute?.displayName ?? kind.ToString();
                    Add("project-artwork:"+kind+":idle",label+"·静态摆件","道具外观·无玩法功能",artwork.FirstFrame(kind),ColorOf(kind));
                }
                var baseFrames=artwork.Find(MechanismArtworkKind.MechanismBase)?.frames;
                if(baseFrames?.Length>1) Add("project-artwork:base:active","基座·激活外观","道具外观·无玩法功能",baseFrames[1],"red");
                Add("project-artwork:respawn:locked","重生点·未激活外观","道具外观·无玩法功能",artwork.respawnLocked);
                Add("project-artwork:respawn:used","重生点·已激活外观","道具外观·无玩法功能",artwork.respawnUsed);
                Add("project-artwork:respawn:current","重生点·当前外观","道具外观·无玩法功能",artwork.respawnCurrent.FirstOrDefault());
                Add("project-artwork:plant:idle","弹性植物·静态摆件","道具外观·无玩法功能",
                    artwork.plantBounce.Length>0?artwork.plantBounce[Mathf.Clamp(artwork.plantIdleFrame,0,artwork.plantBounce.Length-1)]:null,"green");
                for(int i=0;i<artwork.signboards.Length;i++)
                    Add("project-artwork:sign:"+i,"告示牌外观 "+(i+1),"告示牌外观·无触发",artwork.signboards[i]);
                string[] colors={"red","green","blue"}; string[] names={"红色","绿色","蓝色"};
                for(int i=0;i<artwork.doorIndicators.Length;i++)
                    Add("project-artwork:door-indicator:"+i,(i<3?names[i]:"")+"门符号","门饰符号",artwork.doorIndicators[i],i<3?colors[i]:"");
                for(int i=0;i<artwork.doorWhiteIndicators.Length;i++)
                    Add("project-artwork:door-white:"+i,"白色门符号 "+(i+1),"门饰符号",artwork.doorWhiteIndicators[i]);
            }
            int beforeBorder = library.brushes.Count;
            GridArtBorderLibrary.Ensure(library);
            added += library.brushes.Count - beforeBorder;
            return added;
        }
        private static string ColorOf(MechanismArtworkKind kind)
        {
            string name=kind.ToString();
            if(name.StartsWith("Red",StringComparison.Ordinal) || kind==MechanismArtworkKind.Lava || kind==MechanismArtworkKind.MechanismBase || kind==MechanismArtworkKind.DoorButton) return "red";
            if(name.StartsWith("Green",StringComparison.Ordinal) || kind==MechanismArtworkKind.PlantObstacle) return "green";
            if(name.StartsWith("Blue",StringComparison.Ordinal) || kind==MechanismArtworkKind.Obsidian) return "blue";
            return "";
        }
        [MenuItem("Tools/2026TapTap/关卡美术/同步项目共享素材库")]
        public static void SynchronizeShared()
        {
            var library=GridArtLibraryService.Shared();
            int count=GridArtLibraryService.Synchronize(library); EditorGUIUtility.PingObject(library);
            Debug.Log($"共享场景素材库已同步，新增{count}项；道具外观仅供美术摆放，不附带玩法。",library);
        }
    }
}

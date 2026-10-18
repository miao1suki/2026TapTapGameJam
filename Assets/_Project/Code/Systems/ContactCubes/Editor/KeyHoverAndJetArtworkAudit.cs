using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Items;
using Project.ObjectArtwork;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.ContactCubes.Editor
{
    /// <summary>Silent, real-Play lifecycle and GPU checks; never opens an author's scene.</summary>
    public static class KeyHoverAndJetArtworkAudit
    {
        private const string Flag = "KeyHoverAndJetArtworkAudit.Running";
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly List<KeySample> keys = new List<KeySample>();
        private static int phase, checks, frames;
        private static double started;
        private static float phaseTime;
        private static PlayerController player;
        private static ColorKeyPickup collecting;
        private static ContactCube blue, red;
        private static CubeWaterJet jet;
        private static CubeWaterJetArtwork art;
        private static KeyJetAuditReceiver receiver;
        private static Camera camera;
        private static RenderTexture target;
        private static int materialCount, childCount;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Only run in the silent isolated probe, with a real graphics device.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Flag,true); Resume(); EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag,false)) return;
            phase=checks=frames=0; keys.Clear(); started=EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        private static void Check(bool condition,string message)
        {
            if (!condition) throw new InvalidOperationException("KEY/JET: " + message);
            checks++; Debug.Log("KEY/JET PASS: " + message);
        }
        private static void Next() { phase++; phaseTime=Time.time; frames=0; }
        private static GameObject Prefab(string relative,Transform parent=null) => Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/"+relative),parent);
        private static object Call(object owner,Type type,string method,params object[] args) =>
            type.GetMethod(method,Hidden).Invoke(owner,args);
        private static Dictionary<int,Vector2> Velocities => (Dictionary<int,Vector2>)typeof(PlayerController)
            .GetField("waterVelocities",Hidden).GetValue(player);

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup-started>100) { Finish(false,"Timeout, phase "+phase); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time<.2f) return;
            try
            {
                switch (phase)
                {
                    case 0:
                        AudioListener.volume=0; AudioListener.pause=true;
                        Physics.simulationMode=SimulationMode.Script;
                        ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                        foreach (float cell in new[]{1f,2f})
                        {
                            var root=new GameObject("Key grid "+cell);
                            root.AddComponent<KeyJetAuditGrid>().Size=cell;
                            for (int index=0;index<3;index++)
                            {
                                string color=new[]{"red","green","blue"}[index];
                                var key=Prefab("ColorBlocks/Prefabs/ColorKey_"+color+".prefab",root.transform)
                                    .GetComponent<ColorKeyPickup>();
                                key.transform.position=new Vector3(100+index*5,cell*10,0);
                                key.EditorConfigure(color,null);
                                keys.Add(new KeySample { Key=key,Cell=cell });
                            }
                        }
                        Next(); break;
                    case 1:
                        if (Time.time-phaseTime<.4f) break; // Appearance punch has finished.
                        foreach (KeySample sample in keys) sample.Begin();
                        Next(); break;
                    case 2:
                        foreach (KeySample sample in keys) sample.Sample();
                        if (Time.time-phaseTime<2.2f) break;
                        foreach (KeySample sample in keys)
                        {
                            Check(sample.Minimum < -sample.Cell*.10f && sample.Maximum > sample.Cell*.10f,
                                sample.Key.ColorTypeId+" oscillates both ways at cell "+sample.Cell);
                            Check(sample.Maximum <= sample.Cell*.1251f && sample.Minimum >= -sample.Cell*.1251f,
                                "hover amplitude stays exactly within two art pixels");
                            Check(!sample.Drift,"root, trigger and base artwork layout never drift");
                            sample.Key.gameObject.SetActive(false);
                            Check(Mathf.Abs(sample.Key.IdleHoverOffsetWorld)<.00001f,"disable removes only the idle offset");
                            sample.Key.gameObject.SetActive(true); sample.Key.ResetForReuse();
                        }
                        player=Prefab("Player/Player.prefab").GetComponent<PlayerController>();
                        player.GetComponent<PlayerInputDriver>().enabled=false;
                        player.Motor.constraints=RigidbodyConstraints.FreezeAll;
                        player.enabled=false;
                        player.transform.position=keys[0].Key.transform.position+Vector3.right*4;
                        collecting=keys[0].Key;
                        Check((bool)Call(collecting,typeof(DropItemBase),"TryBeginAttraction",player.gameObject),
                            "shared drop lifecycle accepts attraction after pool reuse");
                        Next(); break;
                    case 3:
                        if (Time.time-phaseTime<.24f) break;
                        Check(collecting!=null && collecting.IsAttracting,"attraction remains the root animation owner");
                        Check(Mathf.Abs(collecting.IdleHoverOffsetWorld)<.00001f,"idle motion eases fully out before attraction completes");
                        Next(); break;
                    case 4:
                        if (Time.time-phaseTime<.18f) break;
                        Check(collecting!=null && collecting.CollectionRequested,"attraction hands off to shared pickup flight");
                        Check(!collecting.GetComponent<Collider>().enabled,"collection immediately disables the real trigger");
                        Check(Mathf.Abs(collecting.IdleHoverOffsetWorld)<.00001f,"idle animation never writes during pickup flight");
                        Next(); break;
                    case 5:
                        if (Time.time-phaseTime<1.1f) break;
                        Check(collecting==null || collecting.IsCollectionVisualComplete,"pickup completes without a lingering hovering ghost");
                        foreach (KeySample sample in keys) if (sample.Key!=null) Object.Destroy(sample.Key.transform.parent.gameObject);
                        BeginJet(); Next(); break;
                    case 6:
                        if (++frames<6) break;
                        VerifyGpu(); VerifyStableBatches(); VerifyContactRetirement(); VerifyDirectionsAndCellSize();
                        Finish(true,"PASS "+checks+" key hover / contact jet assertions"); break;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false,"FAIL phase "+phase+" after "+checks+"\n"+error); }
        }

        private static ContactCube Cube(CubeColor color,Vector3 position,Transform parent=null)
        {
            ContactCube cube=Prefab("ContactCubes/"+color+"_ContactCube.prefab",parent).GetComponent<ContactCube>();
            cube.transform.position=position;
            cube.GetComponent<ContactCubeMotion>().enabled=false;
            var body=cube.GetComponent<Rigidbody>(); body.interpolation=RigidbodyInterpolation.None;
            body.isKinematic=true; body.useGravity=false; body.position=position;
            return cube;
        }
        private static void BeginJet()
        {
            blue=Cube(CubeColor.Blue,new Vector3(-6,0,0)); red=Cube(CubeColor.Red,new Vector3(-7,0,0));
            player.transform.position=new Vector3(-4,0,0); player.Motor.position=player.transform.position;
            var item=new GameObject("Jet force receiver"); item.transform.position=new Vector3(-3,0,0);
            item.AddComponent<BoxCollider>().size=Vector3.one*.4f; receiver=item.AddComponent<KeyJetAuditReceiver>();
            Physics.SyncTransforms(); Call(blue,typeof(ContactCube),"FixedUpdate");
            jet=blue.GetComponentInChildren<CubeWaterJet>();
            Check(jet!=null && jet.isActiveAndEnabled,"real red/blue face contact creates a jet in the first step");
            Check(Velocities.TryGetValue(jet.GetInstanceID(),out Vector2 push) && Mathf.Abs(push.x-8f)<.001f,
                "first contact step already publishes 5 x 1.6 player speed, without moving the player root");
            Check(Vector3.Distance(receiver.Velocity,Vector3.right*5)<.001f,"non-player push speed remains five cells per second");
            Check(player.transform.position==new Vector3(-4,0,0),"jet requests controller velocity, never teleports the player");
            art=jet.GetComponent<CubeWaterJetArtwork>();
            Check(art!=null && art.VisibleBubbleCount>0 && art.VisibleBubbleCount<=96,"first contact pre-fills a bounded bubble stream");
            Check(art.RendererCount==7 && art.GetComponentsInChildren<Renderer>().Length==7,"six real TA bubble frames use six batches, plus one water batch");
            Check(art.GetComponentsInChildren<Collider>().Length==0,"presentation creates no extra trigger or solid");
            Check(art.BubbleSpeedCells==6,"default fast bubble motion is six cells per second");
            VerifyPixels(art,1);
            materialCount=ColorAppearanceManager.Instance.SharedMaterialCount;
            childCount=art.GetComponentsInChildren<Transform>().Length;
            ColorAppearanceManager.Instance.Play("blue",true,0);
            camera=new GameObject("Actual jet frame camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(-3,0,-20); camera.orthographic=true; camera.orthographicSize=2.5f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            target=new RenderTexture(512,256,24); target.Create(); camera.targetTexture=target;
        }
        private static void VerifyGpu()
        {
            var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            RenderTexture previous=RenderTexture.active; RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); RenderTexture.active=previous;
            int visible=0;
            // Middle of the jet, away from both cubes and the player's opaque silhouette.
            for (int y=110;y<146;y++) for (int x=280;x<440;x++)
            { Color32 pixel=image.GetPixel(x,y); if (pixel.r+pixel.g+pixel.b>30) visible++; }
            Check(visible>150,"actual camera GPU frame visibly renders formal water and pre-filled bubbles");
            File.WriteAllBytes("Library/KeyHoverAndJetArtworkAudit.png",image.EncodeToPNG()); Object.Destroy(image);
        }
        private static void VerifyStableBatches()
        {
            for (int i=0;i<150;i++) art.Refresh();
            Check(art.GetComponentsInChildren<Transform>().Length==childCount,"refresh does not instantiate particles or children");
            Check(ColorAppearanceManager.Instance.SharedMaterialCount==materialCount,"refresh makes no material copies");
            Material first=null;
            foreach (Renderer renderer in art.GetComponentsInChildren<Renderer>())
            { if (first==null) first=renderer.sharedMaterial; Check(first==renderer.sharedMaterial,"water and bubbles share one blue HSV material"); }
            art.Configure(100,1,1,4,6);
            Check(art.VisibleBubbleCount==96,"long dense author settings cannot exceed the 96-particle pool");
        }
        private static void VerifyContactRetirement()
        {
            red.transform.position=Vector3.left*30; red.GetComponent<Rigidbody>().position=red.transform.position;
            Physics.SyncTransforms(); Call(jet,typeof(CubeWaterJet),"FixedUpdate");
            Check(!jet.gameObject.activeSelf,"real separation retires the water column before its next push");
            Check(!Velocities.ContainsKey(jet.GetInstanceID()) && receiver.Velocity==Vector3.zero,
                "separation immediately releases all player and object velocity sources");
            red.transform.position=blue.transform.position+Vector3.left;
            red.GetComponent<Rigidbody>().position=red.transform.position;
            Physics.SyncTransforms(); Call(blue,typeof(ContactCube),"FixedUpdate");
            CubeWaterJet restored=blue.GetComponentInChildren<CubeWaterJet>();
            Check(restored!=null && restored.GetComponent<CubeWaterJetArtwork>().VisibleBubbleCount>0,
                "recontact reuses the same bounded visual lifecycle without startup delay");
            blue.ResetForRoom();
            Check(!Velocities.ContainsKey(restored.GetInstanceID()),"room reset releases a rebuilt jet's player velocity source");
        }
        private static void VerifyDirectionsAndCellSize()
        {
            Object.DestroyImmediate(blue.gameObject); Object.DestroyImmediate(red.gameObject);
            var root=new GameObject("Two world units per authored cell"); root.AddComponent<KeyJetAuditGrid>().Size=2;
            blue=Cube(CubeColor.Blue,new Vector3(30,0,0),root.transform);
            red=Cube(CubeColor.Red,new Vector3(28,0,0),root.transform);
            foreach (Vector3 direction in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down})
            {
                red.transform.position=blue.transform.position-direction*2; red.GetComponent<Rigidbody>().position=red.transform.position;
                Physics.SyncTransforms(); Call(blue,typeof(ContactCube),"FixedUpdate");
                CubeWaterJet current=blue.GetComponentInChildren<CubeWaterJet>();
                Check(current!=null && Vector3.Dot(current.Direction,direction)>.999f,"true two-unit cell contact supports "+direction);
                Check(Mathf.Abs(current.PlayerPushSpeed-16)<.001f,"player speed scales with the true grid, not prefab scale");
                VerifyPixels(current.GetComponent<CubeWaterJetArtwork>(),2);
            }
        }
        private static void VerifyPixels(CubeWaterJetArtwork artwork,float cell)
        {
            int quads=0;
            foreach (MeshFilter filter in artwork.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.name.StartsWith("TA jet bubbles ",StringComparison.Ordinal)) continue;
                var props=new MaterialPropertyBlock(); filter.GetComponent<Renderer>().GetPropertyBlock(props);
                var texture=props.GetTexture("_MainTex"); Vector3[] vertices=filter.sharedMesh.vertices; Vector2[] uv=filter.sharedMesh.uv;
                for (int i=0;i<vertices.Length;i+=4)
                {
                    Vector3 a=filter.transform.TransformPoint(vertices[i]), b=filter.transform.TransformPoint(vertices[i+1]);
                    float pixels=(uv[i+1].x-uv[i].x)*texture.width;
                    Check(Mathf.Abs(Vector3.Distance(a,b)-pixels*cell/16)<.0001f,"bubble size preserves original 16px-per-cell scale");
                    Check(Mathf.Abs(a.x/(cell/16)-Mathf.Round(a.x/(cell/16)))<.001f &&
                        Mathf.Abs(a.y/(cell/16)-Mathf.Round(a.y/(cell/16)))<.001f,"bubble source pixel edges align to world art pixels");
                    quads++;
                }
            }
            Check(quads>0 && quads<=96,"pre-filled artwork has visible bounded geometry in every direction");
        }
        private static void Finish(bool success,string report)
        {
            EditorApplication.update-=Tick; SessionState.EraseBool(Flag);
            File.WriteAllText("Library/KeyHoverAndJetArtworkAudit.txt",report); Debug.Log(report);
            if (target!=null) target.Release();
            EditorApplication.Exit(success?0:1);
        }
        private sealed class KeySample
        {
            public ColorKeyPickup Key; public float Cell,Minimum=float.PositiveInfinity,Maximum=float.NegativeInfinity;
            public bool Drift; private Vector3 root,baseVisual; private Bounds bounds; private SpriteRenderer sprite;
            public void Begin()
            {
                root=Key.transform.position; bounds=Key.GetComponent<Collider>().bounds;
                sprite=Key.GetComponentInChildren<SpriteRenderer>();
                baseVisual=sprite.transform.position-Vector3.up*Key.IdleHoverOffsetWorld;
                Check(Mathf.Abs(Key.IdleHoverAmplitudeWorld-Cell/8)<.00001f,"hover uses true grid pixel conversion");
                Vector2 natural=GridArtworkMetrics.SpriteCells(sprite.sprite)*Cell;
                Check(Mathf.Abs(sprite.bounds.size.x-natural.x)<.001f && Mathf.Abs(sprite.bounds.size.y-natural.y)<.001f,
                    "formal key artwork retains natural source dimensions");
            }
            public void Sample()
            {
                float offset=Key.IdleHoverOffsetWorld; Minimum=Mathf.Min(Minimum,offset); Maximum=Mathf.Max(Maximum,offset);
                Bounds now=Key.GetComponent<Collider>().bounds;
                Drift |= Vector3.Distance(Key.transform.position,root)>.00001f || Vector3.Distance(now.center,bounds.center)>.00001f ||
                    Vector3.Distance(now.size,bounds.size)>.00001f ||
                    Vector3.Distance(sprite.transform.position-Vector3.up*offset,baseVisual)>.0001f;
            }
        }
    }
    public sealed class KeyJetAuditGrid : MonoBehaviour,IGridCellSizeProvider { public float Size=1; public float CellWorldSize=>Size; }
    public sealed class KeyJetAuditReceiver : MonoBehaviour,ICubeJetPushReceiver
    { public Vector3 Velocity; public void SetCubeJetVelocity(Component source,Vector3 velocity,float deltaTime) => Velocity=velocity; }
}

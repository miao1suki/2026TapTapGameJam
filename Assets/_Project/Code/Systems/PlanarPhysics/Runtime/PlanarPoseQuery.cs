using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Planar
{
    /// <summary>Native shape distance at hypothetical poses, without moving level objects or
    /// adding bodies to static walls. The query scene is never simulated and cannot emit game contacts.</summary>
    internal static class PlanarPoseQuery
    {
        private static Scene scene;
        private static Shape first, second;
#if UNITY_EDITOR
        static PlanarPoseQuery()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Reset;
            UnityEditor.EditorApplication.quitting += Reset;
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Reset();
            };
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
#if UNITY_EDITOR
                if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene))
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                else
#endif
                    SceneManager.UnloadSceneAsync(scene);
            }
            scene = default;
            first = second = null;
        }

        internal static ColliderDistance2D Distance(Collider2D a, Vector3 positionA, Quaternion rotationA,
            Collider2D b, Vector3 positionB, Quaternion rotationB)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
#if UNITY_EDITOR
                scene = !Application.isPlaying ? UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene() :
                    SceneManager.CreateScene("Planar pose queries", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
#else
                scene = SceneManager.CreateScene("Planar pose queries", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
#endif
                first = new Shape(scene);
                second = new Shape(scene);
            }
            Collider2D proxyA = first.Copy(a, positionA, rotationA);
            Collider2D proxyB = second.Copy(b, positionB, rotationB);
            Physics2D.SyncTransforms();
            return proxyA.Distance(proxyB);
        }

        private sealed class Shape
        {
            private readonly Transform transform;
            private readonly Rigidbody2D body;
            private readonly BoxCollider2D box;
            private readonly CircleCollider2D circle;
            private readonly CapsuleCollider2D capsule;
            private readonly PolygonCollider2D polygon;
            private Collider2D active;

            internal Shape(Scene owner)
            {
                var go = new GameObject("Native distance shape") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(go, owner);
                transform = go.transform;
                body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                box = go.AddComponent<BoxCollider2D>();
                circle = go.AddComponent<CircleCollider2D>();
                capsule = go.AddComponent<CapsuleCollider2D>();
                polygon = go.AddComponent<PolygonCollider2D>();
                box.enabled = circle.enabled = capsule.enabled = polygon.enabled = false;
            }

            internal Collider2D Copy(Collider2D source, Vector3 position, Quaternion rotation)
            {
                if (active) active.enabled = false;
                switch (source)
                {
                    case BoxCollider2D value:
                        box.size = value.size;
                        box.edgeRadius = value.edgeRadius;
                        active = box;
                        break;
                    case CircleCollider2D value:
                        circle.radius = value.radius;
                        active = circle;
                        break;
                    case CapsuleCollider2D value:
                        capsule.size = value.size;
                        capsule.direction = value.direction;
                        active = capsule;
                        break;
                    case PolygonCollider2D value:
                        polygon.pathCount = value.pathCount;
                        for (int i = 0; i < value.pathCount; i++) polygon.SetPath(i, value.GetPath(i));
                        active = polygon;
                        break;
                    default:
                        throw new System.NotSupportedException("Unsupported planar distance shape: " + source.GetType().Name);
                }
                Vector3 scale = source.transform.lossyScale;
                transform.localScale = new Vector3(scale.x, scale.y, 1f);
                body.position = position;
                body.rotation = rotation.eulerAngles.z;
                active.offset = source.offset;
                active.enabled = true;
                return active;
            }
        }
    }
}

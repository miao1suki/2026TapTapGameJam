using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TATools.StarPanorama
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("关卡美术/二维星空背景")]
    public sealed class StarPanoramaTimeDriver : MonoBehaviour
    {
        private static readonly int ShaderTimeId = Shader.PropertyToID("_StarPanoramaTime");
        [SerializeField, Tooltip("二维背景沿用原星空材质中的颜色、密度与流动参数。")]
        private Material backgroundMaterial;
        [SerializeField, Tooltip("启用后星空在游戏暂停时仍继续流转。")]
        private bool useUnscaledTime;
        private GameObject background;
        private MeshRenderer backgroundRenderer;
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Prepare;
            UpdateShaderTime();
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Prepare;
            if (background)
            {
                if (Application.isPlaying) Destroy(background); else DestroyImmediate(background);
            }
            background = null; backgroundRenderer = null;
        }
        private void Update() => UpdateShaderTime();
        private void Prepare(ScriptableRenderContext context, Camera camera)
        {
            if (camera && backgroundRenderer)
                backgroundRenderer.forceRenderingOff = !camera.orthographic;
            if (!backgroundMaterial || !camera || !camera.orthographic || camera.cameraType == CameraType.Preview ||
                camera.cameraType == CameraType.Reflection) return;
            if (camera.cameraType != CameraType.SceneView && camera.gameObject.scene != gameObject.scene) return;
            if (!background)
            {
                background = new GameObject("__二维星空背景") { hideFlags = HideFlags.HideAndDontSave };
                if (gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(background, gameObject.scene);
                background.AddComponent<MeshFilter>().sharedMesh = Resources.Load<Mesh>("Planar/UnitQuad");
                backgroundRenderer = background.AddComponent<MeshRenderer>();
                backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
                backgroundRenderer.receiveShadows = false;
                backgroundRenderer.lightProbeUsage = LightProbeUsage.Off;
                backgroundRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var layers = SortingLayer.layers;
                int value = int.MaxValue;
                foreach (var layer in layers)
                    if (layer.value < value) { value = layer.value; backgroundRenderer.sortingLayerID = layer.id; }
                backgroundRenderer.sortingOrder = short.MinValue;
            }
            backgroundRenderer.sharedMaterial = backgroundMaterial;
            float distance = Mathf.Lerp(camera.nearClipPlane, camera.farClipPlane, .95f);
            background.transform.SetPositionAndRotation(camera.transform.position + Vector3.forward * distance,
                Quaternion.Euler(0f, 0f, camera.transform.eulerAngles.z));
            background.transform.localScale = new Vector3(camera.orthographicSize * camera.aspect * 2.002f,
                camera.orthographicSize * 2.002f, 1f);
            UpdateShaderTime();
        }
        private void UpdateShaderTime()
            => Shader.SetGlobalFloat(ShaderTimeId, useUnscaledTime ? Time.unscaledTime : Time.time);
    }
}

using UnityEngine;
using Utils;

namespace InteractiveWater
{
    public class SimplePlanarReflection : MonoBehaviour
    {
        #region Fields
        
        [SerializeField] private LayerMask _reflectionMask;
        [SerializeField] private RenderTexture _reflectionTexture;
        [Range(0.1f,1f)] 
        [SerializeField] private float _reflectionResolutionScale = 0.5f;
        [SerializeField] private Vector3 _reflectionSurfaceNormal = Vector3.up;
        
        private Camera _reflectionCam;
        private Camera _mainCamera;
        private RenderTexture _runtimeReflectionTexture;

        public RenderTexture RuntimeTexture => _runtimeReflectionTexture;
        
        #endregion

        #region Lifecycle

        private void OnEnable()
        {
            if (Application.isPlaying) EnsureRuntimeTexture();
            SetupCamera();
            SetupRenderTexture();
        }
        
        private void OnDisable()
        {
            if (_runtimeReflectionTexture) _runtimeReflectionTexture.Release();
        }

        private void OnDestroy()
        {
            if (!_runtimeReflectionTexture) return;
            if (Application.isPlaying) Destroy(_runtimeReflectionTexture);
            else DestroyImmediate(_runtimeReflectionTexture);
        }
        
        private void LateUpdate()
        {
            RenderToTexture();
        }

        #endregion

        #region Public Methods

#if UNITY_EDITOR
        [CreateInspectorButton("Setup")]
        public void EditorSetup()
        {
            SetupCamera();
            SetupRenderTexture();
        }
#endif

        #endregion

        #region Private Methods

        private void RenderToTexture()
        {
            if (!_runtimeReflectionTexture) return;
            if (!_mainCamera || !_reflectionCam)
            {
                SetupCamera();
                SetupRenderTexture();
                if (!_mainCamera || !_reflectionCam) return;
            }
            
            //No need for reflections when we're underwater.
            if (_mainCamera.transform.position.y <= transform.position.y) return;

            // A side-view camera looks parallel to the horizontal water surface.
            // That plane cannot be used as an oblique near clip plane for this view.
            var surfaceNormal = _reflectionSurfaceNormal.normalized;
            if (Mathf.Abs(Vector3.Dot(_mainCamera.transform.forward, surfaceNormal)) < 0.01f) return;
            
            //Calculating the reflection camera position and rotation.
            var camPos = _mainCamera.transform.position;
            var dot = Vector3.Dot(surfaceNormal, camPos - transform.position);
            var reflectedPos = camPos - 2f * dot * surfaceNormal;
            var reflectedForward = Vector3.Reflect(_mainCamera.transform.forward, surfaceNormal);
            var reflectedUp = Vector3.Reflect(_mainCamera.transform.up, surfaceNormal);
            _reflectionCam.transform.position = reflectedPos;
            _reflectionCam.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);

            //Cut off the reflection camera projection matrix at the reflection surface.
            var viewSpace = _reflectionCam.worldToCameraMatrix;
            var pointViewSpace = viewSpace.MultiplyPoint(transform.position);
            var normalViewSpace = viewSpace.MultiplyVector(surfaceNormal).normalized;
            var planeViewSpace = new Vector4(
                normalViewSpace.x, 
                normalViewSpace.y, 
                normalViewSpace.z, 
                -Vector3.Dot(pointViewSpace, normalViewSpace));
            _reflectionCam.ResetProjectionMatrix();
            var projectionMatrix = _reflectionCam.CalculateObliqueMatrix(planeViewSpace);
            _reflectionCam.projectionMatrix = projectionMatrix;
            
            _reflectionCam.Render();
        }
        
        private void SetupCamera()
        {
            if (!_mainCamera) _mainCamera = Camera.main;
            if (!_mainCamera) return;
            
            if (transform.childCount % 2 == 1) //In my project, the Reflection Cam is either the first or the third child. Change this according to your project.
            {
                var go = transform.GetChild(transform.childCount > 2 ? 2 : 0);
                _reflectionCam = go.GetComponent<Camera>();
            }
            else
            {
                var go = new GameObject("ReflectionCam");
                go.transform.parent = transform;
                _reflectionCam = go.AddComponent<Camera>();
            }
            
            _reflectionCam.CopyFrom(_mainCamera);
            _reflectionCam.enabled = false;
            _reflectionCam.cullingMask = _reflectionMask;
            _reflectionCam.clearFlags = _mainCamera.clearFlags;
            _reflectionCam.backgroundColor = _mainCamera.backgroundColor;
        }
        
        private void SetupRenderTexture()
        {
            if (!_mainCamera || !_reflectionCam || !_runtimeReflectionTexture) return;

            var w = Mathf.Max(1, Mathf.RoundToInt(_mainCamera.pixelWidth * _reflectionResolutionScale));
            var h = Mathf.Max(1, Mathf.RoundToInt(_mainCamera.pixelHeight * _reflectionResolutionScale));
            
            _runtimeReflectionTexture.Release();
            _runtimeReflectionTexture.width = w;
            _runtimeReflectionTexture.height = h;
            
            _reflectionCam.targetTexture = _runtimeReflectionTexture;
        }

        private void EnsureRuntimeTexture()
        {
            if (_runtimeReflectionTexture || !_reflectionTexture) return;
            _runtimeReflectionTexture = Instantiate(_reflectionTexture);
            _runtimeReflectionTexture.name = $"{name} Planar Reflection";
        }

        #endregion
    }
}

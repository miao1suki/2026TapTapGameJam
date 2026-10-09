using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;
using Random = UnityEngine.Random;

namespace InteractiveWater
{
    //You can change this to a singleton for ease of access, I use MonoBehavior to keep this project simple.
    public class InteractiveWater : MonoBehaviour
    {
        #region Structs
        
        private struct Ripple
        {
            public Ripple(Vector2 center, float initialStrength, bool initialRippleUp)
            {
                CenterInUVSpace = center;
                InitialStrength = initialStrength;
                InitialRippleUp = initialRippleUp;
            }

            public Vector2 CenterInUVSpace;
            public float InitialStrength;
            public bool InitialRippleUp;
        }
        
        #endregion
        
        #region Fields

        private static readonly int WaterMeshDepth = Shader.PropertyToID("_WaterMeshDepth");
        private static readonly int ImpactHeight = Shader.PropertyToID("_ImpactHeight");
        private static readonly int AmbientWaveTexture = Shader.PropertyToID("_AmbientWaveTexture");
        private static readonly int RippleSimulationTexture = Shader.PropertyToID("_RippleSimulationTexture");
        private static readonly int PlanarReflectionTexture = Shader.PropertyToID("_PlanarReflectionTexture");
        private static readonly int MainColor = Shader.PropertyToID("_MainColor");
        private const string WaterTopMeshName = "WaterTopMesh";
        private const string WaterFrontMeshName = "WaterFrontMesh";

        [Header("Mesh Settings")] 
        [Tooltip("The x and z size of the top water mesh.")]
        [SerializeField] private Vector2 _topSurfaceSize = new (20, 6.5f);
        [Tooltip("The y size of the front water mesh, the x size is defined in Top Surface Size.")]
        [SerializeField] private float _frontSurfaceHeight = 10f;
        [Tooltip("The x and z vertex count of the top water mesh, ideally this should be a multiple of the size.")]
        [SerializeField] private Vector2Int _topSurfaceVerticesCount = new(200, 130);

        /// <summary>World dimensions of the top surface in its local X/Z plane.</summary>
        public Vector2 TopSurfaceSize => _topSurfaceSize;

        [Header("Textures")]
        [SerializeField] private CustomRenderTexture _ambientWaveTexture;
        [SerializeField] private CustomRenderTexture _rippleSimulationTexture;
        [SerializeField] private RenderTexture _reflectionTexture;
        
        [Header("Materials")]
        [SerializeField] private Material _topMeshMaterial;
        [SerializeField] private Material _frontMeshMaterial;
        [SerializeField] private Material _ambientWaveMaterial;
        [SerializeField] private Material _rippleSimulationMaterial;
        
        [Header("Other Settings")] 
        [SortingLayerDropdown]
        [SerializeField] private int _topMeshSortingLayer;
        [SortingLayerDropdown]
        [SerializeField] private int _frontMeshSortingLayer;
        [Tooltip("How many times the texture updates in one update call.")]
        [Range(1, 8)]
        [SerializeField] private int _rippleSimulationIterationPerFrame = 5;
        [Tooltip("Allow clicking on the surface to see ripples.")]
        [SerializeField] private bool _testRipplesWithMouse;
        [SerializeField] private LayerMask _testWaterLayer;
        
        private Transform _topMesh;
        private Transform _frontMesh;
        private readonly Queue<Ripple> _queuedRipples = new();
        private static readonly Queue<Ripple> sharedQueuedRipples = new();
        private Camera _testRippleCamera;
        private CustomRenderTexture _runtimeAmbientWaveTexture;
        private CustomRenderTexture _runtimeRippleSimulationTexture;
        private Material _runtimeTopMeshMaterial;
        private Material _runtimeFrontMeshMaterial;
        private Material _runtimeAmbientWaveMaterial;
        private Material _runtimeRippleSimulationMaterial;
        private Mesh _runtimeTopMesh;
        private Mesh _runtimeFrontMesh;
        private bool _countedActive;
        private bool _useSharedSimulation;
        private bool _registeredSharedSimulation;
        private float _reveal = 1f;
        private int _presentationLayer = -1;
        private static CustomRenderTexture sharedAmbient;
        private static CustomRenderTexture sharedRipple;
        private static Material sharedAmbientMaterial;
        private static Material sharedRippleMaterial;
        private static int sharedUsers;
        private static InteractiveWater sharedUpdateOwner;

        public void ConfigureBlockVolume(Vector2 surfaceSize, float height)
        {
            _topSurfaceSize = new Vector2(Mathf.Max(.1f, surfaceSize.x), Mathf.Max(.1f, surfaceSize.y));
            _frontSurfaceHeight = Mathf.Max(.1f, height);
            _topSurfaceVerticesCount = new Vector2Int(
                Mathf.Clamp(Mathf.CeilToInt(_topSurfaceSize.x * 8f) + 1, 2, 65),
                Mathf.Clamp(Mathf.CeilToInt(_topSurfaceSize.y * 8f) + 1, 2, 33));
            _useSharedSimulation = true;
            _testRipplesWithMouse = false;
            SimplePlanarReflection reflection = GetComponent<SimplePlanarReflection>();
            if (reflection != null) reflection.enabled = false;
        }

        /// <summary>Reveal the actual water surfaces without changing the source material.</summary>
        public void SetReveal(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (Mathf.Approximately(_reveal, progress)) return;
            _reveal = progress;
            ApplyReveal();
        }

        public void SetPresentationLayer(int layer)
        {
            if (layer < 0 || layer > 31) return;
            _presentationLayer = layer;
            gameObject.layer = layer;
            if (_topMesh != null) _topMesh.gameObject.layer = layer;
            if (_frontMesh != null) _frontMesh.gameObject.layer = layer;
            if (_topMesh != null) _topMesh.GetComponent<Renderer>().renderingLayerMask |= 128u;
            if (_frontMesh != null) _frontMesh.GetComponent<Renderer>().renderingLayerMask |= 128u;
        }

        private void ApplyReveal()
        {
            if (_runtimeFrontMeshMaterial != null)
            {
                Color color = _frontMeshMaterial.GetColor(MainColor);
                color.a *= _reveal;
                _runtimeFrontMeshMaterial.SetColor(MainColor, color);
            }
            if (_topMesh != null)
                _topMesh.localScale = Vector3.one * Mathf.Max(.0001f, _reveal);
        }

        public static int ActiveInstanceCount { get; private set; }
        
        #endregion

        #region Life Cycle

        private void OnEnable()
        {
            if (!Application.isPlaying || _countedActive) return;
            ActiveInstanceCount++;
            _countedActive = true;
        }

        private void OnDisable()
        {
            UnregisterActiveInstance();
        }

        private void UnregisterActiveInstance()
        {
            if (!_countedActive) return;
            ActiveInstanceCount = Mathf.Max(0, ActiveInstanceCount - 1);
            _countedActive = false;
        }

        private void Start()
        {
            Init();
        }

        private void FixedUpdate()
        {
            if (!_runtimeRippleSimulationTexture) return;
            if (_registeredSharedSimulation)
            {
                if (sharedUpdateOwner == null) sharedUpdateOwner = this;
                if (sharedUpdateOwner != this) return;
            }
            _runtimeRippleSimulationTexture.ClearUpdateZones();
            UpdateNewRipples();
            _runtimeRippleSimulationTexture.Update(_rippleSimulationIterationPerFrame);
        }

        private void OnDestroy()
        {
            UnregisterActiveInstance();
            if (_registeredSharedSimulation)
            {
                if (sharedUpdateOwner == this) sharedUpdateOwner = null;
                sharedUsers = Mathf.Max(0, sharedUsers - 1);
                if (sharedUsers == 0)
                {
                    if (sharedAmbient) sharedAmbient.Release();
                    if (sharedRipple) sharedRipple.Release();
                    DestroyRuntimeObject(sharedAmbient);
                    DestroyRuntimeObject(sharedRipple);
                    DestroyRuntimeObject(sharedAmbientMaterial);
                    DestroyRuntimeObject(sharedRippleMaterial);
                    sharedAmbient = sharedRipple = null;
                    sharedAmbientMaterial = sharedRippleMaterial = null;
                    sharedQueuedRipples.Clear();
                }
            }
            else
            {
            if (_runtimeAmbientWaveTexture) _runtimeAmbientWaveTexture.Release();
            if (_runtimeRippleSimulationTexture) _runtimeRippleSimulationTexture.Release();
            DestroyRuntimeObject(_runtimeAmbientWaveTexture);
            DestroyRuntimeObject(_runtimeRippleSimulationTexture);
            DestroyRuntimeObject(_runtimeAmbientWaveMaterial);
            DestroyRuntimeObject(_runtimeRippleSimulationMaterial);
            }
            DestroyRuntimeObject(_runtimeTopMeshMaterial);
            DestroyRuntimeObject(_runtimeFrontMeshMaterial);
            DestroyRuntimeObject(_runtimeTopMesh);
            DestroyRuntimeObject(_runtimeFrontMesh);
        }

        private static void DestroyRuntimeObject(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void Update()
        {
            if (!_testRipplesWithMouse) return;
            CreateRipplesFromMouseInput();
        }

        #endregion
        
        #region Public Methods

        #if UNITY_EDITOR
        [CreateInspectorButton("Generate Mesh")]
        public void EditorGeneratePreviewMesh()
        {
            Init();
        }
        #endif
        
        /// <summary>
        /// Creates a ripple on the water surface.
        /// </summary>
        /// <param name="worldPosition">The center of the ripple.</param>
        /// <param name="initialStrength">How strong the initial ripple is, from 0f to 1f.</param>
        /// <param name="initialUp">Set to true if the initial ripple should point upwards, false otherwise.</param>
        public void CreateContactRippleAt(Vector3 worldPosition, float initialStrength, bool initialUp = true)
        {
            if (!_topMesh) _topMesh = transform.Find(WaterTopMeshName);
            if (!_topMesh) return;
            var localSpace = _topMesh.InverseTransformPoint(worldPosition);
            var uvSpace = new Vector2(localSpace.x / _topSurfaceSize.x, 1 - localSpace.z / _topSurfaceSize.y);
            
            (_useSharedSimulation ? sharedQueuedRipples : _queuedRipples)
                .Enqueue(new Ripple(uvSpace, initialStrength, initialUp));
        }

        #endregion

        #region Private Methods
        
        private void CreateRipplesFromMouseInput()
        {
            var left = Input.GetMouseButtonDown(0);
            var right = Input.GetMouseButtonDown(1);
            
            if (!left && !right) return;
            
            if (!_testRippleCamera) _testRippleCamera = Camera.main;
            if (!_testRippleCamera) return;
            
            var ray = _testRippleCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, _testWaterLayer)) return;
            
            var point = hit.point;
            var strength = Random.Range(0.2f, 0.6f);

            CreateContactRippleAt(point, strength, left);
        }
        
        private void UpdateNewRipples()
        {
            Queue<Ripple> queue = _useSharedSimulation ? sharedQueuedRipples : _queuedRipples;
            if (queue.Count <= 0) return;

            var ripple = queue.Dequeue();
                
            _runtimeRippleSimulationMaterial.SetFloat(ImpactHeight, ripple.InitialStrength / 5);

            var defaultZone = new CustomRenderTextureUpdateZone
            {
                needSwap = true,
                passIndex = 0,
                rotation = 0,
                updateZoneCenter = new Vector3(0.5f, 0.5f),
                updateZoneSize = new Vector3(1f, 1f)
            };

            var impactZone = new CustomRenderTextureUpdateZone
            {
                needSwap = true,
                passIndex = ripple.InitialRippleUp ? 1 : 2,
                rotation = 0,
                updateZoneCenter = new Vector3(ripple.CenterInUVSpace.x, ripple.CenterInUVSpace.y),
                updateZoneSize = new Vector3(0.01f, 0.01f)
            };
                
            _runtimeRippleSimulationTexture.SetUpdateZones(new [] { defaultZone , impactZone});
        }
        
        private void Init()
        {
            if (Application.isPlaying) CreateRuntimeResources();
            SetShaderParams();
            GenerateWaterMeshes();
            ApplyReveal();
            if (Application.isPlaying && (!_useSharedSimulation || sharedUsers == 1))
            {
                _runtimeAmbientWaveTexture.Initialize();
                _runtimeRippleSimulationTexture.Initialize();
            }
            
            //For testing using mouse input
            if (!_testRippleCamera) _testRippleCamera = Camera.main;
        }
        
        private void SetShaderParams()
        {
            if (!Application.isPlaying) return;
            _runtimeTopMeshMaterial.SetFloat(WaterMeshDepth, _topSurfaceSize.y);
            _runtimeFrontMeshMaterial.SetFloat(WaterMeshDepth, _topSurfaceSize.y);
        }

        private void CreateRuntimeResources()
        {
            if (_runtimeRippleSimulationTexture) return;
            _runtimeTopMeshMaterial = new Material(_topMeshMaterial) { name = $"{name} Top Water" };
            _runtimeFrontMeshMaterial = new Material(_frontMeshMaterial) { name = $"{name} Front Water" };
            if (_useSharedSimulation)
            {
                if (sharedAmbient == null || sharedRipple == null)
                {
                    sharedAmbientMaterial = new Material(_ambientWaveMaterial);
                    sharedRippleMaterial = new Material(_rippleSimulationMaterial);
                    sharedAmbient = Instantiate(_ambientWaveTexture);
                    sharedRipple = Instantiate(_rippleSimulationTexture);
                    sharedAmbient.width = sharedAmbient.height = 256;
                    sharedRipple.width = sharedRipple.height = 256;
                    sharedAmbient.material = sharedAmbientMaterial;
                    sharedRipple.material = sharedRippleMaterial;
                }
                sharedUsers++;
                _registeredSharedSimulation = true;
                _runtimeAmbientWaveTexture = sharedAmbient;
                _runtimeRippleSimulationTexture = sharedRipple;
                _runtimeRippleSimulationMaterial = sharedRippleMaterial;
            }
            else
            {
                _runtimeAmbientWaveMaterial = new Material(_ambientWaveMaterial) { name = $"{name} Ambient Waves" };
                _runtimeRippleSimulationMaterial = new Material(_rippleSimulationMaterial) { name = $"{name} Ripple Simulation" };
                _runtimeAmbientWaveTexture = Instantiate(_ambientWaveTexture);
                _runtimeAmbientWaveTexture.name = $"{name} Ambient Waves";
                _runtimeAmbientWaveTexture.material = _runtimeAmbientWaveMaterial;
                _runtimeRippleSimulationTexture = Instantiate(_rippleSimulationTexture);
                _runtimeRippleSimulationTexture.name = $"{name} Ripple Simulation";
                _runtimeRippleSimulationTexture.material = _runtimeRippleSimulationMaterial;
            }

            _runtimeTopMeshMaterial.SetTexture(AmbientWaveTexture, _runtimeAmbientWaveTexture);
            _runtimeTopMeshMaterial.SetTexture(RippleSimulationTexture, _runtimeRippleSimulationTexture);
            _runtimeFrontMeshMaterial.SetTexture(AmbientWaveTexture, _runtimeAmbientWaveTexture);
            _runtimeFrontMeshMaterial.SetTexture(RippleSimulationTexture, _runtimeRippleSimulationTexture);

            var reflection = GetComponent<SimplePlanarReflection>();
            if (reflection && reflection.RuntimeTexture)
                _runtimeTopMeshMaterial.SetTexture(PlanarReflectionTexture, reflection.RuntimeTexture);
        }
        
        private void GenerateWaterMeshes()
        {
            MeshFilter topMF, frontMF;
            MeshRenderer topMR, frontMR;
            SortingGroup topSG, frontSG;
            
            if (transform.childCount >= 2) //The 3rd child is the camera for planar reflection 
            {
                _topMesh = transform.GetChild(0);
                _frontMesh = transform.GetChild(1);
                
                topMF = _topMesh.GetComponent<MeshFilter>();
                topMR = _topMesh.GetComponent<MeshRenderer>();
                topSG = _topMesh.GetComponent<SortingGroup>();
                
                frontMF = _frontMesh.GetComponent<MeshFilter>();
                frontMR = _frontMesh.GetComponent<MeshRenderer>();
                frontSG = _frontMesh.GetComponent<SortingGroup>();
                
                if (_testRipplesWithMouse && !_topMesh.TryGetComponent(out BoxCollider _))
                {
                    _topMesh.gameObject.AddComponent<BoxCollider>();
                }
            }
            else
            {
                foreach (Transform child in transform)
                {
                    DestroyImmediate(child.gameObject);
                }

                _topMesh = new GameObject(WaterTopMeshName).transform;
                _frontMesh = new GameObject(WaterFrontMeshName).transform;
                
                _topMesh.transform.parent = transform;
                _topMesh.transform.localPosition = Vector3.zero;
                
                _frontMesh.transform.parent = transform;
                _frontMesh.transform.localPosition = Vector3.zero;

                topMF = _topMesh.gameObject.AddComponent<MeshFilter>();
                topMR = _topMesh.gameObject.AddComponent<MeshRenderer>();
                topSG = _topMesh.gameObject.AddComponent<SortingGroup>();
                
                frontMF = _frontMesh.gameObject.AddComponent<MeshFilter>();
                frontMR = _frontMesh.gameObject.AddComponent<MeshRenderer>();
                frontSG = _frontMesh.gameObject.AddComponent<SortingGroup>();

                if (_testRipplesWithMouse && !_topMesh.TryGetComponent(out BoxCollider _))
                {
                    _topMesh.gameObject.AddComponent<BoxCollider>();
                }
            }

            if (!transform.TryGetComponent(out BoxCollider2D bc2d))
            {
                bc2d = transform.gameObject.AddComponent<BoxCollider2D>();
                bc2d.isTrigger = true;
                bc2d.size = new Vector2(_topSurfaceSize.x, 0.02f);
                bc2d.offset = new Vector2(_topSurfaceSize.x / 2, 0);
            }            
            
            _topMesh.gameObject.layer = _presentationLayer >= 0 ? _presentationLayer
                : Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(1, _testWaterLayer.value), 2)), 0, 31);
            _frontMesh.gameObject.layer = _topMesh.gameObject.layer;
            if (_presentationLayer >= 0) SetPresentationLayer(_presentationLayer);
            topMR.sharedMaterial = Application.isPlaying ? _runtimeTopMeshMaterial : _topMeshMaterial;
            topMR.sortingOrder = 0;
            topSG.sortingLayerID = _topMeshSortingLayer;
            GenerateTopMesh(topMF);
            
            frontMR.sharedMaterial = Application.isPlaying ? _runtimeFrontMeshMaterial : _frontMeshMaterial;
            frontMR.sortingOrder = 0;
            frontSG.sortingLayerID = _frontMeshSortingLayer;
            GenerateFrontMesh(frontMF);
        }

        private void GenerateTopMesh(MeshFilter mf)
        {
            var mesh = new Mesh { name = WaterTopMeshName };

            var vertices = new Vector3[_topSurfaceVerticesCount.x * _topSurfaceVerticesCount.y];
            var triangles = new int[(_topSurfaceVerticesCount.x - 1) * (_topSurfaceVerticesCount.y - 1) * 6];
            var uv = new Vector2[vertices.Length];

            var dx = _topSurfaceSize.x / (_topSurfaceVerticesCount.x - 1);
            var dz = _topSurfaceSize.y / (_topSurfaceVerticesCount.y - 1);

            for (var z = 0; z < _topSurfaceVerticesCount.y; z++)
            {
                for (var x = 0; x < _topSurfaceVerticesCount.x; x++)
                {
                    var i = x + z * _topSurfaceVerticesCount.x;
                    vertices[i] = new Vector3(x * dx, 0f, z * dz);
                    uv[i] = new Vector2((float)x / (_topSurfaceVerticesCount.x - 1), (float)z / (_topSurfaceVerticesCount.y - 1));
                }
            }

            var triIndex = 0;
            for (var z = 0; z < _topSurfaceVerticesCount.y - 1; z++)
            {
                for (var x = 0; x < _topSurfaceVerticesCount.x - 1; x++)
                {
                    var i = x + z * _topSurfaceVerticesCount.x;

                    triangles[triIndex++] = i;
                    triangles[triIndex++] = i + _topSurfaceVerticesCount.x;
                    triangles[triIndex++] = i + 1;

                    triangles[triIndex++] = i + 1;
                    triangles[triIndex++] = i + _topSurfaceVerticesCount.x;
                    triangles[triIndex++] = i + _topSurfaceVerticesCount.x + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (Application.isPlaying && _runtimeTopMesh) Destroy(_runtimeTopMesh);
            mf.sharedMesh = mesh;
            if (Application.isPlaying) _runtimeTopMesh = mesh;
        }
        
        private void GenerateFrontMesh(MeshFilter mf)
        {
            var mesh = new Mesh { name = WaterFrontMeshName };

            var vertices = new Vector3[_topSurfaceVerticesCount.x * 2];
            var triangles = new int[(_topSurfaceVerticesCount.x - 1) * 6];
            var uv = new Vector2[vertices.Length];

            var dx = _topSurfaceSize.x / (_topSurfaceVerticesCount.x - 1);

            for (var x = 0; x < _topSurfaceVerticesCount.x; x++)
            {
                vertices[x] = new Vector3(x * dx, 0f, 0f);
                uv[x] = new Vector2((float)x / (_topSurfaceVerticesCount.x - 1), 1f);
                
                var i = x + _topSurfaceVerticesCount.x;
                vertices[i] = new Vector3(x * dx, -_frontSurfaceHeight, 0f);
                uv[i] = new Vector2((float)x / (_topSurfaceVerticesCount.x - 1), 0f);
            }

            var triIndex = 0;
            for (var x = 0; x < _topSurfaceVerticesCount.x - 1; x++)
            {
                var topA = x;
                var topB = x + 1;
                var botA = x + _topSurfaceVerticesCount.x;
                var botB = x + _topSurfaceVerticesCount.x + 1;

                triangles[triIndex++] = topB;
                triangles[triIndex++] = botA;
                triangles[triIndex++] = topA;

                triangles[triIndex++] = botB;
                triangles[triIndex++] = botA;
                triangles[triIndex++] = topB;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (Application.isPlaying && _runtimeFrontMesh) Destroy(_runtimeFrontMesh);
            mf.sharedMesh = mesh;
            if (Application.isPlaying) _runtimeFrontMesh = mesh;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var isInitialRippleUp = Random.value > 0.5f;
            if (other.TryGetComponent(out Rigidbody2D rb))
            {
                isInitialRippleUp = rb.linearVelocity.y > 0;
            }
            CreateContactRippleAt(other.transform.position, Random.Range(0.2f, 0.6f), isInitialRippleUp);
        }

        /// <summary>
        /// Creates a splash when a 3D collider enters a 3D trigger placed at the water surface.
        /// The 3D trigger is separate from the 2D trigger and the top mesh collider used by
        /// the mouse-raycast test.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other.transform.IsChildOf(transform)) return;

            var isInitialRippleUp = Random.value > 0.5f;
            if (other.attachedRigidbody != null)
            {
                isInitialRippleUp = other.attachedRigidbody.linearVelocity.y > 0f;
            }
            else if (other.GetComponentInParent<CharacterController>() is { } characterController)
            {
                isInitialRippleUp = characterController.velocity.y > 0f;
            }

            // Project the 3D collider's center onto the top-water plane. CreateContactRippleAt
            // uses the local X/Z coordinates to find the matching simulation-texture UV.
            var localPoint = _topMesh.InverseTransformPoint(other.bounds.center);
            localPoint.y = 0f;
            var surfacePoint = _topMesh.TransformPoint(localPoint);

            CreateContactRippleAt(surfacePoint, Random.Range(0.2f, 0.6f), isInitialRippleUp);
        }

        #endregion
    }
}

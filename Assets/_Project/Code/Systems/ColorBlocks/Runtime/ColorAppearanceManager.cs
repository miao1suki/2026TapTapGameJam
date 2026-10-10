using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>Central appearance choreography. Runtime material copies are pooled per source/color/phase.</summary>
    [DefaultExecutionOrder(-950)]
    public sealed class ColorAppearanceManager : MonoBehaviour
    {
        private sealed class Sequence
        {
            public float progress, target, speed;
            public bool real;
        }
        private sealed class VineTarget
        {
            public string id;
            public Material[] originals;
        }
        private static ColorAppearanceManager instance;
        [SerializeField] private Shader vineRevealShader;
        private readonly Dictionary<string, Sequence> sequences = new Dictionary<string, Sequence>();
        private readonly Dictionary<(Material source, string id, bool real), Material> sharedMaterials =
            new Dictionary<(Material, string, bool), Material>();
        private readonly Dictionary<InteractiveWater.InteractiveWater, string> waters =
            new Dictionary<InteractiveWater.InteractiveWater, string>();
        private readonly Dictionary<Renderer, VineTarget> vines = new Dictionary<Renderer, VineTarget>();
        private readonly List<string> changed = new List<string>();
        private const float Handoff = .3f;
        private Vector4 baseAlpha = Vector4.one, realAlpha = Vector4.zero;
        public static int GetGroupIndex(string id) => id == "red" ? 0 : id == "green" ? 1 : id == "blue" ? 2 : -1;
        public event Action<string> AppearanceChanged;
        public int SharedMaterialCount => sharedMaterials.Count;
        public int WaterCount => waters.Count;
        public int VineCount => vines.Count;
        public static ColorAppearanceManager Existing => instance;
        public static ColorAppearanceManager Instance
        {
            get
            {
                if (instance != null) return instance;
                var existing = ProjectDiscovery.FindFirst<ColorAppearanceManager>();
                if (existing != null) return existing;
                return new GameObject("Color Appearance Manager").AddComponent<ColorAppearanceManager>();
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        public bool ShowsUnlockedAppearance(string id) => sequences.TryGetValue(id, out var s) && s.real;
        public float GetBaseOpacity(string id) => sequences.TryGetValue(id, out var s)
            ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s.progress / Handoff)) : 1f;
        public float GetRealOpacity(string id) => sequences.TryGetValue(id, out var s)
            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Handoff, 1f, s.progress)) : 0f;
        public bool IsComplete(string id) => !sequences.TryGetValue(id, out var s) || Mathf.Approximately(s.progress, s.target);
        public float GetEffectiveDuration(float duration = -1f) => HSVColorFadeManager.Instance.GetEffectiveDuration(duration);

        public void Play(string id, bool unlocked, float duration = -1f)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!sequences.TryGetValue(id, out var s)) { s = new Sequence(); sequences.Add(id, s); }
            s.target = unlocked ? 1f : 0f;
            float seconds = GetEffectiveDuration(duration);
            if (seconds <= 0f) { s.progress = s.target; s.real = unlocked; }
            else s.speed = 1f / seconds;
            ApplyHsv(id, s);
            ApplyMaskGlobals(id);
            ApplyMaterials(id);
            ApplyWater(id);
            ApplyVines(id);
            AppearanceChanged?.Invoke(id);
        }
        public void ResetAll()
        {
            sequences.Clear();
            baseAlpha = Vector4.one;
            realAlpha = Vector4.zero;
            Shader.SetGlobalVector("_ColorAppearanceBaseAlpha", baseAlpha);
            Shader.SetGlobalVector("_ColorAppearanceRealAlpha", realAlpha);
            HSVColorFadeManager.Instance.ResetAll();
            foreach (var key in sharedMaterials.Keys) UpdateMaterial(key, sharedMaterials[key]);
            foreach (var water in waters.Keys) if (water != null) water.SetReveal(0f);
            foreach (var renderer in vines.Keys) if (renderer != null) renderer.enabled = false;
            AppearanceChanged?.Invoke(null);
        }
        private void Update()
        {
            changed.Clear();
            foreach (var pair in sequences)
            {
                var s = pair.Value;
                if (Mathf.Approximately(s.progress, s.target)) continue;
                bool oldReal = s.real;
                // Clamp at the crossing so both appearances have a guaranteed zero-alpha handoff.
                float next = Mathf.MoveTowards(s.progress, s.target, s.speed * Time.unscaledDeltaTime);
                if (s.progress < Handoff && next >= Handoff) next = Handoff;
                if (s.progress > Handoff && next <= Handoff) next = Handoff;
                s.progress = next;
                s.real = s.progress >= Handoff && s.target > 0f || s.progress > Handoff;
                ApplyHsv(pair.Key, s);
                ApplyMaskGlobals(pair.Key);
                ApplyMaterials(pair.Key);
                ApplyWater(pair.Key);
                ApplyVines(pair.Key);
                if (s.real != oldReal) changed.Add(pair.Key);
            }
            foreach (string id in changed) AppearanceChanged?.Invoke(id);
        }
        private static void ApplyHsv(string id, Sequence s)
        {
            float saturation = Mathf.InverseLerp(Handoff, 1f, s.progress);
            float white = s.progress < Handoff ? s.progress / Handoff : 1f - saturation;
            HSVColorFadeManager.Instance.SetHsv(id, saturation, white);
        }
        private void ApplyMaskGlobals(string id)
        {
            int group = GetGroupIndex(id);
            if (group < 0) return;
            baseAlpha[group] = GetBaseOpacity(id);
            realAlpha[group] = GetRealOpacity(id);
            Shader.SetGlobalVector("_ColorAppearanceBaseAlpha", baseAlpha);
            Shader.SetGlobalVector("_ColorAppearanceRealAlpha", realAlpha);
        }
        public Material GetSharedMaterial(Material source, string id, bool real)
        {
            if (source == null) return null;
            var key = (source, id, real);
            if (!sharedMaterials.TryGetValue(key, out var material))
            {
                material = new Material(source) { name = source.name + " (Color Appearance)", hideFlags = HideFlags.DontSave };
                sharedMaterials.Add(key, material);
            }
            UpdateMaterial(key, material);
            return material;
        }
        private void ApplyMaterials(string id)
        {
            foreach (var pair in sharedMaterials) if (pair.Key.id == id) UpdateMaterial(pair.Key, pair.Value);
        }
        private void UpdateMaterial((Material source, string id, bool real) key, Material material)
        {
            float alpha = key.real ? GetRealOpacity(key.id) : GetBaseOpacity(key.id);
            if (material.HasProperty("_ColorRevealOpacity")) material.SetFloat("_ColorRevealOpacity", alpha);
            // Particle shaders differ; pool their original tint times reveal alpha instead of copying per Renderer.
            if (!material.HasProperty("_ColorRevealOpacity"))
                foreach (string property in TintProperties)
                    if (key.source.HasProperty(property))
                    {
                        Color color = key.source.GetColor(property);
                        color.a *= alpha;
                        material.SetColor(property, color);
                    }
        }
        private static readonly string[] TintProperties = { "_TintColor", "_Color", "_BaseColor" };
        public void RegisterWater(InteractiveWater.InteractiveWater water, string id = "blue")
        {
            if (water == null) return;
            waters[water] = id;
            ApplyHsvForRegistration(id);
            water.SetReveal(GetRealOpacity(id));
        }
        public void UnregisterWater(InteractiveWater.InteractiveWater water)
        {
            if (!ReferenceEquals(water, null)) waters.Remove(water);
        }
        private void ApplyWater(string id)
        {
            float alpha = GetRealOpacity(id);
            foreach (var pair in waters) if (pair.Key != null && pair.Value == id) pair.Key.SetReveal(alpha);
        }
        public void RegisterVine(Renderer renderer, string id = "green")
        {
            if (renderer == null || vines.ContainsKey(renderer)) return;
            renderer.enabled = false;
            var originals = renderer.sharedMaterials;
            var materials = new Material[originals.Length];
            var properties = new MaterialPropertyBlock();
            for (int i = 0; i < originals.Length; i++)
            {
                materials[i] = GetSharedMaterial(originals[i], id, true);
                if (materials[i] != null)
                {
                    if (vineRevealShader == null) vineRevealShader = Resources.Load<Material>("ColorBlocks/VineReveal")?.shader;
                    Shader revealShader = vineRevealShader;
                    if (revealShader == null) Debug.LogError("缺少藤蔓透明表现 Shader。", this);
                    else
                    {
                        materials[i].shader = revealShader;
                        materials[i].renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        Texture sourceTexture = originals[i].HasProperty("_MainTex") ? originals[i].GetTexture("_MainTex") : originals[i].mainTexture;
                        materials[i].mainTexture = sourceTexture;
                        Color tint = originals[i].HasProperty("_TintColor") ? originals[i].GetColor("_TintColor")
                            : originals[i].HasProperty("_BaseColor") ? originals[i].GetColor("_BaseColor")
                            : originals[i].HasProperty("_Color") ? originals[i].GetColor("_Color") : Color.white;
                        materials[i].SetColor("_TintColor", tint);
                        UpdateMaterial((originals[i], id, true), materials[i]);
                    }
                }
                renderer.GetPropertyBlock(properties, i);
                // Fresh namespace-only mask data must not override the shared material's alpha.
                properties.Clear();
                properties.SetFloat("_ColorMaskUseVertexAlpha", 1f);
                properties.SetFloat("_ColorMaskGroup", GetGroupIndex(id));
                properties.SetFloat("_ColorMaskReal", 1f);
                var texture = originals[i] != null && originals[i].HasProperty("_MainTex") ? originals[i].GetTexture("_MainTex") : null;
                properties.SetTexture("_ColorMaskAlphaTexture", texture != null ? texture : Texture2D.whiteTexture);
                renderer.SetPropertyBlock(properties, i);
            }
            renderer.sharedMaterials = materials;
            vines.Add(renderer, new VineTarget { id = id, originals = originals });
            ApplyHsvForRegistration(id);
            renderer.enabled = GetRealOpacity(id) > .001f;
        }
        public void UnregisterVine(Renderer renderer)
        {
            if (ReferenceEquals(renderer, null) || !vines.TryGetValue(renderer, out var target)) return;
            if (renderer != null)
            {
                renderer.enabled = false;
                renderer.sharedMaterials = target.originals;
            }
            vines.Remove(renderer);
        }
        private void ApplyVines(string id)
        {
            bool visible = GetRealOpacity(id) > .001f;
            foreach (var pair in vines)
                if (pair.Key != null && pair.Value.id == id && pair.Key.enabled != visible) pair.Key.enabled = visible;
        }
        private void ApplyHsvForRegistration(string id)
        {
            ApplyMaskGlobals(id);
            if (sequences.TryGetValue(id, out var s)) ApplyHsv(id, s);
            else HSVColorFadeManager.Instance.SetHsv(id, 0f, 0f);
        }
        private void OnDestroy()
        {
            foreach (var material in sharedMaterials.Values) if (material != null) Destroy(material);
            if (instance == this) instance = null;
        }
    }
}

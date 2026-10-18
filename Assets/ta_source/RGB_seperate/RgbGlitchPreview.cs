using System.Collections.Generic;
using UnityEngine;

namespace TapTap.TA.RgbGlitch
{
    public sealed class RgbGlitchPreview : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float fearLevel = 0.45f;
        [SerializeField] private bool automaticCycle = true;

        private static readonly Color[] Colors =
        {
            new Color(0.95f, 0.12f, 0.23f),
            new Color(0.12f, 0.8f, 0.95f),
            new Color(0.24f, 0.95f, 0.38f),
            new Color(0.95f, 0.77f, 0.16f)
        };

        private float lastFear = -1f;
        private float cycleTime;
        private int cycleStep;
        private readonly List<Material> materials = new List<Material>();

        private void Start()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return;
            for (int index = 0; index < 4; index++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Color bar " + (index + 1);
                bar.transform.SetParent(transform, false);
                bar.transform.localPosition = new Vector3(-6f + index * 4f, 0f, 0f);
                bar.transform.localScale = new Vector3(3.8f, 9f, 0.2f);
                var material = new Material(unlit);
                material.SetColor("_BaseColor", Colors[index]);
                bar.GetComponent<Renderer>().material = material;
                materials.Add(material);
                Destroy(bar.GetComponent<Collider>());
            }
        }

        private void Update()
        {
            if (!Mathf.Approximately(fearLevel, lastFear))
            {
                RgbGlitchEffect.SetFear(fearLevel, 0f);
                lastFear = fearLevel;
            }

            if (!automaticCycle) return;
            cycleTime += Time.unscaledDeltaTime;
            if (cycleStep == 0 && cycleTime >= 2f)
            {
                RgbGlitchEffect.PlayDeath();
                cycleStep = 1;
            }
            else if (cycleStep == 1 && cycleTime >= 4f)
            {
                RgbGlitchEffect.PlayRespawn();
                cycleStep = 2;
            }
            else if (cycleTime >= 6f)
            {
                cycleTime = 0f;
                cycleStep = 0;
            }
        }

        private void OnDisable() => RgbGlitchEffect.ResetEffect();

        private void OnDestroy()
        {
            foreach (var material in materials)
            {
                if (material != null) Destroy(material);
            }
        }
    }
}

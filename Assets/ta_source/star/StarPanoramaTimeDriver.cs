using UnityEngine;

namespace TATools.StarPanorama
{
    [DisallowMultipleComponent]
    public sealed class StarPanoramaTimeDriver : MonoBehaviour
    {
        private static readonly int ShaderTimeId = Shader.PropertyToID("_StarPanoramaTime");

        [SerializeField, Tooltip("启用后星空在游戏暂停时仍继续流转。")]
        private bool useUnscaledTime;

        private void OnEnable()
        {
            UpdateShaderTime();
        }

        private void Update()
        {
            UpdateShaderTime();
        }

        private void UpdateShaderTime()
        {
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            Shader.SetGlobalFloat(ShaderTimeId, time);
        }
    }
}

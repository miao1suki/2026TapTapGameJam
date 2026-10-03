using System.Collections;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.ColorBlocks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ColorKeyPickup : MonoBehaviour
    {
        [SerializeField] private string colorTypeId = "red";
        [SerializeField] private PlayableDirector cameraDirector;
        [SerializeField] private bool hideOnCollect = true;
        private bool collected;

        public string ColorTypeId => colorTypeId;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            cameraDirector = GetComponent<PlayableDirector>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (collected || player == null) return;
            if (!ColorWorldManager.Instance.Unlock(colorTypeId)) return;
            collected = true;
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            PlayCameraCutscene(player);
            if (hideOnCollect)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            }
            if (cameraDirector != null && cameraDirector.playableAsset != null)
                StartCoroutine(DestroyAfterCutscene());
            else Destroy(gameObject);
        }

        private void PlayCameraCutscene(PlayerController player)
        {
            if (cameraDirector == null) cameraDirector = GetComponent<PlayableDirector>();
            if (cameraDirector == null || cameraDirector.playableAsset == null)
            {
                Debug.LogWarning("[ColorBlocks] 颜色钥匙未配置 Timeline，颜色仍已解锁。", this);
                return;
            }
            var rig = FindFirstObjectByType<TimelineCamRig>();
            if (rig == null)
            {
                var manager = FindFirstObjectByType<Project.CameraModes.CameraControlManager>();
                if (manager != null) rig = manager.gameObject.AddComponent<TimelineCamRig>();
            }
            if (rig == null)
            {
                Debug.LogWarning("[ColorBlocks] 未找到 TimelineCamRig，演出轨无法接管相机。", this);
                return;
            }
            rig.target = player.transform;
            if (cameraDirector.playableAsset is TimelineAsset timeline)
                foreach (var track in timeline.GetOutputTracks())
                    if (track is CameraTimelineTrack) cameraDirector.SetGenericBinding(track, rig);
            cameraDirector.Play();
        }

        private IEnumerator DestroyAfterCutscene()
        {
            yield return null;
            while (cameraDirector != null && cameraDirector.state == PlayState.Playing)
                yield return null;
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        public void EditorConfigure(string typeId, PlayableDirector director)
        {
            colorTypeId = typeId;
            cameraDirector = director;
        }
#endif
    }
}

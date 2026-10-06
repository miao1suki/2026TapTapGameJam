using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Interactions
{
    [DisallowMultipleComponent]
    public sealed class VineGrowthEmitter : MonoBehaviour
    {
        [SerializeField, Min(.5f)] private float searchRadius = 8f;
        [SerializeField, Min(.1f)] private float growthSpeed = 4f;
        [SerializeField, Min(.1f)] private float segmentLength = 1f;
        [SerializeField, Min(.05f)] private float segmentWidth = .35f;
        [SerializeField, Min(1)] private int maxSegments = 32;
        [SerializeField] private Material segmentMaterial;
        private readonly List<GameObject> segments = new List<GameObject>();
        private Coroutine growth;

        public void GrowVineTowardAnchors(GameObject context)
        {
            if (growth != null) StopCoroutine(growth);
            ClearSegments();
            growth = StartCoroutine(Grow());
        }

        private IEnumerator Grow()
        {
            Vector3 current = transform.position;
            var used = new HashSet<AnchorPoint>();
            for (int index = 0; index < maxSegments; index++)
            {
                AnchorPoint next = FindNearestAnchor(current, used);
                if (next == null) break;
                used.Add(next);
                Vector3 delta = next.transform.position - current;
                float distance = delta.magnitude;
                if (distance < .05f) { current = next.transform.position; continue; }
                Vector3 direction = delta / distance;
                int count = Mathf.Max(1, Mathf.CeilToInt(distance / segmentLength));
                for (int part = 0; part < count && index < maxSegments; part++, index++)
                {
                    float begin = distance * part / count;
                    float end = distance * (part + 1) / count;
                    CreateSegment(current + direction * ((begin + end) * .5f),
                        direction, end - begin);
                    yield return new WaitForSeconds(Mathf.Max(.01f, (end - begin) / growthSpeed));
                }
                current = next.transform.position;
            }
            growth = null;
        }

        private AnchorPoint FindNearestAnchor(Vector3 position, HashSet<AnchorPoint> used)
        {
            AnchorPoint best = null;
            float bestDistance = searchRadius;
            AnchorPoint[] anchors = FindObjectsByType<AnchorPoint>(FindObjectsSortMode.None);
            for (int index = 0; index < anchors.Length; index++)
            {
                AnchorPoint anchor = anchors[index];
                if (anchor == null || used.Contains(anchor)) continue;
                float distance = Vector3.Distance(position, anchor.transform.position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = anchor;
                }
            }
            return best;
        }

        private void CreateSegment(Vector3 center, Vector3 direction, float length)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = "Generated Vine Segment";
            segment.transform.position = center;
            segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            segment.transform.localScale = new Vector3(segmentWidth, Mathf.Max(.1f, length), segmentWidth);
            Renderer renderer = segment.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (segmentMaterial != null) renderer.sharedMaterial = segmentMaterial;
                else renderer.material.color = new Color(.2f, .8f, .25f, 1f);
            }
            segments.Add(segment);
        }

        private void OnDisable()
        {
            if (growth != null) StopCoroutine(growth);
            growth = null;
            ClearSegments();
        }

        private void ClearSegments()
        {
            for (int index = 0; index < segments.Count; index++)
            {
                if (segments[index] == null) continue;
                if (Application.isPlaying) Destroy(segments[index]);
                else DestroyImmediate(segments[index]);
            }
            segments.Clear();
        }
    }
}
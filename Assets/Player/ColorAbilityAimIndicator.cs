using Project.InputAbstraction;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Player
{
    // Runs after CameraManager (1000), matching the green visual driver's cadence.
    [AddComponentMenu(""), DisallowMultipleComponent, DefaultExecutionOrder(1100)]
    public sealed class ColorAbilityAimIndicator : MonoBehaviour
    {
        private const int Segments = 40;
        private readonly Vector3[] points = new Vector3[Segments + 1];
        private PlayerColorWheel wheel;
        private PlayerInteractionSensor sensor;
        private PlayerController player;
        private Material material;
        private LineRenderer outline, available;
        private Vector3 lastCursor = Vector3.positiveInfinity, lastPlayer = Vector3.positiveInfinity;
        private float lastRadius = -1f, lastRange = -1f;
        private string lastColor;
        private bool wasVisible;

        public void Initialize(PlayerColorWheel owner, PlayerInteractionSensor interactionSensor)
        {
            wheel = owner;
            sensor = interactionSensor;
            player = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            bool show = wheel != null && wheel.enabled && !wheel.IsWheelOpen &&
                (wheel.IsSelected("red") || wheel.IsSelected("blue")) && Time.timeScale > 0f &&
                player != null && !player.IsDead && !player.IsControlLocked && !player.IsCarrying &&
                !player.IsCarryBusy && !GameInput.IsPointerOverUi();
            if (!show || !ColorAbilityAim.TryPointer(Camera.main, wheel.AimPointerPosition,
                    transform.position, out Vector3 cursor))
            {
                SetVisible(false);
                return;
            }
            if (!EnsureLines()) return;
            float radius = wheel.AimToleranceWorld;
            float range = sensor.ScanRadius;
            Vector3 origin = transform.position;
            bool changed = lastCursor != cursor || lastPlayer != origin || lastRadius != radius ||
                lastRange != range || lastColor != wheel.SelectedColorId || !wasVisible;
            if (!changed) return;
            lastCursor = cursor; lastPlayer = origin; lastRadius = radius; lastRange = range;
            lastColor = wheel.SelectedColorId;
            SetVisible(true);
            DrawArc(outline, cursor, radius, 0f, Mathf.PI * 2f);
            Color tint = wheel.IsSelected("red") ? new Color(1f, .3f, .2f, .75f) : new Color(.2f, .65f, 1f, .75f);
            available.startColor = available.endColor = tint;
            bool centerInside = ColorAbilityAim.InsideRange(origin, cursor, range);
            float distance = Vector2.Distance(origin, cursor);
            if (!centerInside || radius > range + distance)
            {
                available.enabled = false;
                return;
            }
            if (distance + radius <= range)
            {
                DrawArc(available, cursor, radius, 0f, Mathf.PI * 2f);
                outline.enabled = false;
                return;
            }
            // The outside part is grey. No valid arc may advertise range expansion.
            Vector2 inward = origin - cursor;
            float halfAngle = Mathf.Acos(Mathf.Clamp((distance * distance + radius * radius - range * range) /
                (2f * Mathf.Max(.0001f, distance * radius)), -1f, 1f));
            float heading = Mathf.Atan2(inward.y, inward.x);
            DrawArc(available, cursor, radius, heading - halfAngle, halfAngle * 2f);
        }

        private bool EnsureLines()
        {
            if (outline != null) return true;
            Shader shader = Resources.Load<Shader>("GreenHookAimRing") ?? Shader.Find("Sprites/Default");
            if (shader == null) return false;
            material = new Material(shader) { name = "Red Blue Aim Ring", hideFlags = HideFlags.HideAndDontSave };
            outline = CreateLine("__ColorAimOutside", 1001, new Color(.6f, .6f, .6f, .35f));
            available = CreateLine("__ColorAimInside", 1002, Color.white);
            return true;
        }

        private LineRenderer CreateLine(string label, int order, Color tint)
        {
            var child = new GameObject(label) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = Segments + 1;
            line.widthMultiplier = Mathf.Max(.005f, wheel.AimToleranceWorld * .035f);
            line.startColor = line.endColor = tint;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = order;
            return line;
        }

        private void DrawArc(LineRenderer line, Vector3 center, float radius, float start, float sweep)
        {
            for (int i = 0; i <= Segments; i++)
            {
                float angle = start + sweep * i / Segments;
                points[i] = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }
            line.SetPositions(points);
            line.enabled = true;
        }

        private void SetVisible(bool visible)
        {
            wasVisible = visible;
            if (outline != null) outline.enabled = visible;
            if (available != null) available.enabled = visible;
        }

        private void OnDisable() => SetVisible(false);
        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (outline != null) Destroy(outline.gameObject);
            if (available != null) Destroy(available.gameObject);
        }
    }
}

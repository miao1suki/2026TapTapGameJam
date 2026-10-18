using UnityEngine;

namespace Project.Mechanisms
{
    public enum ButtonFacing
    {
        [InspectorName("朝上")] Up = 0,
        [InspectorName("朝下")] Down = 1,
        [InspectorName("朝左")] Left = 2,
        [InspectorName("朝右")] Right = 3
    }

    /// <summary>One authored cell, with a pressure surface on its selected edge.</summary>
    public static class ButtonPressureGeometry
    {
        public static bool IsPressureVolume(Collider collider)
        {
            // Match the sensor itself, never the entire button hierarchy: a
            // separate child wall remains a real placement/sweep obstruction.
            if (collider is not BoxCollider box || box != collider.GetComponent<BoxCollider>()) return false;
            return collider.GetComponent<DoorButton>() != null || collider.GetComponent<Project.BlockFeatures.ButtonLockFeature>() != null;
        }

        public static float RotationDegrees(ButtonFacing facing) => facing switch
        {
            ButtonFacing.Down => 180f,
            ButtonFacing.Left => 90f,
            ButtonFacing.Right => -90f,
            _ => 0f
        };

        public static Quaternion Rotation(ButtonFacing facing) => Quaternion.Euler(0f, 0f, RotationDegrees(facing));
        public static Vector3 Normal(ButtonFacing facing) => Rotation(facing) * Vector3.up;

        public static void Configure(BoxCollider trigger, ButtonFacing facing)
        {
            if (trigger == null) return;
            trigger.isTrigger = true;
            trigger.center = Rotation(facing) * new Vector3(0f, -.35f, 0f);
            trigger.size = facing == ButtonFacing.Left || facing == ButtonFacing.Right
                ? new Vector3(.3f, .6f, .6f) : new Vector3(.6f, .3f, .6f);
        }

        public static bool TouchesPressureFace(Bounds actor, BoxCollider trigger, ButtonFacing facing, float tolerance)
        {
            Vector3 normal = trigger.transform.TransformDirection(Normal(facing)).normalized;
            Vector3 absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            Bounds area = trigger.bounds;
            // The actor face directed toward the button, never its opposite face.
            float contact = Vector3.Dot(actor.center, normal) - Vector3.Dot(actor.extents, absolute);
            float center = Vector3.Dot(area.center, normal);
            float half = Vector3.Dot(area.extents, absolute);
            return contact >= center - half - tolerance && contact <= center + half + tolerance;
        }
    }
}

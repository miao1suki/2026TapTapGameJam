using System;
using UnityEngine;

namespace Project.CameraModes
{
    public enum CameraProjectionMode
    {
        Perspective = 0,
        Orthographic = 1,
    }

    public enum CameraInterruptionPolicy
    {
        AllowHigherPriority = 0,
        BlockAll = 1,
    }

    public static class CameraControlPriorities
    {
        public const int Gameplay = 0;
        public const int GameplayAbility = 100;
        public const int Cutscene = 1000;
        public const int Debug = 10000;
    }

    [Serializable]
    public struct CameraTransition
    {
        [Min(0f)]
        public float duration;

        public AnimationCurve easing;

        public static CameraTransition Immediate => new CameraTransition
        {
            duration = 0f,
            easing = AnimationCurve.Linear(0f, 0f, 1f, 1f),
        };

        public static CameraTransition Ease(float duration)
        {
            return new CameraTransition
            {
                duration = Mathf.Max(0f, duration),
                easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            };
        }
    }

    [Serializable]
    public struct CameraState
    {
        public Vector3 position;
        public Quaternion rotation;
        public CameraProjectionMode projection;
        [Min(0.01f)] public float orthographicSize;
        [Range(1f, 179f)] public float fieldOfView;

        public static CameraState FromCamera(Camera camera)
        {
            return new CameraState
            {
                position = camera.transform.position,
                rotation = camera.transform.rotation,
                projection = camera.orthographic
                    ? CameraProjectionMode.Orthographic
                    : CameraProjectionMode.Perspective,
                orthographicSize = camera.orthographicSize,
                fieldOfView = camera.fieldOfView,
            };
        }
    }

    public readonly struct CameraControlContext
    {
        public CameraControlContext(Vector3 focusPoint, float deltaTime)
        {
            FocusPoint = focusPoint;
            DeltaTime = deltaTime;
        }

        public Vector3 FocusPoint { get; }
        public float DeltaTime { get; }
    }

    public interface ICameraControlSource
    {
        string CameraControlName { get; }

        bool TryGetCameraState(in CameraControlContext context, out CameraState state);
    }

    public readonly struct CameraControlHandle : IEquatable<CameraControlHandle>, IDisposable
    {
        private readonly CameraControlManager manager;
        private readonly int id;

        internal CameraControlHandle(CameraControlManager manager, int id)
        {
            this.manager = manager;
            this.id = id;
        }

        internal CameraControlManager Manager => manager;
        internal int Id => id;

        public bool IsValid => manager != null && manager.IsHandleValid(this);
        public bool HasControl => manager != null && manager.HasControl(this);

        public void Release(CameraTransition transition = default)
        {
            if (manager != null)
            {
                manager.ReleaseControl(this, transition);
            }
        }

        public void Dispose()
        {
            Release(CameraTransition.Immediate);
        }

        public bool Equals(CameraControlHandle other)
        {
            return manager == other.manager && id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is CameraControlHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((manager != null ? manager.GetHashCode() : 0) * 397) ^ id;
            }
        }

        public static bool operator ==(CameraControlHandle left, CameraControlHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CameraControlHandle left, CameraControlHandle right)
        {
            return !left.Equals(right);
        }
    }
}

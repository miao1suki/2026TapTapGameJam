using UnityEngine;

namespace Project.CameraModes
{
    /// <summary>
    /// Implement this on gameplay objects that want to provide a dedicated
    /// camera-focus transform without coupling gameplay code to the manager.
    /// Complex follow logic should calculate a final focus point and call
    /// CameraControlManager.SetFocusPoint instead.
    /// </summary>
    public interface ICameraFollowTarget
    {
        Transform CameraFollowTransform { get; }
    }
}

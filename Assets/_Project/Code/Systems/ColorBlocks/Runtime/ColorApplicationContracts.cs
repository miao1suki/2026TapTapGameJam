using System;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>
    /// Fixed color object state. Color is an unlock group, never a mutable
    /// gameplay attribute that changes the object's form.
    /// </summary>
    public interface IColorObject
    {
        string BaseColorTypeId { get; }
        bool IsActive { get; }
        void Activate();
        void Deactivate();
        event Action<IColorObject, bool> ActiveStateChanged;
    }

    /// <summary>
    /// A target that can receive the player's selected color directly or
    /// from a fixed UniversalColorBlock broadcast.
    /// </summary>
    public interface IColorApplicationTarget
    {
        bool CanApplyColor(string colorId, GameObject actor);
        bool ApplyColor(string colorId, GameObject actor);
    }

    /// <summary>
    /// Optional lifecycle hook for room-scoped gameplay objects.
    /// </summary>
    public interface IRoomColorResettable
    {
        void ResetForRoom();
    }
}

using System;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.ContactCubes
{
    /// <summary>Blue hit application starts growth without changing the cube's fixed identity.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ContactCube))]
    public sealed class BlueContactCubeGrowthTarget : MonoBehaviour, IColorApplicationHitTarget
    {
        private ContactCube cube;
        private void Awake() => cube = GetComponent<ContactCube>();

        public bool CanApplyColor(string colorId, GameObject actor)
        {
            if (!isActiveAndEnabled || cube == null || !cube.CanRequestPlayerGrowth || actor == null ||
                !string.Equals(colorId, "blue", StringComparison.OrdinalIgnoreCase)) return false;
            PlayerController player = actor.GetComponent<PlayerController>();
            PlayerColorWheel wheel = actor.GetComponent<PlayerColorWheel>();
            return player != null && player.isActiveAndEnabled && !player.IsDead && !player.IsControlLocked &&
                !player.IsCarrying && !player.IsCarryBusy && player.gameObject.scene == gameObject.scene &&
                wheel != null && wheel.isActiveAndEnabled &&
                string.Equals(wheel.SelectedColorId, "blue", StringComparison.OrdinalIgnoreCase) &&
                ColorRuntimeService.Existing != null && ColorRuntimeService.Existing.IsAbilityUnlocked("blue");
        }

        // Contact and universal-block broadcasts use this entry. Only a real
        // player's hit application may start this independent growth request.
        public bool ApplyColor(string colorId, GameObject actor) => false;

        public bool ApplyColorAt(string colorId, GameObject actor, Vector3 hitWorldPoint)
        {
            if (!CanApplyColor(colorId, actor)) return false;
            BoxCollider shape = cube.GetComponentInChildren<BoxCollider>();
            float skin = cube.CellWorldSize * .01f;
            if (shape == null || (shape.ClosestPoint(hitWorldPoint) - hitWorldPoint).sqrMagnitude > skin * skin)
                return false;
            return cube.RequestPlayerGrowth(actor.GetComponent<PlayerController>().CarryCenterWorldPosition);
        }

        private void OnDisable() { if (cube != null) cube.CancelPlayerGrowth(); }
    }
}

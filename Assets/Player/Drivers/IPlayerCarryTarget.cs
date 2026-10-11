using System.Collections.Generic;
using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 玩家提供给搬运目标的只读命令口，避免目标直接依赖 PlayerController。
    /// </summary>
    public interface IPlayerCarryHost
    {
        Vector3 CarryAnchorWorldPosition { get; }
        Vector3 CarryCenterWorldPosition { get; }
    }

    /// <summary>
    /// 可被玩家从相邻网格拾取、顶在头顶并左右放下的场景物体。
    /// </summary>
    public interface IPlayerCarryTarget
    {
        Transform CarryTransform { get; }
        bool IsCarryActive { get; }
        bool IsCarryDropAnimating { get; }
        bool IsCarryResetAnimating { get; }
        bool CanCarry(IPlayerCarryHost player);
        bool BeginCarry(IPlayerCarryHost player);
        void UpdateCarryAnchor(Vector3 anchorWorldPosition);
        bool BeginCarryDrop(
            IPlayerCarryHost player,
            Vector3 worldPosition);
        bool BeginCarryReset(IPlayerCarryHost player);
        void CancelCarry();
    }

    /// <summary>
    /// 物体进入可拾取状态时注册，玩家脚本只在注册表中筛选相邻格目标。
    /// </summary>
    public static class PlayerCarryTargetRegistry
    {
        private static readonly List<IPlayerCarryTarget> Targets =
            new List<IPlayerCarryTarget>();

        public static IReadOnlyList<IPlayerCarryTarget> All
        {
            get
            {
                for (int index = Targets.Count - 1;
                     index >= 0;
                     index--)
                {
                    IPlayerCarryTarget target = Targets[index];
                    if (target == null ||
                        (target is Object unityObject &&
                         unityObject == null))
                    {
                        Targets.RemoveAt(index);
                    }
                }

                return Targets;
            }
        }

        public static void Register(IPlayerCarryTarget target)
        {
            if (target == null || Targets.Contains(target))
            {
                return;
            }

            Targets.Add(target);
        }

        public static void Unregister(IPlayerCarryTarget target)
        {
            if (target == null)
            {
                return;
            }

            Targets.Remove(target);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "升降机",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Movement,
        Interactions = BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Motion })]
    public sealed class LiftFeature : BlockFeature,
        IMechanismSignalReceiver
    {
        [BlockParameter(
            Label = "升降偏移",
            Group = "升降设置",
            Order = 0)]
        [SerializeField] private Vector3 localOffset =
            new Vector3(0f, 3f, 0f);

        [BlockParameter(
            Label = "单程时间",
            Group = "升降设置",
            Order = 1)]
        [SerializeField, Min(.05f)] private float travelDuration = 1.5f;

        [BlockParameter(
            Label = "端点停留",
            Group = "升降设置",
            Order = 2)]
        [SerializeField, Min(0f)] private float holdAtEnds = .5f;

        [BlockParameter(
            Label = "初始在顶端",
            Group = "升降设置",
            Order = 3)]
        [SerializeField] private bool startsAtTop;

        [BlockParameter(
            Label = "初始启动",
            Group = "升降设置",
            Order = 4)]
        [SerializeField] private bool startsActive = true;

        private Vector3 origin;
        private bool active;
        private float elapsed;

        protected override void OnAttach()
        {
            origin = transform.localPosition;
            active = startsActive;
            elapsed = startsAtTop
                ? travelDuration
                : 0f;
        }

        protected override void OnDetach()
        {
            transform.localPosition = origin;
        }

        protected override void OnTick(float deltaTime)
        {
            if (!active)
            {
                return;
            }

            elapsed += deltaTime;
            float travel = Mathf.Max(.05f, travelDuration);
            float hold = Mathf.Max(0f, holdAtEnds);
            float cycle = travel * 2f + hold * 2f;
            float time = cycle > 0f
                ? Mathf.Repeat(elapsed, cycle)
                : 0f;
            float progress;
            if (time < travel)
            {
                progress = time / travel;
            }
            else if (time < travel + hold)
            {
                progress = 1f;
            }
            else if (time < travel * 2f + hold)
            {
                progress = 1f -
                    (time - travel - hold) / travel;
            }
            else
            {
                progress = 0f;
            }

            transform.localPosition =
                origin + localOffset * progress;
        }

        protected override void OnResetForRoom()
        {
            active = startsActive;
            elapsed = startsAtTop ? travelDuration : 0f;
            transform.localPosition = origin;
        }

        public void SetSignal(bool activeValue, Component source)
        {
            active = activeValue;
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "运行状态",
                active ? "运行中" : "已停止"));
            values.Add(new BlockDebugValue(
                "当前位置",
                transform.localPosition));
        }
    }
}

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class AppearanceAlignmentTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        [Test]
        public void OfficialIcon_IsConfiguredForBothPlatforms()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Resources/正式ICON.png");
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.width, Is.EqualTo(512));
#pragma warning disable CS0618
            var desktop = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
#pragma warning restore CS0618
            Assert.That(desktop.Length, Is.GreaterThan(0));
            foreach (var texture in desktop) Assert.That(texture, Is.SameAs(icon));
            int count = 0;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(UnityEditor.Build.NamedBuildTarget.Android))
                foreach (var slot in PlayerSettings.GetPlatformIcons(UnityEditor.Build.NamedBuildTarget.Android, kind))
                    for (int layer = 0; layer < slot.maxLayerCount; layer++)
                    {
                        Assert.That(slot.GetTexture(layer), Is.SameAs(icon));
                        count++;
                    }
            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void StandardPlayer_OpaqueFeetMeetColliderAndLadderIsBehind()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"));
            var ladder = new GameObject("Alignment test ladder");
            try
            {
                var presentationType = Runtime("Project.Player.PlayerSpritePresentation");
                var presentation = root.GetComponent(presentationType);
                presentationType.GetMethod("Initialize", Private).Invoke(presentation, null);
                presentationType.GetMethod("Refresh", Private).Invoke(presentation, new object[] { 0f });
                var visual = (SpriteRenderer)presentationType.GetProperty("Visual").GetValue(presentation);
                Physics.SyncTransforms();
                Assert.That(visual.sprite, Is.Not.Null);
                // Idle/run frames have four empty pixels below the visible foot, at 16 effective pixels/cell.
                Assert.That(visual.bounds.min.y + .25f, Is.EqualTo(root.GetComponent<CapsuleCollider>().bounds.min.y).Within(.002f));
                ladder.AddComponent(Runtime("Project.Mechanisms.NeutralLadder"));
                var mechanism = Runtime("Project.BlockFeatures.MechanismArtworkPresentation");
                var ladderPresentation = mechanism.GetMethod("Ensure").Invoke(null, new object[] { ladder });
                var ladderVisual = (SpriteRenderer)mechanism.GetProperty("Visual").GetValue(ladderPresentation);
                Assert.That(ladderVisual.transform.position.z, Is.GreaterThan(visual.transform.position.z));
                Assert.That(ladderVisual.sortingOrder, Is.LessThan(visual.sortingOrder));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(ladder); }
        }

        [Test]
        public void SuspendedEntrance_FindsFloorWithoutMutatingPlayerAndRestoresPhysics()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.transform.position = new Vector3(20000, 3, 0);
                floor.transform.position = new Vector3(20000, -.5f, 0);
                floor.transform.localScale = new Vector3(10, 1, 10);
                var type = Runtime("Project.Player.PlayerController");
                var player = root.GetComponent(type);
                var body = root.GetComponent<Rigidbody>();
                var capsule = root.GetComponent<CapsuleCollider>();
                type.GetField("motor", Private).SetValue(player, body);
                type.GetField("capsule", Private).SetValue(player, capsule);
                Physics.SyncTransforms();
                float feetOffset = root.transform.position.y - capsule.bounds.min.y;
                bool gravity = body.useGravity, collisions = body.detectCollisions, kinematic = body.isKinematic;
                Assert.That(type.GetMethod("BeginPresentationMotion").Invoke(player, null), Is.True);
                var arguments = new object[] { root.transform.position, Vector3.zero };
                Assert.That(type.GetMethod("TryGetGroundedPresentationPosition").Invoke(player, arguments), Is.True);
                Assert.That(((Vector3)arguments[1]).y, Is.EqualTo(feetOffset).Within(.002f));
                Assert.That(root.transform.position.y, Is.EqualTo(3f));
                arguments[0] = new Vector3(30000, 3, 0);
                Assert.That(type.GetMethod("TryGetGroundedPresentationPosition").Invoke(player, arguments), Is.False);
                type.GetMethod("EndPresentationMotion").Invoke(player, null);
                Assert.That(body.useGravity, Is.EqualTo(gravity));
                Assert.That(body.detectCollisions, Is.EqualTo(collisions));
                Assert.That(body.isKinematic, Is.EqualTo(kinematic));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(floor); }
        }

        [Test]
        public void RespawnArtwork_ClosesSmallFloorGapWithoutMovingGameplayRoot()
        {
            var root = new GameObject("Alignment test anchor");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.transform.position = new Vector3(20000, .9f, 0);
                floor.transform.position = new Vector3(20000, -.5f, 0);
                floor.transform.localScale = new Vector3(3, 1, 3);
                Physics.SyncTransforms();
                root.AddComponent(Runtime("Project.BlockFeatures.RespawnAnchorFeature"));
                var type = Runtime("Project.BlockFeatures.WorldObjectSpritePresentation");
                var presentation = type.GetMethod("Ensure").Invoke(null, new object[] { root });
                type.GetMethod("Refresh").Invoke(presentation, new object[] { 0f });
                var visual = (SpriteRenderer)type.GetProperty("Visual").GetValue(presentation);
                Assert.That(visual.bounds.min.y, Is.EqualTo(0f).Within(.002f));
                Assert.That(root.transform.position.y, Is.EqualTo(.9f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(floor); }
        }
    }
}

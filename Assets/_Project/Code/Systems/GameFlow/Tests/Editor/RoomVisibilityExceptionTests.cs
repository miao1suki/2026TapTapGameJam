using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GameFlow.Tests
{
    public sealed class RoomVisibilityExceptionTests
    {
        static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        [TestCase(true)]
        [TestCase(false)]
        public void ExceptionRooms_StayVisibleAcrossRoomChanges_WithoutRevealingOtherCells(bool connected)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Room exception test");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var roomType = Runtime("Project.Rooms.RoomRegion");
                var managerType = Runtime("Project.Rooms.RoomLifecycleManager");
                var manager = root.AddComponent(managerType);
                // RoomLifecycleManager is not ExecuteAlways; register its lifecycle explicitly in EditMode.
                managerType.GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, null);
                var regions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(roomType));
                void AddRoom(string id, params Vector2Int[] cells)
                {
                    var region = Activator.CreateInstance(roomType);
                    roomType.GetField("id").SetValue(region, id);
                    roomType.GetField("cells").SetValue(region, new List<Vector2Int>(cells));
                    regions.Add(region);
                }
                AddRoom("room_cc07f6ef", new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1));
                AddRoom("room_791e28e6", new Vector2Int(4,0));
                AddRoom("ordinary", new Vector2Int(8,0));
                managerType.GetMethod("Configure").Invoke(manager, new object[] { regions, 1f, "" });
                var camera = root.AddComponent<Camera>(); camera.orthographic = true;
                var viewer = new GameObject("Viewer"); viewer.transform.SetParent(root.transform);
                var visibilityType = Runtime("Project.WorldVisibility.WorldVisibility2D");
                var visibility = root.AddComponent(visibilityType);
                visibilityType.GetField("viewer").SetValue(visibility, viewer.transform);
                visibilityType.GetField("connectedSpaceVisibility").SetValue(visibility, connected);
                visibilityType.GetField("unoccludedRoomIds").SetValue(visibility, new[] { "room_cc07f6ef", "room_791e28e6" });
                foreach (float x in new[] { 8.5f, .5f, 8.5f, 4.5f, .5f })
                {
                    viewer.transform.position = new Vector3(x, -.5f, 0);
                    Assert.That(visibilityType.GetMethod("Prepare").Invoke(visibility, new object[] { camera }), Is.True);
                    var mesh = (Mesh)visibilityType.GetProperty("UnoccludedRoomMesh").GetValue(visibility);
                    Assert.That(Covers(mesh, new Vector2(.5f,-.5f)), Is.True);
                    Assert.That(Covers(mesh, new Vector2(4.5f,-.5f)), Is.True);
                    Assert.That(Covers(mesh, new Vector2(1.5f,-1.5f)), Is.False, "Room shape hole must stay occluded.");
                    Assert.That(Covers(mesh, new Vector2(8.5f,-.5f)), Is.False, "Ordinary room must not be opened.");
                    Assert.That((float)visibilityType.GetProperty("OcclusionStrength").GetValue(visibility), Is.EqualTo(1f));
                }
                visibilityType.GetField("unoccludedRoomIds").SetValue(visibility, Array.Empty<string>());
                visibilityType.GetMethod("Prepare").Invoke(visibility, new object[] { camera });
                Assert.That(((Mesh)visibilityType.GetProperty("UnoccludedRoomMesh").GetValue(visibility)).vertexCount, Is.Zero);
            }
            finally
            {
                var type = Runtime("Project.Rooms.RoomLifecycleManager");
                var manager = root.GetComponent(type);
                if (manager != null) type.GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, null);
                UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static bool Covers(Mesh mesh, Vector2 point)
        {
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i += 4)
                if (point.x > vertices[i].x && point.x < vertices[i+2].x && point.y > vertices[i].y && point.y < vertices[i+2].y) return true;
            return false;
        }
    }
}

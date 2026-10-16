using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.GridArt.Editor;
using UnityEngine;

namespace Project.GridArt.Tests
{
    public sealed class CanvasViewportTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(.13f)] [TestCase(.75f)] [TestCase(2.01f)] [TestCase(5f)]
        [TestCase(16f)] [TestCase(26f)] [TestCase(192f)]
        public void GridDensityHasBoundedCostAndKeepsWholeLogicalCells(float size)
        {
            foreach (var viewport in new[] { new Rect(0, 0, 1600, 900), new Rect(4, 6, 7680, 4320) })
            {
                int stride = GridViewportDrawing.CellStride(size, viewport);
                Assert.That(stride, Is.GreaterThanOrEqualTo(1));
                Assert.That(stride & (stride - 1), Is.Zero, "LOD must retain stable power-of-two alignment");
                Assert.That(size * stride, Is.GreaterThanOrEqualTo(GridViewportDrawing.MinimumSpacing));
                foreach (float pan in new[] { -1000.25f, -25f, 0, 2.25f, 1000.5f })
                {
                    int count = GridViewportDrawing.LineCount(size * stride, viewport.xMin, viewport.xMax, pan) +
                        GridViewportDrawing.LineCount(size * stride, viewport.yMin, viewport.yMax, pan);
                    Assert.That(count, Is.LessThanOrEqualTo(GridViewportDrawing.LineBudget));
                    Assert.That(count, Is.GreaterThan(0));
                }
            }
        }

        [Test] public void GridPreservesUnitLinesAtNormalZoomAndRejectsInvalidPitch()
        {
            Assert.That(GridViewportDrawing.CellStride(26, new Rect(0, 0, 1600, 900)), Is.EqualTo(1));
            Assert.That(GridViewportDrawing.LineCount(26, 0, 26, 0), Is.EqualTo(2));
            Assert.That(GridViewportDrawing.LineCount(26, 0, 26, -26), Is.EqualTo(2));
            foreach (float size in new[] { 0, -1, float.NaN, float.PositiveInfinity })
                Assert.That(GridViewportDrawing.CellStride(size, new Rect(0, 0, 1600, 900)), Is.Zero);
        }

        // Planning lives in the legacy default editor assembly. Keep the tools' assembly dependency one-way.
        private static Type PlanningType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("PlanningEditorPrototype." + name)).First(t => t != null);
        private static object NewRoom() => Activator.CreateInstance(PlanningType("PlanningRoom"), "验证");
        private static IList Cells(object room) => (IList)room.GetType().GetField("cells").GetValue(room);
        private static void AddCell(object room, int x, int y) => Cells(room).Add(Activator.CreateInstance(PlanningType("PlanningCell"), x, y));
        private static object Geometry(object room) => Activator.CreateInstance(PlanningType("PlanningOwnerPreview"),
            Hidden | BindingFlags.Public, null, new[] { room }, null);
        private static IList Rectangles(object geometry) => (IList)geometry.GetType().GetField("Rectangles", Hidden).GetValue(geometry);
        private static object Invoke(object o, string method, params object[] args) => o.GetType().GetMethod(method, Hidden).Invoke(o, args);

        [Test] public void SolidRoomUsesOneRectangleInsteadOfThousandsOfCellPaths()
        {
            var room = NewRoom();
            for (int y = -64; y < 0; y++) for (int x = -50; x < 50; x++) AddCell(room, x, y);
            var geometry = Geometry(room);
            Assert.That(Rectangles(geometry).Count, Is.EqualTo(1));
            Assert.That(Rectangles(geometry)[0], Is.EqualTo(new RectInt(-50, -64, 100, 64)));
            Assert.That(((IList)geometry.GetType().GetField("Edges", Hidden).GetValue(geometry)).Count, Is.EqualTo(4));
            Assert.That(Cells(room).Count, Is.EqualTo(6400), "preview must not merge the actual map cells");
        }

        [Test] public void IrregularRoomPreviewRetainsHolesDisconnectedIslandsAndNegativeCoordinates()
        {
            var room = NewRoom(); var expected = new HashSet<Vector2Int>();
            for (int y = -3; y < 0; y++) for (int x = -3; x < 0; x++)
                if (x != -2 || y != -2) { AddCell(room, x, y); expected.Add(new Vector2Int(x, y)); }
            AddCell(room, 5, 2); expected.Add(new Vector2Int(5, 2)); AddCell(room, 5, 2);
            var geometry = Geometry(room); var actual = new HashSet<Vector2Int>();
            foreach (RectInt rect in Rectangles(geometry)) foreach (Vector2Int p in rect.allPositionsWithin)
                Assert.That(actual.Add(p), Is.True, "fill rectangles must not overlap and darken transparent fills");
            Assert.That(actual, Is.EquivalentTo(expected));
            Assert.That(((IList)geometry.GetType().GetField("Edges", Hidden).GetValue(geometry)).Count, Is.EqualTo(12));
        }

        [Test] public void PanReusesOwnerGeometryAndDocumentChangesInvalidateSameCountEdits()
        {
            var room = NewRoom(); AddCell(room, 0, 0); AddCell(room, 1, 0);
            object canvas = Activator.CreateInstance(PlanningType("PlanningCanvas"));
            object first = Invoke(canvas, "OwnerPreview", room);
            Assert.That(Invoke(canvas, "OwnerPreview", room), Is.SameAs(first));
            // An edit/move can keep the same number of cells; the normal document invalidation must clear it.
            Cells(room)[1] = Activator.CreateInstance(PlanningType("PlanningCell"), 3, 0);
            Invoke(canvas, "InvalidatePreviewData");
            object changed = Invoke(canvas, "OwnerPreview", room);
            Assert.That(changed, Is.Not.SameAs(first));
            Assert.That(Rectangles(changed).Count, Is.EqualTo(2));
        }

        [TestCase("ValidatePlanningOperations")]
        [TestCase("ValidateWorldAndAssemblyEditing")]
        [TestCase("VerifyUnifiedGrid")]
        public void ExistingPlanningOperationsAndLegacySaveMigrationStillPass(string method)
        {
            PlanningType("PlanningCanvas").GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(null, null);
        }
    }
}

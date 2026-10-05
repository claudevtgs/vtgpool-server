using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;

namespace VTG.Pool.Tests
{
    public sealed class RackLayoutTests
    {
        private const float R = PoolConstants.BallRadius;

        private static void AssertNoOverlap(Vector3[] positions, int count)
        {
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    Assert.GreaterOrEqual(Vector3.Distance(positions[i], positions[j]), 2f * R - 1e-6f, $"Balls {i} and {j} overlap");
                }
            }
        }

        [Test]
        public void Triangle_HasFifteenNonOverlappingTouchingBalls()
        {
            var positions = new Vector3[15];
            Vector3 apex = new Vector3(0f, 0.79f, 0.635f);
            RackLayout.GetTrianglePositions(apex, R, 0f, positions);
            AssertNoOverlap(positions, 15);
            Assert.AreEqual(apex, positions[0]);
            Assert.AreEqual(2f * R, Vector3.Distance(positions[0], positions[1]), 1e-5f, "Neighbours touch with zero gap");
            Assert.IsTrue(positions.All(p => p.z >= apex.z - 1e-6f), "Rack extends toward the foot rail");
        }

        [Test]
        public void Triangle_GapIsRespected()
        {
            var positions = new Vector3[15];
            RackLayout.GetTrianglePositions(Vector3.zero, R, 0.001f, positions);
            Assert.AreEqual(2f * R + 0.001f, Vector3.Distance(positions[0], positions[2]), 1e-5f);
        }

        [Test]
        public void EightBallOrder_IsValid()
        {
            int[] order = RackLayout.EightBallOrder;
            Assert.AreEqual(15, order.Distinct().Count());
            Assert.IsTrue(order.All(n => n >= 1 && n <= 15));
            Assert.AreEqual(8, order[4], "8-ball in the centre of the third row");
            bool cornerASolid = order[10] < 8;
            bool cornerBSolid = order[14] < 8;
            Assert.AreNotEqual(cornerASolid, cornerBSolid, "Back corners must be one solid and one stripe");
        }

        [Test]
        public void Diamond_HasNineNonOverlappingBallsWithNineInCentre()
        {
            var positions = new Vector3[9];
            RackLayout.GetDiamondPositions(Vector3.zero, R, 0f, positions);
            AssertNoOverlap(positions, 9);
            Assert.AreEqual(1, RackLayout.NineBallOrder[0]);
            int nineSlot = System.Array.IndexOf(RackLayout.NineBallOrder, 9);
            Assert.AreEqual(0f, positions[nineSlot].x, 1e-6f, "9-ball on the long axis");
            Assert.AreEqual(positions[0].z + 2f * (2f * R * Mathf.Sqrt(3f) * 0.5f), positions[nineSlot].z, 1e-5f, "9-ball in the middle row");
        }
    
        [Test]
        public void Jitter_KeepsBallsApartAndNearTheirSpots()
        {
            const float radius = 0.028575f;
            var rack = new Vector3[15];
            RackLayout.GetTrianglePositions(Vector3.zero, radius, 0.0002f, rack);
            var original = (Vector3[])rack.Clone();
            RackLayout.Jitter(rack, 15, radius, 0.0003f, 0.00005f, new System.Random(3));
            bool anyMoved = false;
            for (int i = 0; i < 15; i++)
            {
                float moved = Vector3.Distance(rack[i], original[i]);
                anyMoved |= moved > 1e-5f;
                Assert.Less(moved, 0.0015f, "Jitter stays sub-millimetre-ish");
                Assert.AreEqual(original[i].y, rack[i].y, 1e-6f);
                for (int j = i + 1; j < 15; j++)
                {
                    Assert.GreaterOrEqual(Vector3.Distance(rack[i], rack[j]), 2f * radius + 0.00004f, "No overlaps after jitter");
                }
            }

            Assert.IsTrue(anyMoved);
        }
}
}

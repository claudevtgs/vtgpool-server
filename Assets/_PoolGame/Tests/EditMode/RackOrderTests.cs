using System.Linq;
using NUnit.Framework;
using VTG.Pool.Balls;

namespace VTG.Pool.Tests
{
    public sealed class RackOrderTests
    {
        [Test]
        public void RandomEightBallRacks_AreAlwaysLegal()
        {
            var random = new System.Random(1234);
            for (int i = 0; i < 500; i++)
            {
                int[] order = RackLayout.BuildEightBallOrder(random);
                Assert.AreEqual(15, order.Distinct().Count());
                Assert.IsTrue(order.All(n => n >= 1 && n <= 15));
                Assert.AreEqual(8, order[RackLayout.EightBallSlot]);
                bool aSolid = order[RackLayout.BackCornerSlotA] < 8;
                bool bSolid = order[RackLayout.BackCornerSlotB] < 8;
                Assert.AreNotEqual(aSolid, bSolid);
            }
        }

        [Test]
        public void RandomNineBallRacks_AreAlwaysLegal()
        {
            var random = new System.Random(99);
            for (int i = 0; i < 200; i++)
            {
                int[] order = RackLayout.BuildNineBallOrder(random);
                Assert.AreEqual(9, order.Distinct().Count());
                Assert.AreEqual(1, order[0]);
                Assert.AreEqual(9, order[4]);
            }
        }

        [Test]
        public void SameSeed_GivesSameRack()
        {
            CollectionAssert.AreEqual(RackLayout.BuildEightBallOrder(new System.Random(7)), RackLayout.BuildEightBallOrder(new System.Random(7)));
        }
    }
}

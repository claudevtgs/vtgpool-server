using UnityEngine;

namespace VTG.Pool.Balls
{
    /// <summary>
    /// Pure rack geometry. Positions are computed from the ball diameter so racks never overlap.
    /// The apex points toward the head string (-Z); rows grow toward the foot rail (+Z).
    /// </summary>
    public static class RackLayout
    {
        private static readonly float RowSpacingFactor = Mathf.Sqrt(3f) * 0.5f;

        /// <summary>
        /// Standard 8-ball order for <see cref="GetTrianglePositions"/> slots:
        /// 8 in the centre of row 3, one solid and one stripe in the back corners.
        /// </summary>
        public static readonly int[] EightBallOrder = { 1, 9, 2, 10, 8, 3, 11, 7, 14, 4, 5, 13, 15, 6, 12 };

        /// <summary>9-ball diamond order: 1 at the apex, 9 in the centre.</summary>
        public static readonly int[] NineBallOrder = { 1, 2, 3, 4, 9, 5, 6, 7, 8 };

        private static readonly int[] DiamondRowCounts = { 1, 2, 3, 2, 1 };

        /// <summary>Triangle slot of the 8-ball (centre of the third row).</summary>
        public const int EightBallSlot = 4;

        /// <summary>Back-row corner slots of the triangle.</summary>
        public const int BackCornerSlotA = 10;
        public const int BackCornerSlotB = 14;

        /// <summary>
        /// Random legal 8-ball rack order: 8 in the centre, one solid and one stripe in the back corners,
        /// all other balls shuffled.
        /// </summary>
        public static int[] BuildEightBallOrder(System.Random random)
        {
            var solids = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5, 6, 7 };
            var stripes = new System.Collections.Generic.List<int> { 9, 10, 11, 12, 13, 14, 15 };
            var order = new int[15];
            order[EightBallSlot] = 8;

            int solid = TakeRandom(solids, random);
            int stripe = TakeRandom(stripes, random);
            bool solidFirst = random.Next(2) == 0;
            order[BackCornerSlotA] = solidFirst ? solid : stripe;
            order[BackCornerSlotB] = solidFirst ? stripe : solid;

            var rest = new System.Collections.Generic.List<int>(12);
            rest.AddRange(solids);
            rest.AddRange(stripes);
            for (int slot = 0; slot < 15; slot++)
            {
                if (slot == EightBallSlot || slot == BackCornerSlotA || slot == BackCornerSlotB)
                {
                    continue;
                }

                order[slot] = TakeRandom(rest, random);
            }

            return order;
        }

        /// <summary>Random legal 9-ball diamond order: 1 at the apex, 9 in the centre, others shuffled.</summary>
        public static int[] BuildNineBallOrder(System.Random random)
        {
            var rest = new System.Collections.Generic.List<int> { 2, 3, 4, 5, 6, 7, 8 };
            var order = new int[9];
            order[0] = 1;
            order[4] = 9;
            for (int slot = 1; slot < 9; slot++)
            {
                if (slot != 4)
                {
                    order[slot] = TakeRandom(rest, random);
                }
            }

            return order;
        }

        private static int TakeRandom(System.Collections.Generic.List<int> list, System.Random random)
        {
            int index = random.Next(list.Count);
            int value = list[index];
            list.RemoveAt(index);
            return value;
        }

        /// <summary>Fills 15 triangle slots (rows of 1..5) starting at the apex.</summary>
        public static void GetTrianglePositions(Vector3 apex, float radius, float gap, Vector3[] result)
        {
            if (result == null || result.Length < 15)
            {
                throw new System.ArgumentException("result must hold 15 positions");
            }

            float pitch = radius * 2f + gap;
            int index = 0;
            for (int row = 0; row < 5; row++)
            {
                float z = apex.z + row * pitch * RowSpacingFactor;
                for (int i = 0; i <= row; i++)
                {
                    float x = apex.x + (i - row * 0.5f) * pitch;
                    result[index++] = new Vector3(x, apex.y, z);
                }
            }
        }

        /// <summary>Fills 9 diamond slots (rows of 1,2,3,2,1) starting at the apex.</summary>
        public static void GetDiamondPositions(Vector3 apex, float radius, float gap, Vector3[] result)
        {
            if (result == null || result.Length < 9)
            {
                throw new System.ArgumentException("result must hold 9 positions");
            }

            float pitch = radius * 2f + gap;
            int index = 0;
            for (int row = 0; row < DiamondRowCounts.Length; row++)
            {
                float z = apex.z + row * pitch * RowSpacingFactor;
                int count = DiamondRowCounts[row];
                for (int i = 0; i < count; i++)
                {
                    float x = apex.x + (i - (count - 1) * 0.5f) * pitch;
                    result[index++] = new Vector3(x, apex.y, z);
                }
            }
        }
    
        /// <summary>
        /// Real racks are never a perfect lattice. Offsets each of the first <paramref name="count"/> positions by up to
        /// <paramref name="amount"/> on the table plane, then relaxes overlaps so centres stay at least
        /// 2R + <paramref name="minGap"/> apart. Without this a hard-sphere break channels the impulse along the
        /// lattice lines into the two back corners and the rest of the rack barely moves.
        /// </summary>
        public static void Jitter(Vector3[] positions, int count, float radius, float amount, float minGap, System.Random random)
        {
            if (amount <= 0f || random == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float x = (float)(random.NextDouble() * 2.0 - 1.0) * amount;
                float z = (float)(random.NextDouble() * 2.0 - 1.0) * amount;
                positions[i] += new Vector3(x, 0f, z);
            }

            float minDistance = 2f * radius + minGap;
            for (int iteration = 0; iteration < 16; iteration++)
            {
                bool moved = false;
                for (int i = 0; i < count - 1; i++)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        Vector3 delta = positions[j] - positions[i];
                        delta.y = 0f;
                        float distance = delta.magnitude;
                        if (distance < minDistance && distance > 1e-7f)
                        {
                            Vector3 push = delta / distance * ((minDistance - distance) * 0.5f + 1e-6f);
                            positions[i] -= push;
                            positions[j] += push;
                            moved = true;
                        }
                    }
                }

                if (!moved)
                {
                    break;
                }
            }
        }
}
}

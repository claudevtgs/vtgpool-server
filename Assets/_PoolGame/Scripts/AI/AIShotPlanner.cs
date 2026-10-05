using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Aiming;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;

namespace VTG.Pool.AI
{
    /// <summary>One active object ball in a read-only planning snapshot (cue ball supplied separately).</summary>
    public readonly struct AIBall
    {
        public readonly int Number;
        public readonly Vector3 Position;
        public AIBall(int number, Vector3 position) { Number = number; Position = position; }
    }

    public readonly struct AIShotCandidate
    {
        public readonly int BallNumber;
        public readonly int PocketIndex;
        public readonly Vector3 GhostPosition;
        public readonly Vector3 AimDirection;
        public readonly float CueDistance;
        public readonly float ObjectDistance;
        public readonly float CutAngle;
        public readonly float Score;

        public AIShotCandidate(int ballNumber, int pocketIndex, Vector3 ghostPosition,
            Vector3 aimDirection, float cueDistance, float objectDistance, float cutAngle, float score)
        {
            BallNumber = ballNumber;
            PocketIndex = pocketIndex;
            GhostPosition = ghostPosition;
            AimDirection = aimDirection;
            CueDistance = cueDistance;
            ObjectDistance = objectDistance;
            CutAngle = cutAngle;
            Score = score;
        }
    }

    /// <summary>
    /// Enumerates direct pots only. The caller supplies world-space pocket targets, playable centre
    /// bounds (Rect x/y represent world x/z), and a swept-ball clearance query for cushions/jaws.
    /// No scene mutation, random global state, or physics simulation occurs during planning.
    /// Power, scratch prediction, banks, combinations and break strategy belong to later stages.
    /// </summary>
    public static class AIShotPlanner
    {
        public delegate bool SurfacePathClear(Vector3 start, Vector3 end, float radius);

        /// <summary>Clears and fills a caller-owned list; larger score means an easier direct pot.</summary>
        public static void Generate(Vector3 cuePosition, IReadOnlyList<AIBall> balls,
            IReadOnlyList<Vector3> pockets, float radius, Rect playableCenters,
            MatchState state, IRuleSet rules, AIProfile profile, SurfacePathClear surfacePathClear,
            List<AIShotCandidate> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if (balls == null || pockets == null || state == null || rules == null || profile == null)
                throw new ArgumentNullException("Planning inputs must be provided.");
            if (surfacePathClear == null) throw new ArgumentNullException(nameof(surfacePathClear));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (state.IsGameOver || state.IsBreakShot) return;

            float clearance = Mathf.Max(0f, profile.clearance);
            for (int i = 0; i < balls.Count; i++)
            {
                AIBall target = balls[i];
                if (!state.IsOnTable(target.Number) || !rules.IsLegalFirstContact(state, target.Number)) continue;
                for (int p = 0; p < pockets.Count; p++)
                {
                    Vector3 pocket = pockets[p];
                    pocket.y = cuePosition.y;
                    Vector3 objectPosition = target.Position;
                    objectPosition.y = cuePosition.y;
                    Vector3 objectPath = pocket - objectPosition;
                    float objectDistance = objectPath.magnitude;
                    if (objectDistance < radius) continue;
                    Vector3 ghost = GhostBallMath.GhostBallForTarget(objectPosition, pocket, radius);
                    if (!playableCenters.Contains(new Vector2(ghost.x, ghost.z))) continue;
                    Vector3 cuePath = ghost - cuePosition;
                    float cueDistance = cuePath.magnitude;
                    if (cueDistance < 1e-5f) continue;
                    Vector3 aim = cuePath / cueDistance;
                    float cut = GhostBallMath.CutAngleDegrees(aim, objectPath);
                    if (cut > Mathf.Clamp(profile.maximumCutAngle, 1f, 85f)) continue;
                    if (!BallPathClear(cuePosition, ghost, balls, target.Number, state, 2f * radius + clearance)
                        || !BallPathClear(objectPosition, pocket, balls, target.Number, state, 2f * radius + clearance)
                        || !surfacePathClear(cuePosition, ghost, radius + clearance)
                        || !surfacePathClear(objectPosition, pocket, radius + clearance)) continue;
                    float score = -Mathf.Max(0f, profile.distanceWeight) * (cueDistance + objectDistance)
                        - Mathf.Max(0f, profile.cutWeight) * (cut / 90f);
                    output.Add(new AIShotCandidate(target.Number, p, ghost, aim,
                        cueDistance, objectDistance, cut, score));
                }
            }
        }

        private static bool BallPathClear(Vector3 start, Vector3 end, IReadOnlyList<AIBall> balls,
            int excludedNumber, MatchState state, float separation)
        {
            Vector3 path = GhostBallMath.Flatten(end - start);
            float lengthSquared = path.sqrMagnitude;
            for (int i = 0; i < balls.Count; i++)
            {
                AIBall ball = balls[i];
                if (ball.Number == excludedNumber || !state.IsOnTable(ball.Number)) continue;
                Vector3 offset = GhostBallMath.Flatten(ball.Position - start);
                float t = lengthSquared > 1e-10f ? Mathf.Clamp01(Vector3.Dot(offset, path) / lengthSquared) : 0f;
                if ((offset - t * path).sqrMagnitude <= separation * separation) return false;
            }
            return true;
        }

        public static bool TrySelectBest(IReadOnlyList<AIShotCandidate> candidates, out AIShotCandidate best)
        {
            best = default;
            if (candidates == null || candidates.Count == 0) return false;
            best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
                if (candidates[i].Score > best.Score) best = candidates[i];
            return true;
        }

        /// <summary>Caller-owned seeded RNG supports repeatable tests/replays without affecting Unity Random.</summary>
        public static ShotParameters ApplyExecutionError(ShotParameters shot, AIProfile profile, System.Random random)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (random == null) throw new ArgumentNullException(nameof(random));
            float angle = ((float)random.NextDouble() * 2f - 1f) * profile.AimErrorDegrees;
            float powerScale = 1f + ((float)random.NextDouble() * 2f - 1f) * profile.RelativePowerError;
            return new ShotParameters(Quaternion.AngleAxis(angle, Vector3.up) * shot.AimDirection,
                shot.Power * powerScale, shot.TipOffset);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Aiming;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Simulation;
using VTG.Pool.Table;

namespace VTG.Pool.AI
{
    /// <summary>Owns computer turns; uses the same placement, strike and rules pipeline as a human.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class AIController : MonoBehaviour
    {
        private MatchManager match;
        private ShotController shot;
        private CueBallPlacementController placement;
        private TableBuilder table;
        private AIProfile profile;
        private readonly List<AIBall> balls = new List<AIBall>(15);
        private readonly List<Vector3> pockets = new List<Vector3>(6);
        private readonly List<AIShotCandidate> candidates = new List<AIShotCandidate>(90);
        private System.Random random = new System.Random();
        public double LastPlanningMilliseconds { get; private set; }
        public void SetRandomSeed(int seed) => random = new System.Random(seed);
        private AIShotPlanner.SurfacePathClear surfaceQuery;
        private AIKickPlanner.RailAvailable railQuery;
        public bool LastPlanWasKick { get; private set; }
        public int LastKickTrials { get; private set; }
        private float thinking;
        public bool IsThinking { get; private set; }

        public void Configure(MatchManager owner, ShotController controller, AIProfile settings)
        {
            if (match != null) match.TurnManager.StateChanged -= SyncOwnership;
            match = owner;
            shot = controller;
            placement = owner.TurnManager.Placement;
            table = shot.Table;
            profile = settings;
            surfaceQuery = SurfaceClear;
            railQuery = RailAvailable;
            match.TurnManager.StateChanged += SyncOwnership;
            thinking = 0f;
            SyncOwnership();
        }

        private void OnDisable()
        {
            if (match != null) match.TurnManager.StateChanged -= SyncOwnership;
            if (shot != null) shot.ComputerOwnsInput = false;
            IsThinking = false;
        }

        private void OnEnable()
        {
            if (match != null)
            {
                match.TurnManager.StateChanged -= SyncOwnership;
                match.TurnManager.StateChanged += SyncOwnership;
                SyncOwnership();
            }
        }

        private void SyncOwnership()
        {
            shot.ComputerOwnsInput = isActiveAndEnabled && match.State != null
                && !match.State.IsGameOver && match.State.CurrentPlayer.Kind == PlayerKind.AI;
            thinking = 0f;
        }

        private void Update()
        {
            if (shot == null || !shot.ComputerOwnsInput || match.State.IsGameOver
                || shot.Phase != ShotPhase.Aiming || Time.timeScale <= 0f || Replay.ShotReplay.IsPlaying)
            { IsThinking = false; return; }
            IsThinking = true;
            thinking += Time.deltaTime;
            if (thinking < profile.thinkSeconds) return;
            thinking = 0f;
            Snapshot();
            if (placement.Mode != BallInHandMode.None && placement.IsPlacing)
            {
                PlaceCueBall();
                if (!placement.TryConfirm()) return;
            }
            if (shot.CueBall.IsHeld || shot.CueBall.IsPocketed || !AtRest()) return;
            if (TryPlan(out ShotParameters planned))
            {
                shot.ExecuteShot(AIShotPlanner.ApplyExecutionError(planned, profile, random));
                IsThinking = false;
            }
        }

        private static bool AtRest()
        {
            var all = BallRegistry.Balls;
            for (int i = 0; i < all.Count; i++)
                if (!all[i].IsPocketed && !all[i].IsHeld
                    && (all[i].LinearVelocity.sqrMagnitude > 0.0001f || all[i].AngularVelocity.sqrMagnitude > 0.04f)) return false;
            return true;
        }

        private void Snapshot()
        {
            balls.Clear();
            var all = BallRegistry.Balls;
            for (int i = 0; i < all.Count; i++)
                if (!all[i].IsCueBall && !all[i].IsPocketed && all[i].gameObject.activeInHierarchy)
                    balls.Add(new AIBall(all[i].BallId, all[i].Position));
            pockets.Clear();
            var holes = table.PocketManager.Pockets;
            for (int i = 0; i < holes.Count; i++) pockets.Add(holes[i].Center);
            Physics.SyncTransforms();
        }

        private Rect Bounds()
        {
            float r = shot.CueBall.Radius;
            Vector3 center = table.SurfaceCenter;
            return new Rect(center.x - table.Geometry.HalfWidth + r, center.z - table.Geometry.HalfLength + r,
                table.Geometry.playWidth - 2f * r, table.Geometry.playLength - 2f * r);
        }

        private bool SurfaceClear(Vector3 start, Vector3 end, float radius)
        {
            Vector3 delta = end - start;
            int mask = 1 << PoolLayers.Cushion;
            if (Physics.CheckSphere(start, radius, mask, QueryTriggerInteraction.Ignore)) return false;
            return !Physics.SphereCast(start, radius, delta.normalized, out _, delta.magnitude,
                mask, QueryTriggerInteraction.Ignore);
        }

        private void Generate(Vector3 cuePosition)
        {
            AIShotPlanner.Generate(cuePosition, balls, pockets, shot.CueBall.Radius, Bounds(),
                match.State, match.Rules, profile, surfaceQuery, candidates);
        }

        private void PlaceCueBall()
        {
            Vector3 best = shot.CueBall.Position;
            float score = float.NegativeInfinity;
            if (match.State.IsBreakShot)
            {
                placement.TryMoveTo(table.HeadSpot + Vector3.up * shot.CueBall.Radius);
                return;
            }
            Rect bounds = Bounds();
            // A small fixed grid keeps placement bounded and considers both kitchen and full-table rights.
            for (int x = 0; x < 7; x++)
                for (int z = 0; z < 13; z++)
                {
                    Vector3 point = new Vector3(Mathf.Lerp(bounds.xMin + 0.03f, bounds.xMax - 0.03f, x / 6f),
                        table.SurfaceHeight + shot.CueBall.Radius,
                        Mathf.Lerp(bounds.yMin + 0.03f, bounds.yMax - 0.03f, z / 12f));
                    if (!placement.IsValid(point)) continue;
                    Generate(point);
                    if (AIShotPlanner.TrySelectBest(candidates, out var candidate) && candidate.Score > score)
                    { score = candidate.Score; best = point; }
                }
            placement.TryMoveTo(best);
        }

        public bool TryPlan(out ShotParameters planned)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { return Plan(out planned); }
            finally
            {
                LastPlanningMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start)
                    * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
        }

        private bool Plan(out ShotParameters planned)
        {
            LastPlanWasKick = false;
            LastKickTrials = 0;
            planned = default;
            Snapshot();
            Vector3 cue = shot.CueBall.Position;
            if (!match.State.IsBreakShot)
            {
                Generate(cue);
                float best = float.NegativeInfinity;
                bool found = false;
                for (int i = 0; i < candidates.Count; i++)
                {
                    AIShotCandidate c = candidates[i];
                    Vector3 target = c.GhostPosition + GhostBallMath.Flatten(pockets[c.PocketIndex] - c.GhostPosition).normalized * (2f * shot.CueBall.Radius);
                    if (!AIShotPrediction.TryEstimate(c, target, shot.StrikeProfile, shot.CueBall.Profile,
                        profile.pocketArrivalSpeed, out var parameters, out var velocity, out var angular)) continue;
                    float score = c.Score - PositionCost(c.GhostPosition, velocity, angular);
                    if (score > best) { best = score; planned = parameters; found = true; }
                }
                if (found) return true;
            }
            // Break: nearest visible legal ball is the rack apex. Otherwise try direct legal contact.
            float nearest = float.PositiveInfinity;
            bool contact = false;
            for (int i = 0; i < balls.Count; i++)
            {
                AIBall ball = balls[i];
                if (!match.Rules.IsLegalFirstContact(match.State, ball.Number)) continue;
                Vector3 delta = GhostBallMath.Flatten(ball.Position - cue);
                float length = delta.magnitude;
                if (length >= nearest || length < 0.0001f) continue;
                Vector3 end = ball.Position - delta.normalized * (2f * shot.CueBall.Radius);
                if (!ClearContact(cue, end, ball.Number)) continue;
                nearest = length;
                planned = new ShotParameters(delta, match.State.IsBreakShot ? profile.breakPower : profile.fallbackPower, Vector2.zero);
                contact = true;
            }
            if (contact) return true;
            if (AIKickPlanner.TryPlan(cue, balls, Bounds(), match.State, match.Rules, shot.StrikeProfile,
                shot.CueBall.Profile, table.Geometry.cushionNoseHeight, profile, railQuery,
                out planned, out _, out int trials))
            {
                LastKickTrials = trials;
                LastPlanWasKick = true;
                return true;
            }
            LastKickTrials = trials;
            // Snookered: a physical attempt still advances the turn; rules adjudicate any foul normally.
            for (int i = 0; i < balls.Count; i++)
                if (match.Rules.IsLegalFirstContact(match.State, balls[i].Number))
                { planned = new ShotParameters(balls[i].Position - cue, profile.fallbackPower, Vector2.zero); return true; }
            return false;
        }

        private bool RailAvailable(Vector3 center, Vector3 inward, float radius)
        {
            // The straight nose must span the whole ball footprint (+ margin): contacts at a jaw tip next to a
            // pocket mouth rebound unpredictably (edge vs face normal), so they are not usable kick points.
            Vector3 along = Vector3.Cross(Vector3.up, inward).normalized * (radius + 0.01f);
            return RailFaceAt(center, inward, radius) && RailFaceAt(center + along, inward, radius) && RailFaceAt(center - along, inward, radius);
        }

        private bool RailFaceAt(Vector3 center, Vector3 inward, float radius)
        {
            // Verify the physical nose exists here: side/corner mouths and jaw faces are not rails.
            Vector3 start = center + inward * 0.02f;
            if (!Physics.SphereCast(start, radius, -inward, out RaycastHit hit, 0.025f,
                1 << PoolLayers.Cushion, QueryTriggerInteraction.Ignore)) return false;
            return Vector3.Dot(hit.normal, inward) > 0.99f
                && hit.collider.TryGetComponent<CushionSurface>(out var surface)
                && surface.Kind == CushionSurfaceKind.Rail;
        }

        private bool ClearContact(Vector3 start, Vector3 end, int target)
        {
            if (!SurfaceClear(start, end, shot.CueBall.Radius)) return false;
            Vector3 delta = end - start;
            for (int i = 0; i < balls.Count; i++)
                if (balls[i].Number != target && GhostBallMath.TryGetContactDistance(start, delta,
                    balls[i].Position, shot.CueBall.Radius, out float d) && d <= delta.magnitude) return false;
            return true;
        }

        private float PositionCost(Vector3 position, Vector3 velocity, Vector3 angular)
        {
            var cloth = shot.CueBall.Profile.ToClothParameters(Mathf.Abs(Physics.gravity.y));
            Rect bounds = Bounds();
            for (int step = 0; step < 2400 && velocity.sqrMagnitude > 0.000025f; step++)
            {
                ClothFrictionModel.Step(ref velocity, ref angular, cloth, 1f / 120f);
                position += velocity / 120f;
                var holes = table.PocketManager.Pockets;
                for (int p = 0; p < holes.Count; p++)
                    if (GhostBallMath.Flatten(position - holes[p].Center).sqrMagnitude
                        < Mathf.Pow(holes[p].HoleRadius + shot.CueBall.Radius, 2f)) return profile.scratchWeight;
                // Stop at the first rail; this heuristic does not claim a full-table prediction.
                if (!bounds.Contains(new Vector2(position.x, position.z))) break;
            }
            return GhostBallMath.Flatten(position - table.SurfaceCenter).magnitude * profile.positionWeight;
        }
    }
}

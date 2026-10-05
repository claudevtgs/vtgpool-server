using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Aiming;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Simulation;

namespace VTG.Pool.AI
{
    /// <summary>
    /// Bounded search for a one-rail first contact on an axis-aligned table. Mirror geometry seeds
    /// the search; the actual cloth and cushion models determine the path. No live bodies are moved.
    /// This predicts first contact only, not a pot or a guaranteed foul-free outcome.
    /// </summary>
    public static class AIKickPlanner
    {
        public delegate bool RailAvailable(Vector3 ballCenter, Vector3 inwardNormal, float radius);
        private const float Dt = 1f / 120f;
        private const int MaxSteps = 1800;

        public static bool TryPlan(Vector3 cuePosition, IReadOnlyList<AIBall> balls, Rect bounds,
            MatchState state, IRuleSet rules, CueStrikeProfile cue, BallPhysicsProfile physics,
            float noseHeight, AIProfile profile, RailAvailable railAvailable,
            out ShotParameters shot, out int firstBall, out int trials)
        {
            shot = default;
            firstBall = -1;
            trials = 0;
            if (state == null || state.IsBreakShot || state.IsGameOver || balls == null
                || railAvailable == null || !bounds.Contains(new Vector2(cuePosition.x, cuePosition.z))) return false;
            float best = float.NegativeInfinity;
            int offsets = Mathf.CeilToInt(Mathf.Clamp(profile.kickSearchDegrees, 0f, 20f) / 0.5f);
            for (int b = 0; b < balls.Count; b++)
            {
                if (!state.IsOnTable(balls[b].Number) || !rules.IsLegalFirstContact(state, balls[b].Number)) continue;
                for (int rail = 0; rail < 4; rail++)
                {
                    Vector3 reflected = balls[b].Position;
                    switch (rail)
                    {
                        case 0: reflected.x = 2f * bounds.xMin - reflected.x; break;
                        case 1: reflected.x = 2f * bounds.xMax - reflected.x; break;
                        case 2: reflected.z = 2f * bounds.yMin - reflected.z; break;
                        default: reflected.z = 2f * bounds.yMax - reflected.z; break;
                    }
                    Vector3 seed = GhostBallMath.Flatten(reflected - cuePosition).normalized;
                    // Centre-out order searches the geometric seed before increasingly large corrections.
                    for (int o = 0; o <= offsets * 2; o++)
                    {
                        float angle = o == 0 ? 0f : ((o + 1) / 2) * 0.5f * (o % 2 == 1 ? 1f : -1f);
                        var candidate = new ShotParameters(Quaternion.AngleAxis(angle, Vector3.up) * seed,
                            profile.kickPower, Vector2.zero);
                        trials++;
                        if (!Trace(cuePosition, candidate, balls, bounds, state, rules, cue, physics,
                            noseHeight, railAvailable, out int hit, out float quality)) continue;
                        if (quality > best) { best = quality; shot = candidate; firstBall = hit; }
                        // A solid contact is more tolerant of execution error than a grazing hit.
                        if (quality >= 0.8f) return true;
                    }
                }
            }
            return firstBall > 0;
        }

        private static bool Trace(Vector3 position, ShotParameters shot, IReadOnlyList<AIBall> balls,
            Rect bounds, MatchState state, IRuleSet rules, CueStrikeProfile cue, BallPhysicsProfile physics,
            float noseHeight, RailAvailable railAvailable, out int firstBall, out float quality)
        {
            firstBall = -1;
            quality = 0f;
            var strike = CueStrikeModel.Compute(shot, cue.EvaluateCueSpeed(shot.Power), physics.ballMass,
                physics.ballRadius, cue.ToSettings());
            Vector3 velocity = strike.LinearVelocity, angular = strike.AngularVelocity;
            var cloth = physics.ToClothParameters(Mathf.Abs(Physics.gravity.y));
            var cushion = physics.ToCushionParameters(noseHeight - physics.ballRadius, 1f, 1f);
            bool bounced = false;
            for (int step = 0; step < MaxSteps; step++)
            {
                ClothFrictionModel.Step(ref velocity, ref angular, cloth, Dt);
                if (velocity.sqrMagnitude < 0.01f) return false;
                float time = Dt;
                Vector3 normal = Vector3.zero;
                CheckRail(position.x, velocity.x, bounds.xMin, bounds.xMax, Vector3.right, ref time, ref normal);
                CheckRail(position.z, velocity.z, bounds.yMin, bounds.yMax, Vector3.forward, ref time, ref normal);
                float firstTime = time;
                int index = -1;
                for (int b = 0; b < balls.Count; b++)
                {
                    if (!state.IsOnTable(balls[b].Number)) continue;
                    Vector3 target = balls[b].Position;
                    target.y = position.y;
                    if (BallCollisionModel.TimeOfImpact(position, velocity, target, Vector3.zero,
                        2f * physics.ballRadius, firstTime, out float t)) { index = b; firstTime = t; }
                }
                if (index >= 0)
                {
                    if (!bounced || !rules.IsLegalFirstContact(state, balls[index].Number)) return false;
                    Vector3 contactNormal = GhostBallMath.Flatten(balls[index].Position - (position + velocity * firstTime)).normalized;
                    quality = Vector3.Dot(velocity.normalized, contactNormal);
                    if (quality < 0.35f || velocity.magnitude * quality < 0.5f) return false;
                    firstBall = balls[index].Number;
                    return true;
                }
                position += velocity * time;
                if (normal == Vector3.zero) continue;
                if (bounced || !railAvailable(position, normal, physics.ballRadius)) return false;
                CushionResponseModel.Resolve(ref velocity, ref angular, normal, cushion, out _);
                position += normal * 0.0001f;
                bounced = true;
            }
            return false;
        }

        private static void CheckRail(float position, float speed, float min, float max, Vector3 axis,
            ref float time, ref Vector3 normal)
        {
            if (Mathf.Abs(speed) < 1e-7f) return;
            float t = ((speed > 0f ? max : min) - position) / speed;
            if (t >= 0f && t <= time) { time = t; normal = speed > 0f ? -axis : axis; }
        }
    }
}

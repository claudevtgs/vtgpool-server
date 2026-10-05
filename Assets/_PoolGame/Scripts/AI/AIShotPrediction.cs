using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Simulation;

namespace VTG.Pool.AI
{
    /// <summary>Cloth/first-contact prediction only; later rails and secondary collisions are not simulated.</summary>
    public static class AIShotPrediction
    {
        private const float Dt = 1f / 120f;
        private const int MaxSteps = 3600;

        public static bool TryEstimate(AIShotCandidate candidate, Vector3 objectPosition,
            CueStrikeProfile cue, BallPhysicsProfile physics, float arrivalSpeed,
            out ShotParameters shot, out Vector3 cueVelocity, out Vector3 cueAngular)
        {
            shot = default;
            cueVelocity = cueAngular = Vector3.zero;
            // Search launch speed rather than power: user curves need not be monotonic.
            float maxSpeed = 0f;
            for (int i = 1; i <= 128; i++)
                maxSpeed = Mathf.Max(maxSpeed, CueStrikeModel.BallSpeed(cue.EvaluateCueSpeed(i / 128f),
                    physics.ballMass, cue.ToSettings(), Vector2.zero));
            float lo = 0f, hi = maxSpeed;
            if (!ReachesPocket(hi, candidate, objectPosition, physics, arrivalSpeed, out _, out _)) return false;
            for (int i = 0; i < 12; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (ReachesPocket(mid, candidate, objectPosition, physics, arrivalSpeed, out _, out _)) hi = mid;
                else lo = mid;
            }
            float bestPower = 1f, difference = float.PositiveInfinity;
            for (int i = 1; i <= 512; i++)
            {
                float power = i / 512f;
                float speed = CueStrikeModel.BallSpeed(cue.EvaluateCueSpeed(power), physics.ballMass,
                    cue.ToSettings(), Vector2.zero);
                if (speed >= hi && speed - hi < difference) { difference = speed - hi; bestPower = power; }
            }
            if (float.IsInfinity(difference)) return false;
            shot = new ShotParameters(candidate.AimDirection, bestPower, Vector2.zero);
            float actualSpeed = CueStrikeModel.BallSpeed(cue.EvaluateCueSpeed(bestPower), physics.ballMass,
                cue.ToSettings(), Vector2.zero);
            return ReachesPocket(actualSpeed, candidate, objectPosition, physics, arrivalSpeed, out cueVelocity, out cueAngular);
        }

        private static bool ReachesPocket(float speed, AIShotCandidate candidate, Vector3 objectPosition,
            BallPhysicsProfile physics, float arrivalSpeed, out Vector3 cueVelocity, out Vector3 cueAngular)
        {
            cueVelocity = candidate.AimDirection * speed;
            cueAngular = Vector3.zero;
            ClothParameters cloth = physics.ToClothParameters(Mathf.Abs(Physics.gravity.y));
            if (!Travel(ref cueVelocity, ref cueAngular, candidate.CueDistance, cloth)) return false;
            Vector3 objectVelocity = Vector3.zero, objectAngular = Vector3.zero;
            var collision = new BallCollisionParameters { Radius = physics.ballRadius,
                Restitution = physics.ballRestitution, Friction = physics.ballBallFriction };
            BallCollisionModel.Resolve(ref cueVelocity, ref cueAngular, ref objectVelocity, ref objectAngular,
                candidate.GhostPosition, objectPosition, collision, out _);
            return Travel(ref objectVelocity, ref objectAngular, candidate.ObjectDistance, cloth)
                && objectVelocity.magnitude >= arrivalSpeed;
        }

        private static bool Travel(ref Vector3 velocity, ref Vector3 angular, float distance, ClothParameters cloth)
        {
            float remaining = distance;
            for (int i = 0; i < MaxSteps; i++)
            {
                float speed = velocity.magnitude;
                if (speed < 0.001f) return false;
                float dt = Mathf.Min(Dt, remaining / speed);
                ClothFrictionModel.Step(ref velocity, ref angular, cloth, dt);
                remaining -= velocity.magnitude * dt;
                if (remaining <= 0.0001f) return true;
            }
            return false;
        }
    }
}

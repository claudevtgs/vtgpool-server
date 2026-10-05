using UnityEngine;
using VTG.Pool.Core;

namespace VTG.Pool.Simulation
{
    /// <summary>Inputs for <see cref="ClothFrictionModel"/> (SI units).</summary>
    public struct ClothParameters
    {
        public float SlidingFriction;
        public float RollingResistance;
        public float SpinFriction;
        public float Gravity;
        public float Radius;
        public float MinLinearSpeed;
        public float MinAngularSpeed;
        public float SlipSpeedEpsilon;
    }

    /// <summary>Motion phase on the cloth produced by one model step.</summary>
    public enum ClothMotionPhase
    {
        Stationary,
        Sliding,
        Rolling,
        Spinning
    }

    /// <summary>
    /// Ball-on-cloth model, independent of PhysX so it can be unit tested and reused for prediction.
    ///
    /// Sliding: the contact point slips with velocity u = v + w x (-R up). Kinetic friction
    /// F = -mu_s m g u_hat acts at the contact, decelerating v and changing w. The slip decays at
    /// (7/2) mu_s g, so a stun shot reaches pure rolling at 5/7 of its initial speed.
    ///
    /// Rolling: v = w x r is enforced; rolling resistance decelerates at mu_r g.
    /// Spin about the vertical axis decays independently at 2.5 mu_sp g / R.
    /// </summary>
    public static class ClothFrictionModel
    {
        private const float InverseSurfaceImpulseFactor = 1f / PoolConstants.SurfaceImpulseFactor;

        /// <summary>Horizontal velocity of the ball's cloth contact point.</summary>
        public static Vector3 ContactSlipVelocity(Vector3 linear, Vector3 angular, float radius)
        {
            Vector3 u = linear + Vector3.Cross(angular, new Vector3(0f, -radius, 0f));
            u.y = 0f;
            return u;
        }

        /// <summary>Angular velocity (horizontal components) of pure rolling for a horizontal velocity.</summary>
        public static Vector3 RollingAngularVelocity(Vector3 linear, float radius, float verticalSpin)
        {
            return new Vector3(linear.z / radius, verticalSpin, -linear.x / radius);
        }

        /// <summary>
        /// Advances the cloth interaction by <paramref name="dt"/>. Only horizontal velocity and
        /// angular velocity are modified; vertical velocity is left to the rigid-body solver.
        /// </summary>
        public static ClothMotionPhase Step(ref Vector3 linear, ref Vector3 angular, in ClothParameters p, float dt)
        {
            float radius = p.Radius;
            Vector3 horizontal = new Vector3(linear.x, 0f, linear.z);
            Vector3 slip = ContactSlipVelocity(linear, angular, radius);
            float slipSpeed = slip.magnitude;
            bool rolling;

            if (slipSpeed > p.SlipSpeedEpsilon)
            {
                float slipDecay = PoolConstants.SurfaceImpulseFactor * p.SlidingFriction * p.Gravity * dt;
                if (slipDecay >= slipSpeed)
                {
                    // Slip ends inside this step: apply the exact impulse J = -u m / 3.5 at the contact.
                    horizontal -= slip * InverseSurfaceImpulseFactor;
                    rolling = true;
                }
                else
                {
                    Vector3 slipDirection = slip / slipSpeed;
                    float deltaV = p.SlidingFriction * p.Gravity * dt;
                    horizontal -= slipDirection * deltaV;

                    // dw = (r x F) / I with r = -R up, F = -mu m g u_hat  =>  (mu g / (0.4 R)) (up x u_hat)
                    float deltaW = deltaV / (PoolConstants.SolidSphereInertiaFactor * radius);
                    Vector3 spinChange = Vector3.Cross(Vector3.up, slipDirection) * deltaW;
                    angular.x += spinChange.x;
                    angular.z += spinChange.z;
                    rolling = false;
                }
            }
            else
            {
                rolling = true;
            }

            if (rolling)
            {
                float speed = horizontal.magnitude;
                float newSpeed = speed - p.RollingResistance * p.Gravity * dt;
                if (newSpeed <= p.MinLinearSpeed)
                {
                    horizontal = Vector3.zero;
                }
                else
                {
                    horizontal *= newSpeed / speed;
                }

                angular.x = horizontal.z / radius;
                angular.z = -horizontal.x / radius;
            }

            angular.y = DecaySpin(angular.y, p, dt);

            linear.x = horizontal.x;
            linear.z = horizontal.z;

            if (!rolling)
            {
                return ClothMotionPhase.Sliding;
            }

            if (horizontal.sqrMagnitude > 0f)
            {
                return ClothMotionPhase.Rolling;
            }

            return angular.y != 0f ? ClothMotionPhase.Spinning : ClothMotionPhase.Stationary;
        }

        private static float DecaySpin(float spin, in ClothParameters p, float dt)
        {
            float magnitude = Mathf.Abs(spin);
            float decay = 2.5f * p.SpinFriction * p.Gravity / p.Radius * dt;
            magnitude -= decay;
            if (magnitude <= p.MinAngularSpeed)
            {
                return 0f;
            }

            return Mathf.Sign(spin) * magnitude;
        }
    }
}

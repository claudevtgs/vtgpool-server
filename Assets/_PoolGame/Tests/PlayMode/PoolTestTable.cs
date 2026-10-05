using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Cue;
using VTG.Pool.Simulation;
using VTG.Pool.Table;

namespace VTG.Pool.Tests
{
    /// <summary>
    /// Builds a self-contained table + physics system at runtime and steps it manually
    /// (Physics.simulationMode = Script) so physics tests are fast and repeatable.
    /// </summary>
    public sealed class PoolTestTable
    {
        public const float Dt = 1f / 120f;

        private readonly List<Object> owned = new List<Object>();
        private readonly SimulationMode previousMode;

        public GameObject Root { get; }
        public TableBuilder Table { get; }
        public PoolPhysicsSystem Physics { get; }
        public BallPhysicsProfile Profile { get; }
        public CueStrikeProfile StrikeProfile { get; }

        public float Radius => Profile.ballRadius;

        public PoolTestTable()
        {
            previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            Profile = Own(ScriptableObject.CreateInstance<BallPhysicsProfile>());
            Profile.ballMaterial = Own(new PhysicsMaterial("PM_Ball") { dynamicFriction = 0.06f, staticFriction = 0.06f, bounciness = 0.93f });
            StrikeProfile = Own(ScriptableObject.CreateInstance<CueStrikeProfile>());

            TableGeometry geometry = Own(ScriptableObject.CreateInstance<TableGeometry>());
            geometry.clothPhysicsMaterial = Own(new PhysicsMaterial("PM_Cloth")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            });
            geometry.cushionPhysicsMaterial = Own(new PhysicsMaterial("PM_Cushion")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            });

            // Built away from the origin so it can never overlap geometry of a scene loaded by another test.
            Root = new GameObject("PoolTestTable");
            Root.transform.position = new Vector3(40f, 0f, 0f);
            owned.Add(Root);
            Table = Root.AddComponent<TableBuilder>();
            Table.SetGeometry(geometry);
            Table.Build();

            var systemObject = new GameObject("PoolPhysicsSystem");
            systemObject.SetActive(false);
            systemObject.transform.SetParent(Root.transform);
            Physics = systemObject.AddComponent<PoolPhysicsSystem>();
            Physics.Configure(Profile, Table.PocketManager, Table.SurfaceHeight);
            systemObject.SetActive(true);
        }

        public PoolBall AddBall(int number, Vector2 tablePosition)
        {
            BallDefinition definition = Own(ScriptableObject.CreateInstance<BallDefinition>());
            definition.Initialize(number, BallDefinition.StandardColor(number), null);

            var ballObject = new GameObject($"Ball_{number:00}");
            ballObject.SetActive(false);
            ballObject.transform.SetParent(Root.transform);
            ballObject.AddComponent<Rigidbody>();
            ballObject.AddComponent<SphereCollider>();
            PoolBall ball = ballObject.AddComponent<PoolBall>();
            ball.SetDefinition(definition, Profile);
            ballObject.SetActive(true);
            ball.PlaceAt(Table.BallRestPosition(tablePosition, Radius));
            return ball;
        }

        public void Strike(PoolBall ball, Vector3 direction, float power, Vector2 tip, float elevation = 0f)
        {
            var shot = new ShotParameters(direction, power, tip, elevation);
            StrikeResult result = CueStrikeModel.Compute(shot, StrikeProfile.EvaluateCueSpeed(power), Profile.ballMass, Profile.ballRadius, StrikeProfile.ToSettings());
            ball.ApplyStrike(result.LinearVelocity, result.AngularVelocity);
        }

        public void Step(int steps = 1)
        {
            for (int i = 0; i < steps; i++)
            {
                Physics.Simulate(Dt);
            }
        }

        /// <summary>Steps until all balls are at rest; returns the simulated time, or -1 on timeout.</summary>
        public float RunUntilRest(float maxSeconds = 60f, System.Action perStep = null)
        {
            int maxSteps = Mathf.CeilToInt(maxSeconds / Dt);
            Step(2);
            for (int i = 0; i < maxSteps; i++)
            {
                Physics.Simulate(Dt);
                perStep?.Invoke();
                if (Physics.AllBallsAtRest)
                {
                    return (i + 3) * Dt;
                }
            }

            return -1f;
        }

        public Vector2 ToTable(Vector3 world)
        {
            Vector3 local = Root.transform.InverseTransformPoint(world);
            return new Vector2(local.x, local.z);
        }

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i] != null)
                {
                    Object.DestroyImmediate(owned[i]);
                }
            }

            owned.Clear();
            UnityEngine.Physics.simulationMode = previousMode;
        }

        private T Own<T>(T asset) where T : Object
        {
            owned.Add(asset);
            return asset;
        }
    }
}

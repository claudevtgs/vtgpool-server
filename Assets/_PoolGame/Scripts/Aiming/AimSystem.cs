using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Inputs;

namespace VTG.Pool.Aiming
{
    /// <summary>
    /// Owns the aim direction and draws the prediction (cue line, ghost ball, object-ball line,
    /// optional cue-ball tangent). First contact is found with a sphere cast of the ball radius.
    /// Renderers are created once and reused (no per-frame allocation).
    /// </summary>
    public sealed class AimSystem : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private AimAssistLevel assistLevel = AimAssistLevel.Training;
        [SerializeField] private float maxPredictionDistance = 4f;
        [SerializeField] private float objectLineLength = 0.35f;
        [SerializeField] private float tangentLineLength = 0.3f;
        [SerializeField] private float lineWidth = 0.004f;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color cueLineColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color objectLineColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        [SerializeField] private Color tangentLineColor = new Color(0.4f, 0.8f, 1f, 0.8f);

        private IShotInput input;
        private float yawDegrees;
        private LineRenderer cueLine;
        private LineRenderer objectLine;
        private LineRenderer tangentLine;
        private Transform ghostBall;
        private bool hasPrediction;

        /// <summary>Allows aim input (set by ShotController during Aiming/PowerSelection).</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Shows/hides all guides.</summary>
        public bool GuidesVisible { get; set; } = true;

        private readonly System.Collections.Generic.List<Vector3> curvedPath = new System.Collections.Generic.List<Vector3>(128);
        private bool curvedPathEndsAtBall;

        /// <summary>A predicted curved cue-ball path is shown instead of the straight guides (massé).</summary>
        public bool HasCurvedPath => curvedPath.Count >= 2;

        /// <summary>Shows a predicted curved path (world points) for the cue ball; the ghost ball is drawn at its end when it meets a ball.</summary>
        public void SetCurvedPath(System.Collections.Generic.IReadOnlyList<Vector3> points, bool endsAtBall)
        {
            curvedPath.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                curvedPath.Add(points[i]);
            }

            curvedPathEndsAtBall = endsAtBall;
        }

        public void ClearCurvedPath() => curvedPath.Clear();

        public PoolBall CueBall { get; set; }

        public AimAssistLevel AssistLevel
        {
            get => assistLevel;
            set => assistLevel = value;
        }

        public Vector3 AimDirection => Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;

        public float AimYawDegrees => yawDegrees;

        /// <summary>Latest prediction: ball that will be hit first (null if a cushion or nothing).</summary>
        public PoolBall PredictedTarget { get; private set; }

        public Vector3 PredictedGhostPosition { get; private set; }

        public Vector3 PredictedObjectDirection { get; private set; }

        public void SetInput(IShotInput shotInput)
        {
            input = shotInput;
        }

        public void SetAimDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 1e-8f)
            {
                yawDegrees = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            }
        }

        public void Rotate(float degrees)
        {
            yawDegrees = Mathf.Repeat(yawDegrees + degrees + 180f, 360f) - 180f;
        }

        private void Awake()
        {
            if (input == null && inputSource is IShotInput shotInput)
            {
                input = shotInput;
            }

            cueLine = CreateLine("AimLine_Cue", cueLineColor);
            objectLine = CreateLine("AimLine_Object", objectLineColor);
            tangentLine = CreateLine("AimLine_Tangent", tangentLineColor);
            CreateGhostBall();
        }

        private void Update()
        {
            if (InputEnabled && input != null)
            {
                Rotate(input.AimDeltaDegrees);
            }
        }

        private void LateUpdate()
        {
            hasPrediction = CueBall != null && !CueBall.IsPocketed && UpdatePrediction();
            RenderGuides();
        }

        private bool UpdatePrediction()
        {
            PredictedTarget = null;
            float radius = CueBall.Radius;
            Vector3 origin = CueBall.transform.position;
            Vector3 direction = AimDirection;

            // Slightly smaller radius so touching balls at the start are not reported at distance 0.
            if (!Physics.SphereCast(origin, radius * 0.98f, direction, out RaycastHit hit, maxPredictionDistance,
                    PoolLayers.AimPredictionMask, QueryTriggerInteraction.Ignore))
            {
                PredictedGhostPosition = origin + direction * maxPredictionDistance;
                return true;
            }

            Vector3 ghost = origin + direction * hit.distance;
            if (hit.collider.TryGetComponent(out PoolBall target))
            {
                Vector3 targetPosition = target.transform.position;
                if (GhostBallMath.TryGetGhostBall(origin, direction, targetPosition, radius, out Vector3 exactGhost))
                {
                    ghost = new Vector3(exactGhost.x, origin.y, exactGhost.z);
                }

                PredictedTarget = target;
                PredictedObjectDirection = GhostBallMath.ObjectBallDirection(ghost, targetPosition);
            }

            PredictedGhostPosition = ghost;
            return true;
        }

        private void RenderGuides()
        {
            bool show = GuidesVisible && hasPrediction && assistLevel != AimAssistLevel.None;
            cueLine.enabled = show;
            bool showGhost = show && assistLevel >= AimAssistLevel.Standard;
            ghostBall.gameObject.SetActive(showGhost);
            objectLine.enabled = showGhost && PredictedTarget != null;
            tangentLine.enabled = show && assistLevel >= AimAssistLevel.Training && PredictedTarget != null;
            if (!show)
            {
                return;
            }

            Vector3 origin = CueBall.transform.position;
            Vector3 lift = Vector3.up * 0.0005f;
            if (HasCurvedPath)
            {
                // Massé: the straight ghost-ball prediction does not apply.
                cueLine.positionCount = curvedPath.Count;
                for (int i = 0; i < curvedPath.Count; i++)
                {
                    cueLine.SetPosition(i, curvedPath[i] + lift);
                }

                objectLine.enabled = false;
                tangentLine.enabled = false;
                ghostBall.gameObject.SetActive(showGhost && curvedPathEndsAtBall);
                ghostBall.position = curvedPath[curvedPath.Count - 1];
                ghostBall.localScale = Vector3.one * (CueBall.Radius * 2f);
                return;
            }

            Vector3 ghost = PredictedGhostPosition;
            cueLine.positionCount = 2;
            cueLine.SetPosition(0, origin + lift);
            cueLine.SetPosition(1, ghost + lift);

            if (showGhost)
            {
                ghostBall.position = ghost;
                ghostBall.localScale = Vector3.one * (CueBall.Radius * 2f);
            }

            if (PredictedTarget != null)
            {
                Vector3 targetPosition = PredictedTarget.transform.position;
                float objectLength = assistLevel >= AimAssistLevel.Training ? objectLineLength * 2f : objectLineLength;
                objectLine.SetPosition(0, targetPosition + lift);
                objectLine.SetPosition(1, targetPosition + PredictedObjectDirection * objectLength + lift);

                Vector3 tangent = GhostBallMath.CueBallTangentDirection(AimDirection, PredictedObjectDirection);
                tangentLine.SetPosition(0, ghost + lift);
                tangentLine.SetPosition(1, ghost + tangent * tangentLineLength + lift);
            }
        }

        private LineRenderer CreateLine(string lineName, Color color)
        {
            var lineObject = new GameObject(lineName);
            lineObject.layer = PoolLayers.AimHelper;
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.enabled = false;
            return line;
        }

        private void CreateGhostBall()
        {
            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ghost.name = "GhostBall";
            ghost.layer = PoolLayers.AimHelper;
            Destroy(ghost.GetComponent<Collider>());
            ghost.transform.SetParent(transform, false);
            MeshRenderer renderer = ghost.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (ghostMaterial != null)
            {
                renderer.sharedMaterial = ghostMaterial;
            }

            ghostBall = ghost.transform;
            ghost.SetActive(false);
        }
    }
}

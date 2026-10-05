using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Core;

namespace VTG.Pool.Table
{
    /// <summary>
    /// Builds table colliders (slate, cushions, jaws, rails, pocket liners) and placeholder visuals
    /// from a <see cref="TableGeometry"/>. Works in the editor and at runtime (PlayMode tests).
    /// All generated objects live under a "Generated" child and are rebuilt from scratch.
    /// </summary>
    public sealed class TableBuilder : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated";
        private const int LinerSegments = 12;
        private const float JawThickness = 0.03f;

        [SerializeField] private TableGeometry geometry;
        [SerializeField] private PocketManager pocketManager;
        [SerializeField] private BoxCollider bedCollider;

        public TableGeometry Geometry => geometry;

        public Collider BedCollider => bedCollider;

        public PocketManager PocketManager => pocketManager;

        public float SurfaceHeight => transform.position.y + (geometry != null ? geometry.surfaceHeight : 0.76f);

        /// <summary>Centre of the cloth surface in world space.</summary>
        public Vector3 SurfaceCenter => transform.TransformPoint(new Vector3(0f, geometry.surfaceHeight, 0f));

        /// <summary>Head spot (centre of the head half, -Z) on the cloth.</summary>
        public Vector3 HeadSpot => TablePointToWorld(new Vector2(0f, -geometry.playLength * 0.25f));

        /// <summary>Foot spot (rack apex, +Z) on the cloth.</summary>
        public Vector3 FootSpot => TablePointToWorld(new Vector2(0f, geometry.playLength * 0.25f));

        public void SetGeometry(TableGeometry newGeometry)
        {
            geometry = newGeometry;
        }

        /// <summary>Converts table coordinates (x across, y along the long axis) to a world point on the cloth.</summary>
        public Vector3 TablePointToWorld(Vector2 tablePoint)
        {
            return transform.TransformPoint(new Vector3(tablePoint.x, geometry.surfaceHeight, tablePoint.y));
        }

        /// <summary>World position of a resting ball centre at a table coordinate.</summary>
        public Vector3 BallRestPosition(Vector2 tablePoint, float ballRadius)
        {
            return TablePointToWorld(tablePoint) + Vector3.up * ballRadius;
        }

        [ContextMenu("Rebuild Table")]
        public void Build()
        {
            if (geometry == null)
            {
                Debug.LogError("[TableBuilder] No TableGeometry assigned.", this);
                return;
            }

            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            Transform root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);

            TableGeometry g = geometry;
            float s = g.surfaceHeight;
            float cd = g.cushionDepth;
            float rw = g.railWidth;

            // Slate bed (cloth surface). Pocket holes are created by per-ball collision filtering.
            bedCollider = CreateBox("Bed", root, new Vector3(0f, s - g.bedThickness * 0.5f, 0f),
                new Vector3(g.playWidth + 2f * (cd + rw), g.bedThickness, g.playLength + 2f * (cd + rw)),
                Quaternion.identity, PoolLayers.Table, g.clothMaterial, g.clothPhysicsMaterial, true).GetComponent<BoxCollider>();

            BuildCushions(root, g);
            BuildJaws(root, g);
            BuildRails(root, g);
            List<Pocket> pockets = BuildPockets(root, g);
            BuildApron(root, g);

            if (pocketManager == null)
            {
                pocketManager = GetComponent<PocketManager>();
            }

            if (pocketManager == null)
            {
                pocketManager = gameObject.AddComponent<PocketManager>();
            }

            pocketManager.Configure(pockets, bedCollider, SurfaceHeight);
        }

        private void BuildCushions(Transform root, TableGeometry g)
        {
            float hw = g.HalfWidth;
            float hl = g.HalfLength;
            float d = g.CornerJawSetback;
            float halfSide = g.sideMouth * 0.5f;
            float y = g.surfaceHeight + g.cushionColliderHeight * 0.5f;
            float cd = g.cushionDepth;

            Transform parent = CreateGroup("Cushions", root);
            int index = 0;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float x = sx * (hw + cd * 0.5f);
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    // Long-rail segment between the corner jaw and the side jaw.
                    float zStart = sz * halfSide;
                    float zEnd = sz * (hl - d);
                    float length = Mathf.Abs(zEnd - zStart);
                    Vector3 center = new Vector3(x, y, (zStart + zEnd) * 0.5f);
                    CreateCushion($"Cushion_Long_{index++}", parent, center, new Vector3(cd, g.cushionColliderHeight, length), g);
                }
            }

            for (int sz = -1; sz <= 1; sz += 2)
            {
                float z = sz * (hl + cd * 0.5f);
                float length = g.playWidth - 2f * d;
                CreateCushion($"Cushion_Short_{index++}", parent, new Vector3(0f, y, z), new Vector3(length, g.cushionColliderHeight, cd), g);
            }
        }

        private void CreateCushion(string name, Transform parent, Vector3 center, Vector3 size, TableGeometry g)
        {
            GameObject cushion = CreateBox(name, parent, center, size, Quaternion.identity, PoolLayers.Cushion, g.cushionMaterial, g.cushionPhysicsMaterial, true);
            cushion.AddComponent<CushionSurface>().Configure(CushionSurfaceKind.Rail, 1f, 1f, g.cushionNoseHeight);
        }

        private void BuildJaws(Transform root, TableGeometry g)
        {
            float hw = g.HalfWidth;
            float hl = g.HalfLength;
            float d = g.CornerJawSetback;
            float halfSide = g.sideMouth * 0.5f;
            Transform parent = CreateGroup("Jaws", root);
            int index = 0;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    // Corner pocket: one jaw on the long rail, one on the short rail.
                    CreateJaw($"Jaw_Corner_{index++}", parent, g, new Vector3(sx * hw, 0f, sz * (hl - d)),
                        new Vector3(0f, 0f, sz), new Vector3(sx, 0f, 0f), g.cornerJawAngle);
                    CreateJaw($"Jaw_Corner_{index++}", parent, g, new Vector3(sx * (hw - d), 0f, sz * hl),
                        new Vector3(sx, 0f, 0f), new Vector3(0f, 0f, sz), g.cornerJawAngle);

                    // Side pocket: jaw at each side of the mouth (toward the pocket is -sz).
                    CreateJaw($"Jaw_Side_{index++}", parent, g, new Vector3(sx * hw, 0f, sz * halfSide),
                        new Vector3(0f, 0f, -sz), new Vector3(sx, 0f, 0f), g.sideJawAngle);
                }
            }
        }

        /// <param name="noseJawPoint">Jaw point on the cushion nose line (cloth plane, y ignored).</param>
        /// <param name="towardPocket">Unit vector along the rail pointing toward the pocket.</param>
        /// <param name="backward">Unit vector pointing from the nose line into the rail.</param>
        /// <param name="interiorAngle">Angle between the cushion nose and the jaw face.</param>
        private void CreateJaw(string name, Transform parent, TableGeometry g, Vector3 noseJawPoint, Vector3 towardPocket, Vector3 backward, float interiorAngle)
        {
            float radians = interiorAngle * Mathf.Deg2Rad;
            float alongPocket = -Mathf.Cos(radians);
            float alongBack = Mathf.Sin(radians);
            Vector3 faceDirection = (towardPocket * alongPocket + backward * alongBack).normalized;
            Vector3 openingNormal = (towardPocket * alongBack - backward * alongPocket).normalized;

            float length = g.cushionDepth / Mathf.Max(0.2f, alongBack) + 0.015f;
            Vector3 center = noseJawPoint + faceDirection * (length * 0.5f) - openingNormal * (JawThickness * 0.5f);
            center.y = g.surfaceHeight + g.cushionColliderHeight * 0.5f;

            GameObject jaw = CreateBox(name, parent, center, new Vector3(JawThickness, g.cushionColliderHeight, length),
                Quaternion.LookRotation(faceDirection, Vector3.up), PoolLayers.Cushion, g.cushionMaterial, g.cushionPhysicsMaterial, true);
            jaw.AddComponent<CushionSurface>().Configure(CushionSurfaceKind.Jaw, g.jawRestitutionScale, g.jawFrictionScale, g.cushionNoseHeight);
        }

        private void BuildRails(Transform root, TableGeometry g)
        {
            float hw = g.HalfWidth;
            float hl = g.HalfLength;
            float cd = g.cushionDepth;
            float rw = g.railWidth;
            float y = g.surfaceHeight + g.railHeight * 0.5f;
            Transform parent = CreateGroup("Rails", root);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                CreateRail($"Rail_Long_{(sx < 0 ? "L" : "R")}", parent, new Vector3(sx * (hw + cd + rw * 0.5f), y, 0f),
                    new Vector3(rw, g.railHeight, g.playLength + 2f * (cd + rw)), g);
            }

            for (int sz = -1; sz <= 1; sz += 2)
            {
                CreateRail($"Rail_Short_{(sz < 0 ? "Head" : "Foot")}", parent, new Vector3(0f, y, sz * (hl + cd + rw * 0.5f)),
                    new Vector3(g.playWidth + 2f * cd, g.railHeight, rw), g);
            }
        }

        private void CreateRail(string name, Transform parent, Vector3 center, Vector3 size, TableGeometry g)
        {
            GameObject rail = CreateBox(name, parent, center, size, Quaternion.identity, PoolLayers.Cushion, g.railMaterial, g.cushionPhysicsMaterial, true);
            rail.AddComponent<CushionSurface>().Configure(CushionSurfaceKind.PocketBack, g.pocketBackRestitutionScale, g.pocketBackFrictionScale, g.cushionNoseHeight);
        }

        private List<Pocket> BuildPockets(Transform root, TableGeometry g)
        {
            float hw = g.HalfWidth;
            float hl = g.HalfLength;
            Transform parent = CreateGroup("Pockets", root);
            var pockets = new List<Pocket>(6);
            int index = 0;

            for (int sz = -1; sz <= 1; sz += 2)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    Vector3 center = new Vector3(sx * (hw + g.cornerHoleOffset), g.surfaceHeight, sz * (hl + g.cornerHoleOffset));
                    pockets.Add(CreatePocket($"Pocket_Corner_{index}", parent, center, PocketKind.Corner, index, g.cornerHoleRadius, g));
                    index++;
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 center = new Vector3(sx * (hw + g.sideHoleOffset), g.surfaceHeight, 0f);
                pockets.Add(CreatePocket($"Pocket_Side_{index}", parent, center, PocketKind.Side, index, g.sideHoleRadius, g));
                index++;
            }

            return pockets;
        }

        private Pocket CreatePocket(string name, Transform parent, Vector3 center, PocketKind kind, int index, float radius, TableGeometry g)
        {
            var pocketObject = new GameObject(name);
            pocketObject.layer = PoolLayers.Pocket;
            pocketObject.transform.SetParent(parent, false);
            pocketObject.transform.localPosition = center;
            Pocket pocket = pocketObject.AddComponent<Pocket>();
            pocket.Configure(kind, index, radius);

            // Liner: ring of boxes below the slate that guides dropping balls into the capture volume.
            float ringRadius = radius + g.linerThickness * 0.5f;
            float segmentLength = 2f * Mathf.PI * ringRadius / LinerSegments * 1.08f;
            float linerCenterY = -g.linerTopDrop - g.linerDepth * 0.5f;
            for (int i = 0; i < LinerSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / LinerSegments;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 localCenter = radial * ringRadius + Vector3.up * linerCenterY;
                CreateBox($"Liner_{i}", pocketObject.transform, localCenter, new Vector3(segmentLength, g.linerDepth, g.linerThickness),
                    Quaternion.LookRotation(radial, Vector3.up), PoolLayers.Pocket, g.pocketMaterial, g.cushionPhysicsMaterial, false);
            }

            CreateBox("Liner_Floor", pocketObject.transform, Vector3.up * (-g.linerTopDrop - g.linerDepth),
                new Vector3(radius * 2.4f, 0.01f, radius * 2.4f), Quaternion.identity, PoolLayers.Pocket, g.pocketMaterial, g.cushionPhysicsMaterial, false);

            // Hole visual: dark disc just above the cloth (visual only).
            GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hole.name = "HoleVisual";
            DestroyImmediate(hole.GetComponent<Collider>());
            hole.layer = PoolLayers.Table;
            hole.transform.SetParent(pocketObject.transform, false);
            hole.transform.localPosition = Vector3.up * 0.0006f;
            hole.transform.localScale = new Vector3(radius * 2f, 0.0005f, radius * 2f);
            ApplyMaterial(hole, g.pocketMaterial);
            return pocket;
        }

        private void BuildApron(Transform root, TableGeometry g)
        {
            float outerWidth = g.playWidth + 2f * (g.cushionDepth + g.railWidth);
            float outerLength = g.playLength + 2f * (g.cushionDepth + g.railWidth);
            const float apronDepth = 0.2f;
            GameObject apron = CreateBox("Apron", root, new Vector3(0f, g.surfaceHeight - g.bedThickness - apronDepth * 0.5f, 0f),
                new Vector3(outerWidth, apronDepth, outerLength), Quaternion.identity, PoolLayers.Environment, g.railMaterial, null, false);
            DestroyImmediate(apron.GetComponent<Collider>());
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localCenter, Vector3 size, Quaternion localRotation,
            int layer, Material visual, PhysicsMaterial physics, bool visible)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = layer;
            box.isStatic = true;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localCenter;
            box.transform.localRotation = localRotation;
            box.transform.localScale = size;

            BoxCollider collider = box.GetComponent<BoxCollider>();
            collider.sharedMaterial = physics;
            collider.contactOffset = 0.001f;

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (!visible)
            {
                renderer.enabled = false;
            }
            else
            {
                ApplyMaterial(box, visual);
            }

            return box;
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            if (material != null)
            {
                target.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}

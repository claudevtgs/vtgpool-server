using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Presentation;

namespace VTG.Pool.Table
{
    /// <summary>
    /// Presentation geometry for a table built by <see cref="TableBuilder"/>: cushion rubber with a real nose
    /// profile and jaw-angled ends, bevelled wood rails, metal pocket caps, diamond sights, turned legs and
    /// an apron. Purely visual: the physics colliders are untouched (their renderers are hidden).
    /// Built once in the editor/at startup; a few draw calls and a few thousand triangles in total.
    /// </summary>
    public sealed class TableDressing : MonoBehaviour
    {
        private const string RootName = "Dressing";

        [SerializeField] private TableBuilder table;
        [SerializeField] private Material clothMaterial;
        [SerializeField] private Material railMaterial;
        [SerializeField] private Material capMaterial;
        [SerializeField] private Material diamondMaterial;
        [SerializeField] private Material legMaterial;
        [SerializeField] private float railTopHeight = 0.056f;
        [SerializeField] private float apronDepth = 0.2f;
        [SerializeField] private float legHeight = 0.52f;

        public void Configure(TableBuilder builder, Material cloth, Material rail, Material cap, Material diamond, Material leg)
        {
            table = builder;
            clothMaterial = cloth;
            railMaterial = rail;
            capMaterial = cap;
            diamondMaterial = diamond;
            legMaterial = leg;
        }

        [ContextMenu("Rebuild Dressing")]
        public void Build()
        {
            if (table == null || table.Geometry == null)
            {
                return;
            }

            Transform existing = transform.Find(RootName);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject(RootName).transform;
            root.SetParent(transform, false);
            HideColliderRenderers();

            TableGeometry g = table.Geometry;
            BuildSurface(root, g);
            BuildCushions(root, g);
            BuildRails(root, g);
            BuildPocketCaps(root, g);
            BuildDiamonds(root, g);
            BuildLegs(root, g);
        }

        private void HideColliderRenderers()
        {
            Transform generated = table.transform.Find("Generated");
            if (generated == null)
            {
                return;
            }

            Transform bed = generated.Find("Bed");
            if (bed != null && bed.TryGetComponent(out MeshRenderer bedRenderer))
            {
                bedRenderer.enabled = false;
            }

            foreach (string group in new[] { "Cushions", "Jaws", "Rails" })
            {
                Transform child = generated.Find(group);
                if (child == null)
                {
                    continue;
                }

                foreach (MeshRenderer renderer in child.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.enabled = false;
                }
            }
        }

        // ------------------------------------------------------------------ surface

        /// <summary>
        /// Cloth over the playing area (and under the cushions) plus a dark base for the rest of the slab, which is
        /// only visible in the gaps around the pockets. Cloth UVs are in world metres x10 (one texture tile per 10 cm).
        /// </summary>
        private void BuildSurface(Transform root, TableGeometry g)
        {
            float clothHalfX = g.HalfWidth + g.cushionDepth;
            float clothHalfZ = g.HalfLength + g.cushionDepth;
            AddMesh(root, "Cloth", FlatQuad("ClothSurface", clothHalfX, clothHalfZ, g.surfaceHeight + 0.0002f, 10f), clothMaterial);

            float baseHalfX = clothHalfX + g.railWidth;
            float baseHalfZ = clothHalfZ + g.railWidth;
            AddMesh(root, "SlabBase", FlatQuad("SlabBase", baseHalfX, baseHalfZ, g.surfaceHeight - 0.0004f, 4f), capMaterial);
        }

        private static Mesh FlatQuad(string meshName, float halfX, float halfZ, float y, float uvPerMetre)
        {
            var mesh = new Mesh { name = meshName };
            mesh.vertices = new[]
            {
                new Vector3(-halfX, y, -halfZ), new Vector3(-halfX, y, halfZ), new Vector3(halfX, y, halfZ), new Vector3(halfX, y, -halfZ)
            };
            mesh.uv = new[]
            {
                new Vector2(-halfX, -halfZ) * uvPerMetre, new Vector2(-halfX, halfZ) * uvPerMetre,
                new Vector2(halfX, halfZ) * uvPerMetre, new Vector2(halfX, -halfZ) * uvPerMetre
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ cushions

        private void BuildCushions(Transform root, TableGeometry g)
        {
            float cd = g.cushionDepth;
            float nose = g.cushionNoseHeight;
            float top = g.cushionColliderHeight - 0.002f;

            // (depth outward from the nose line, height above the cloth); counter-clockwise seen from the start.
            var profile = new List<Vector2>
            {
                new Vector2(cd, 0f),
                new Vector2(0.009f, 0f),
                new Vector2(0f, nose),
                new Vector2(0.004f, nose + 0.006f),
                new Vector2(0.016f, top),
                new Vector2(cd, top)
            };

            float hw = g.HalfWidth;
            float hl = g.HalfLength;
            float d = g.CornerJawSetback;
            float halfSide = g.sideMouth * 0.5f;
            float cornerShift = JawShift(g.cornerJawAngle);
            float sideShift = JawShift(g.sideJawAngle);
            float s = g.surfaceHeight;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 outward = new Vector3(sx, 0f, 0f);
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    // From the side pocket toward the corner pocket.
                    Vector3 start = new Vector3(sx * hw, s, sz * halfSide);
                    Vector3 end = new Vector3(sx * hw, s, sz * (hl - d));
                    AddMesh(root, $"Cushion_Long_{sx}_{sz}", ProceduralMeshes.ExtrudeProfile("CushionLong", profile, start, end, outward, sideShift, cornerShift, 10f, true), clothMaterial);
                }
            }

            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 outward = new Vector3(0f, 0f, sz);
                Vector3 start = new Vector3(-(hw - d), s, sz * hl);
                Vector3 end = new Vector3(hw - d, s, sz * hl);
                AddMesh(root, $"Cushion_Short_{sz}", ProceduralMeshes.ExtrudeProfile("CushionShort", profile, start, end, outward, cornerShift, cornerShift, 10f, true), clothMaterial);
            }
        }

        /// <summary>How far the jaw face moves toward the pocket per metre of cushion depth.</summary>
        private static float JawShift(float interiorAngle)
        {
            float radians = interiorAngle * Mathf.Deg2Rad;
            return -Mathf.Cos(radians) / Mathf.Max(0.2f, Mathf.Sin(radians));
        }

        // ------------------------------------------------------------------ rails

        private void BuildRails(Transform root, TableGeometry g)
        {
            float cd = g.cushionDepth;
            float rw = g.railWidth;
            float s = g.surfaceHeight;
            var profile = new List<Vector2>
            {
                new Vector2(cd + rw, -apronDepth),
                new Vector2(cd + rw, railTopHeight - 0.014f),
                new Vector2(cd + rw - 0.006f, railTopHeight - 0.003f),
                new Vector2(cd + rw - 0.018f, railTopHeight),
                new Vector2(cd + 0.004f, railTopHeight),
                new Vector2(cd, railTopHeight - 0.004f),
                new Vector2(cd, g.cushionColliderHeight - 0.004f)
            };

            float hw = g.HalfWidth;
            float hl = g.HalfLength;
            float cornerGap = g.cornerHoleRadius + 0.02f;
            float sideGap = g.sideHoleRadius + 0.012f;
            Vector3 cornerCenter = new Vector3(hw + g.cornerHoleOffset, 0f, hl + g.cornerHoleOffset);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 outward = new Vector3(sx, 0f, 0f);
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 start = new Vector3(sx * hw, s, sz * sideGap);
                    Vector3 end = new Vector3(sx * hw, s, sz * (cornerCenter.z - cornerGap));
                    AddMesh(root, $"Rail_Long_{sx}_{sz}", ProceduralMeshes.ExtrudeProfile("RailLong", profile, start, end, outward, 0f, 0f, 2f, true), railMaterial);
                }
            }

            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 outward = new Vector3(0f, 0f, sz);
                Vector3 start = new Vector3(-(cornerCenter.x - cornerGap), s, sz * hl);
                Vector3 end = new Vector3(cornerCenter.x - cornerGap, s, sz * hl);
                AddMesh(root, $"Rail_Short_{sz}", ProceduralMeshes.ExtrudeProfile("RailShort", profile, start, end, outward, 0f, 0f, 2f, true), railMaterial);
            }
        }

        // ------------------------------------------------------------------ pockets

        private void BuildPocketCaps(Transform root, TableGeometry g)
        {
            if (table.PocketManager == null)
            {
                return;
            }

            foreach (Pocket pocket in table.PocketManager.Pockets)
            {
                Vector3 local = table.transform.InverseTransformPoint(pocket.Center);
                float outwardAngle = Mathf.Atan2(local.z, local.x);
                if (pocket.Kind == PocketKind.Side)
                {
                    outwardAngle = local.x > 0f ? 0f : Mathf.PI;
                }

                // Corner caps wrap 240 degrees around the outside; side caps 170 degrees.
                float halfSpan = (pocket.Kind == PocketKind.Corner ? 120f : 85f) * Mathf.Deg2Rad;
                float inner = pocket.HoleRadius + 0.003f;
                float outer = pocket.HoleRadius + (pocket.Kind == PocketKind.Corner ? 0.03f : 0.026f);
                Mesh mesh = ProceduralMeshes.AnnulusSector("PocketCap", inner, outer, outwardAngle - halfSpan, outwardAngle + halfSpan,
                    -0.03f, railTopHeight + 0.001f, 24);
                Transform cap = AddMesh(root, $"PocketCap_{pocket.PocketIndex}", mesh, capMaterial);
                cap.localPosition = new Vector3(local.x, g.surfaceHeight, local.z);
            }
        }

        // ------------------------------------------------------------------ sights and legs

        private void BuildDiamonds(Transform root, TableGeometry g)
        {
            float railCenter = g.cushionDepth + g.railWidth * 0.5f;
            float y = g.surfaceHeight + railTopHeight + 0.0004f;
            var sights = new List<Vector3>();
            for (int k = 1; k < 8; k++)
            {
                if (k == 4)
                {
                    continue;
                }

                float z = -g.HalfLength + g.playLength * k / 8f;
                sights.Add(new Vector3(g.HalfWidth + railCenter, y, z));
                sights.Add(new Vector3(-(g.HalfWidth + railCenter), y, z));
            }

            for (int k = 1; k < 4; k++)
            {
                float x = -g.HalfWidth + g.playWidth * k / 4f;
                sights.Add(new Vector3(x, y, g.HalfLength + railCenter));
                sights.Add(new Vector3(x, y, -(g.HalfLength + railCenter)));
            }

            Mesh disc = ProceduralMeshes.Lathe("Diamond", new[] { new LatheSection(0f, 0.0008f, 0.0065f, 0.006f) }, 16, 1f, 1);
            for (int i = 0; i < sights.Count; i++)
            {
                Transform sight = AddMesh(root, $"Diamond_{i}", disc, diamondMaterial);
                sight.localPosition = sights[i];
                sight.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                sight.localScale = new Vector3(1.4f, 1f, 1f);
                sight.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void BuildLegs(Transform root, TableGeometry g)
        {
            float x = g.HalfWidth + g.cushionDepth + g.railWidth * 0.35f;
            float z = g.HalfLength + g.cushionDepth + g.railWidth * 0.35f;
            float top = g.surfaceHeight - g.bedThickness - apronDepth;
            float height = Mathf.Min(legHeight, top);
            Mesh leg = ProceduralMeshes.CreateLeg(height, 0.055f);
            Vector3[] positions =
            {
                new Vector3(x, top, z), new Vector3(-x, top, z), new Vector3(x, top, -z), new Vector3(-x, top, -z),
                new Vector3(x, top, 0f), new Vector3(-x, top, 0f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                Transform legTransform = AddMesh(root, $"Leg_{i}", leg, legMaterial);
                legTransform.localPosition = positions[i];
            }
        }

        private Transform AddMesh(Transform parent, string objectName, Mesh mesh, Material material)
        {
            var meshObject = new GameObject(objectName);
            meshObject.layer = PoolLayers.Environment;
            meshObject.isStatic = true;
            meshObject.transform.SetParent(parent, false);
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return meshObject.transform;
        }
    }
}

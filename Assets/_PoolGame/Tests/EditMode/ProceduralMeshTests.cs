using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Presentation;

namespace VTG.Pool.Tests
{
    public sealed class ProceduralMeshTests
    {
        [Test]
        public void Cue_HasRealDimensionsAndEightMaterialSections()
        {
            Mesh cue = ProceduralMeshes.CreateCue();
            Assert.AreEqual(8, cue.subMeshCount);
            Bounds bounds = cue.bounds;
            Assert.AreEqual(-1.47f, bounds.min.z, 0.002f, "58 in cue extends 1.47 m behind the tip");
            Assert.AreEqual(0f, bounds.max.z, 0.002f, "Tip at the origin");
            Assert.AreEqual(0.0146f, bounds.max.x, 0.0005f, "Butt radius");
            Object.DestroyImmediate(cue);
        }

        [Test]
        public void Leg_HasRequestedHeight()
        {
            Mesh leg = ProceduralMeshes.CreateLeg(0.5f, 0.05f);
            Assert.AreEqual(0f, leg.bounds.max.y, 0.001f);
            Assert.AreEqual(-0.5f, leg.bounds.min.y, 0.001f);
            Object.DestroyImmediate(leg);
        }

        [Test]
        public void ExtrudedProfile_FacesPointOutward()
        {
            // Square profile 0..1 x 0..1, extruded along +Z: every face must point away from the centre.
            var profile = new List<Vector2> { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            Mesh mesh = ProceduralMeshes.ExtrudeProfile("Test", profile, Vector3.zero, new Vector3(0f, 0f, 2f), Vector3.right, 0f, 0f, 1f, true);
            AssertOutward(mesh, new Vector3(0.5f, 0.5f, 1f));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ExtrudedProfile_MirroredFrame_FacesPointOutward()
        {
            var profile = new List<Vector2> { new Vector2(0.05f, 0f), new Vector2(0.01f, 0f), new Vector2(0f, 0.036f), new Vector2(0.016f, 0.046f), new Vector2(0.05f, 0.046f) };
            Mesh mesh = ProceduralMeshes.ExtrudeProfile("Cushion", profile, new Vector3(0f, 0f, 1f), Vector3.zero, Vector3.left, 1.2f, 0.25f, 1f, true);
            AssertOutward(mesh, new Vector3(-0.025f, 0.023f, 0.5f));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void PocketCap_FacesPointOutwardFromItsVolume()
        {
            Mesh mesh = ProceduralMeshes.AnnulusSector("Cap", 0.07f, 0.12f, -1f, 1f, -0.1f, 0.05f, 16);
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Vector3 center = (a + b + c) / 3f;
                // Nearest point inside the volume: radial middle at the same angle, mid height.
                float angle = Mathf.Atan2(center.z, center.x);
                Vector3 inside = new Vector3(Mathf.Cos(angle) * 0.095f, -0.025f, Mathf.Sin(angle) * 0.095f);
                Assert.Greater(Vector3.Dot(normal, center - inside), -1e-4f, $"Triangle {i / 3} faces inward");
            }

            Object.DestroyImmediate(mesh);
        }

        private static void AssertOutward(Mesh mesh, Vector3 interior)
        {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Assert.Greater(triangles.Length, 0);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                {
                    continue;
                }

                Vector3 center = (a + b + c) / 3f;
                Assert.Greater(Vector3.Dot(normal, center - interior), 0f, $"Triangle {i / 3} faces inward");
            }
        }
    }
}

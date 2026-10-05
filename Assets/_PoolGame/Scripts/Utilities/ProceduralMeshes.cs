using System.Collections.Generic;
using UnityEngine;

namespace VTG.Pool.Presentation
{
    /// <summary>Radius of a lathe section at a distance along its axis.</summary>
    public struct LatheSection
    {
        public float Start;
        public float End;
        public float StartRadius;
        public float EndRadius;

        public LatheSection(float start, float end, float startRadius, float endRadius)
        {
            Start = start;
            End = end;
            StartRadius = startRadius;
            EndRadius = endRadius;
        }
    }

    /// <summary>
    /// Small procedural mesh toolkit for presentation geometry (cue, legs, cushions, rails, pocket caps).
    /// Meshes are built once (setup or Awake), never per frame.
    /// </summary>
    public static class ProceduralMeshes
    {
        /// <summary>
        /// Builds a lathe mesh along -Z (start at z = 0 going toward -Z), one sub-mesh per section so each
        /// section can have its own material. Ends are capped.
        /// </summary>
        public static Mesh Lathe(string meshName, IReadOnlyList<LatheSection> sections, int sides, float uvLengthScale, int ringsPerMeter = 30)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var subMeshes = new List<List<int>>();

            for (int s = 0; s < sections.Count; s++)
            {
                LatheSection section = sections[s];
                var triangles = new List<int>();
                int rings = Mathf.Max(1, Mathf.CeilToInt((section.End - section.Start) * ringsPerMeter));
                float slope = (section.EndRadius - section.StartRadius) / Mathf.Max(1e-5f, section.End - section.Start);
                int baseIndex = vertices.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float t = (float)r / rings;
                    float z = Mathf.Lerp(section.Start, section.End, t);
                    float radius = Mathf.Lerp(section.StartRadius, section.EndRadius, t);
                    for (int i = 0; i <= sides; i++)
                    {
                        float angle = i * Mathf.PI * 2f / sides;
                        var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                        vertices.Add(radial * radius + Vector3.back * z);
                        normals.Add((radial + Vector3.forward * slope).normalized);
                        uvs.Add(new Vector2((float)i / sides, z * uvLengthScale));
                    }
                }

                int stride = sides + 1;
                for (int r = 0; r < rings; r++)
                {
                    for (int i = 0; i < sides; i++)
                    {
                        int a = baseIndex + r * stride + i;
                        int b = a + stride;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                        triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                    }
                }

                if (s == 0)
                {
                    AddCap(vertices, normals, uvs, triangles, section.Start, section.StartRadius, sides, Vector3.forward);
                }

                if (s == sections.Count - 1)
                {
                    AddCap(vertices, normals, uvs, triangles, section.End, section.EndRadius, sides, Vector3.back);
                }

                subMeshes.Add(triangles);
            }

            return Finish(meshName, vertices, normals, uvs, subMeshes, false);
        }

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, float z, float radius, int sides, Vector3 normal)
        {
            int center = vertices.Count;
            vertices.Add(Vector3.back * z);
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -z));
                normals.Add(normal);
                uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }

            bool facesForward = normal.z > 0f;
            for (int i = 0; i < sides; i++)
            {
                if (facesForward)
                {
                    triangles.Add(center); triangles.Add(center + 2 + i); triangles.Add(center + 1 + i);
                }
                else
                {
                    triangles.Add(center); triangles.Add(center + 1 + i); triangles.Add(center + 2 + i);
                }
            }
        }

        /// <summary>
        /// Extrudes a 2D profile (x = depth outward from the nose line, y = height) between two end frames.
        /// Each profile point is placed at <c>origin + outward * x + up * y + along * (length-axis position)</c>,
        /// where the start/end positions along the axis may shift linearly with depth (angled/mitred ends).
        /// </summary>
        public static Mesh ExtrudeProfile(string meshName, IReadOnlyList<Vector2> profile, Vector3 startOrigin, Vector3 endOrigin, Vector3 outward,
            float startShiftPerDepth, float endShiftPerDepth, float uvScale, bool capEnds)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 along = (endOrigin - startOrigin).normalized;
            float length = Vector3.Distance(startOrigin, endOrigin);

            Vector3 StartPoint(Vector2 p) => startOrigin + outward * p.x + Vector3.up * p.y - along * (p.x * startShiftPerDepth);
            Vector3 EndPoint(Vector2 p) => endOrigin + outward * p.x + Vector3.up * p.y + along * (p.x * endShiftPerDepth);

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < profile.Count; i++)
            {
                centroid += profile[i];
            }

            centroid /= Mathf.Max(1, profile.Count);
            float arc = 0f;
            for (int i = 0; i < profile.Count - 1; i++)
            {
                Vector2 p0 = profile[i];
                Vector2 p1 = profile[i + 1];
                float segment = Vector2.Distance(p0, p1);
                int index = vertices.Count;
                vertices.Add(StartPoint(p0));
                vertices.Add(EndPoint(p0));
                vertices.Add(StartPoint(p1));
                vertices.Add(EndPoint(p1));
                uvs.Add(new Vector2(0f, arc * uvScale));
                uvs.Add(new Vector2(length * uvScale, arc * uvScale));
                uvs.Add(new Vector2(0f, (arc + segment) * uvScale));
                uvs.Add(new Vector2(length * uvScale, (arc + segment) * uvScale));
                AddQuad(triangles, index, index + 1, index + 2, index + 3, vertices, outward, p0, p1, centroid);
                arc += segment;
            }

            if (capEnds)
            {
                AddProfileCap(vertices, uvs, triangles, profile, StartPoint, -along, uvScale);
                AddProfileCap(vertices, uvs, triangles, profile, EndPoint, along, uvScale);
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds a quad strip, choosing winding so the face points away from the profile centroid.</summary>
        private static void AddQuad(List<int> triangles, int s0, int e0, int s1, int e1, List<Vector3> vertices, Vector3 outward, Vector2 p0, Vector2 p1, Vector2 centroid)
        {
            Vector2 edge = p1 - p0;
            Vector2 outside2D = new Vector2(edge.y, -edge.x);
            if (Vector2.Dot(outside2D, (p0 + p1) * 0.5f - centroid) < 0f)
            {
                outside2D = -outside2D;
            }

            Vector3 outside = outward * outside2D.x + Vector3.up * outside2D.y;
            Vector3 normal = Vector3.Cross(vertices[e0] - vertices[s0], vertices[s1] - vertices[s0]);
            if (Vector3.Dot(normal, outside) >= 0f)
            {
                triangles.Add(s0); triangles.Add(e0); triangles.Add(s1);
                triangles.Add(e0); triangles.Add(e1); triangles.Add(s1);
            }
            else
            {
                triangles.Add(s0); triangles.Add(s1); triangles.Add(e0);
                triangles.Add(e0); triangles.Add(s1); triangles.Add(e1);
            }
        }

        private static void AddProfileCap(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, IReadOnlyList<Vector2> profile,
            System.Func<Vector2, Vector3> place, Vector3 facing, float uvScale)
        {
            int start = vertices.Count;
            for (int i = 0; i < profile.Count; i++)
            {
                vertices.Add(place(profile[i]));
                uvs.Add(profile[i] * uvScale);
            }

            for (int i = 1; i < profile.Count - 1; i++)
            {
                Vector3 normal = Vector3.Cross(vertices[start + i] - vertices[start], vertices[start + i + 1] - vertices[start]);
                if (Vector3.Dot(normal, facing) >= 0f)
                {
                    triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1);
                }
                else
                {
                    triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + i);
                }
            }
        }

        /// <summary>Solid annulus sector (pocket cap): inner/outer radius, angle range (radians, around Y), height range.</summary>
        public static Mesh AnnulusSector(string meshName, float innerRadius, float outerRadius, float fromAngle, float toAngle, float bottom, float top, int segments)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            Vector3 Point(float radius, float angle, float y) => new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);

            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(fromAngle, toAngle, (float)i / segments);
                float a1 = Mathf.Lerp(fromAngle, toAngle, (float)(i + 1) / segments);
                float u0 = (float)i / segments;
                float u1 = (float)(i + 1) / segments;
                // top
                Quad(vertices, uvs, triangles, Point(innerRadius, a0, top), Point(innerRadius, a1, top), Point(outerRadius, a0, top), Point(outerRadius, a1, top), u0, u1, Vector3.up);
                // outer wall
                Quad(vertices, uvs, triangles, Point(outerRadius, a0, top), Point(outerRadius, a1, top), Point(outerRadius, a0, bottom), Point(outerRadius, a1, bottom), u0, u1,
                    new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f)));
                // inner wall (faces the hole)
                Quad(vertices, uvs, triangles, Point(innerRadius, a0, top), Point(innerRadius, a1, top), Point(innerRadius, a0, bottom), Point(innerRadius, a1, bottom), u0, u1,
                    -new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f)));
            }

            // end caps
            foreach (float angle in new[] { fromAngle, toAngle })
            {
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (angle == fromAngle ? -1f : 1f);
                Quad(vertices, uvs, triangles, Point(innerRadius, angle, top), Point(outerRadius, angle, top), Point(innerRadius, angle, bottom), Point(outerRadius, angle, bottom), 0f, 1f, tangent);
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds a quad (a0, a1 along the first edge; b0, b1 along the second) facing <paramref name="facing"/>.</summary>
        private static void Quad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float u0, float u1, Vector3 facing)
        {
            int i = vertices.Count;
            vertices.Add(a0); vertices.Add(a1); vertices.Add(b0); vertices.Add(b1);
            uvs.Add(new Vector2(u0, 1f)); uvs.Add(new Vector2(u1, 1f)); uvs.Add(new Vector2(u0, 0f)); uvs.Add(new Vector2(u1, 0f));
            Vector3 normal = Vector3.Cross(a1 - a0, b0 - a0);
            if (Vector3.Dot(normal, facing) >= 0f)
            {
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i + 1); triangles.Add(i + 3); triangles.Add(i + 2);
            }
            else
            {
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i + 3);
            }
        }

        private static Mesh Finish(string meshName, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<List<int>> subMeshes, bool recalculateNormals)
        {
            var mesh = new Mesh { name = meshName };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subMeshes.Count;
            for (int i = 0; i < subMeshes.Count; i++)
            {
                mesh.SetTriangles(subMeshes[i], i);
            }

            if (recalculateNormals)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Standard 58 in (1.47 m) cue, tip at z = 0 extending along -Z. Sub-meshes (in order):
        /// tip, ferrule, shaft, joint, forearm, wrap, sleeve, bumper.
        /// </summary>
        public static Mesh CreateCue(int sides = 24)
        {
            var sections = new[]
            {
                new LatheSection(0f, 0.011f, 0.0062f, 0.0065f),      // leather tip
                new LatheSection(0.011f, 0.034f, 0.0065f, 0.0066f),  // ferrule
                new LatheSection(0.034f, 0.735f, 0.0066f, 0.0098f),  // maple shaft (pro taper approximated linearly)
                new LatheSection(0.735f, 0.762f, 0.0104f, 0.0104f),  // joint collar
                new LatheSection(0.762f, 1.06f, 0.0104f, 0.0124f),   // forearm
                new LatheSection(1.06f, 1.31f, 0.0129f, 0.0131f),    // wrap
                new LatheSection(1.31f, 1.455f, 0.0131f, 0.0146f),   // butt sleeve
                new LatheSection(1.455f, 1.47f, 0.0146f, 0.0140f)    // bumper
            };
            return Lathe("Cue", sections, sides, 2f);
        }

        /// <summary>Turned table leg (pointing down from y = 0 to y = -height) with a foot.</summary>
        public static Mesh CreateLeg(float height, float radius)
        {
            var sections = new[]
            {
                new LatheSection(0f, height * 0.08f, radius * 1.15f, radius * 1.15f),
                new LatheSection(height * 0.08f, height * 0.85f, radius, radius * 0.75f),
                new LatheSection(height * 0.85f, height * 0.94f, radius * 0.75f, radius * 1.1f),
                new LatheSection(height * 0.94f, height, radius * 1.1f, radius * 1.05f)
            };
            Mesh mesh = Lathe("TableLeg", sections, 20, 1.5f, 12);

            // Lathe runs along -Z; rotate to run along -Y.
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Quaternion rotate = Quaternion.Euler(-90f, 0f, 0f);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotate * vertices[i];
                normals[i] = rotate * normals[i];
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

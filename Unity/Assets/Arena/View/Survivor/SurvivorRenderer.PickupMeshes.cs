using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>Meshes built in code for the pickups: gem, flask, magnet and their helpers.</summary>
    public sealed partial class SurvivorRenderer
    {
        private static Mesh BuiltinMesh(PrimitiveType type)
        {
            GameObject temporary = GameObject.CreatePrimitive(type);
            Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            DestroyUnityObject(temporary);
            return mesh;
        }

        /// <summary>Flat-shaded elongated octahedron (unit height), the classic EXP crystal.</summary>
        private static Mesh BuildGemMesh()
        {
            Vector3 top = new Vector3(0f, 0.5f, 0f);
            Vector3 bottom = new Vector3(0f, -0.5f, 0f);
            Vector3[] ring =
            {
                new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f)
            };
            List<Vector3> vertices = new List<Vector3>(24);
            List<int> triangles = new List<int>(24);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % 4];
                AddTriangle(vertices, triangles, top, b, a);
                AddTriangle(vertices, triangles, bottom, a, b);
            }
            Mesh mesh = new Mesh { name = "Gem" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Outline of a potion flask from the bottom centre up to the lip: (radius, height).
        private static readonly Vector2[] FlaskProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.3f, 0.02f), new Vector2(0.42f, 0.2f), new Vector2(0.38f, 0.42f),
            new Vector2(0.17f, 0.6f), new Vector2(0.13f, 0.8f), new Vector2(0.2f, 0.84f), new Vector2(0.2f, 0.9f),
            new Vector2(0f, 0.9f)
        };

        /// <summary>Turns an outline (radius, height) around the vertical axis; smooth shaded, centred on its height.</summary>
        private static Mesh BuildLatheMesh(string meshName, Vector2[] profile, int segments)
        {
            float middle = profile[profile.Length - 1].y * 0.5f;
            Vector3[] vertices = new Vector3[profile.Length * segments];
            List<int> triangles = new List<int>((profile.Length - 1) * segments * 6);
            for (int i = 0; i < profile.Length; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    float angle = j * Mathf.PI * 2f / segments;
                    vertices[i * segments + j] = new Vector3(profile[i].x * Mathf.Cos(angle), profile[i].y - middle, profile[i].x * Mathf.Sin(angle));
                }
            }
            for (int i = 0; i < profile.Length - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int next = (j + 1) % segments;
                    int a = i * segments + j;
                    int b = (i + 1) * segments + j;
                    int c = (i + 1) * segments + next;
                    int d = i * segments + next;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }
            Mesh mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A horseshoe magnet standing with its two poles up: the red U, or (tips) the two silver pole ends.</summary>
        private static Mesh BuildMagnetMesh(bool tips)
        {
            const float radius = 0.26f;
            const float thick = 0.095f;
            List<Vector3> vertices = new List<Vector3>(360);
            List<int> triangles = new List<int>(360);
            if (tips)
            {
                AddBox(vertices, triangles, new Vector3(-radius, 0.27f, 0f), new Vector3(thick + 0.004f, 0.08f, thick + 0.004f), Quaternion.identity);
                AddBox(vertices, triangles, new Vector3(radius, 0.27f, 0f), new Vector3(thick + 0.004f, 0.08f, thick + 0.004f), Quaternion.identity);
            }
            else
            {
                const int bends = 7;
                for (int i = 0; i < bends; i++)
                {
                    float angle = Mathf.PI + (i + 0.5f) * Mathf.PI / bends;
                    Vector3 center = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 0.05f, 0f);
                    float half = radius * Mathf.Tan(Mathf.PI / bends * 0.5f) + thick * 0.45f;
                    AddBox(vertices, triangles, center, new Vector3(half, thick, thick), Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f));
                }
                AddBox(vertices, triangles, new Vector3(-radius, 0.07f, 0f), new Vector3(thick, 0.125f, thick), Quaternion.identity);
                AddBox(vertices, triangles, new Vector3(radius, 0.07f, 0f), new Vector3(thick, 0.125f, thick), Quaternion.identity);
            }
            Mesh mesh = new Mesh { name = tips ? "Magnet Tips" : "Magnet" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 half, Quaternion rotation)
        {
            Vector3[] axes = { rotation * Vector3.right * half.x, rotation * Vector3.up * half.y, rotation * Vector3.forward * half.z };
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 normal = axes[axis];
                Vector3 u = axes[(axis + 1) % 3];
                Vector3 v = axes[(axis + 2) % 3];
                AddQuad(vertices, triangles, center + normal, u, v);
                AddQuad(vertices, triangles, center - normal, v, u);
            }
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 u, Vector3 v)
        {
            Vector3 a = center - u - v;
            Vector3 b = center + u - v;
            Vector3 c = center + u + v;
            Vector3 d = center - u + v;
            AddTriangle(vertices, triangles, a, b, c);
            AddTriangle(vertices, triangles, a, c, d);
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}

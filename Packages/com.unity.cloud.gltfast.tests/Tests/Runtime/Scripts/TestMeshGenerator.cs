// SPDX-FileCopyrightText: 2025 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace GLTFast.Tests
{
    static class TestMeshGenerator
    {
        internal static Mesh GenerateCylinderMesh(uint triangleCount, float height = 1f, float radius = .5f)
        {
            triangleCount += triangleCount % 2;
            Assert.IsTrue(triangleCount >= 6);
            var m = new Mesh
            {
                name = "Cylinder"
            };

            // Arrays to hold mesh data
            var vertexCount = triangleCount + 2;
            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var indices = new int[triangleCount * 3];

            var topAngleIncrement = 2 * math.PI / triangleCount;

            for (var i = 0; i < triangleCount; i++)
            {
                var top = (i & 1) == 0;
                float y;
                indices[i * 3] = i;
                var angle = i * topAngleIncrement;
                if (top)
                {
                    y = 1;
                    indices[i * 3 + 1] = i + 2;
                    indices[i * 3 + 2] = i + 1;
                }
                else
                {
                    y = 0;
                    indices[i * 3 + 1] = i + 1;
                    indices[i * 3 + 2] = i + 2;
                }

                var x = Mathf.Cos(angle);
                var z = Mathf.Sin(angle);
                positions[i] = new Vector3(x * radius, y, z * radius);
                normals[i] = new Vector3(x, 0, z);
                uv[i] = new Vector2(i / (float)triangleCount, y);
            }

            positions[triangleCount] = positions[0];
            normals[triangleCount] = normals[0];
            uv[triangleCount] = new Vector2(1, 1);

            positions[triangleCount + 1] = positions[1];
            normals[triangleCount + 1] = normals[1];
            uv[triangleCount + 1] = new Vector2((triangleCount + 1) / (float)triangleCount, 0);

            // Assign arrays to mesh
            m.vertices = positions;
            m.normals = normals;
            m.uv = uv;
            m.triangles = indices;

            m.RecalculateTangents();
            m.RecalculateBounds();

            return m;
        }
    }
}

/*
* Copyright (c) 2023 InterDigital
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using UnityEngine;

namespace GLTFast
{
    public static class PrimitivesHelper
    {
        public static Mesh PrimitivesCreateBoxMesh(float width,
            float height,
            float length,
            Vector3 centroid)
        {
            float _x = width / 2.0f;
            float _y = height / 2.0f;
            float _z = length / 2.0f;

            // Vertices
            Vector3[] vertices = {
                new Vector3 (-_x,-_y,-_z),  // bottom left
                new Vector3 ( _x,-_y,-_z),  // bottom right
                new Vector3 ( _x, _y,-_z),  // top right
                new Vector3 (-_x, _y,-_z),  // top left
                new Vector3 (-_x, _y, _z),  // rear top left
                new Vector3 ( _x, _y, _z),  // rear top right
                new Vector3 ( _x,-_y, _z),  // rear bottom right
                new Vector3 (-_x,-_y, _z),  // rear bottom left
            };

            OffsetVerticesFromCentroid(vertices, centroid);

            int[] triangles = {
                0, 2, 1, //face front
	            0, 3, 2,
                2, 3, 4, //face top
	            2, 4, 5,
                1, 2, 5, //face right
	            1, 5, 6,
                0, 7, 4, //face left
	            0, 4, 3,
                5, 4, 7, //face back
	            5, 7, 6,
                0, 6, 7, //face bottom
	            0, 1, 6
            };

            Mesh _msh = new Mesh();
            _msh.vertices = vertices;
            _msh.triangles = triangles;
            _msh.Optimize();
            _msh.RecalculateNormals();
            return _msh;
        }

        public static Mesh PrimitivesCreatePlaneMesh(float width,
            float length,
            Vector3 centroid)
        {
            float _x = width / 2.0f;
            float _z = length / 2.0f;

            // Vertices
            Vector3[] vertices = {
                new Vector3 (-_x,0,-_z),  // bottom left
                new Vector3 ( _x,0,-_z),  // bottom right
                new Vector3 ( _x,0, _z),  // rear bottom right
                new Vector3 (-_x,0, _z),  // rear bottom left
            };

            // Centroid here should be the pivot of the cube, so offset the vertices
            // from there
            OffsetVerticesFromCentroid(vertices, centroid);

            int[] triangles = {
                0, 2, 1, //face front
	            0, 3, 2
            };

            Mesh _msh = new Mesh();
            _msh.vertices = vertices;
            _msh.triangles = triangles;
            _msh.Optimize();
            _msh.RecalculateNormals();
            return _msh;
        }

        public static Mesh PrimitivesCreateCylinderMesh(float radius,
            float length,
            Vector3 centroid)
        {
            if (radius <= 0.0f || length == 0.0f)
            {
                throw new System.Exception("Failed creating cylinder please enter a valid cylinder radius or length");
            }

            int stacks = 1;
            int slices = 32;
            float sliceStep = (float)Mathf.PI * 2.0f / slices;
            float currentHeight = -length / 2;
            int vertexCount = (stacks + 1) * slices + 2;
            int triangleCount = (stacks + 1) * slices * 2;
            int indexCount = triangleCount * 3;
            float currentRadius = radius;

            Vector3[] cylinderVertices = new Vector3[vertexCount];

            // Start at the bottom of the cylinder            
            int currentVertex = 0;
            cylinderVertices[currentVertex] = new Vector3(0, currentHeight, 0);
            currentVertex++;

            for (int i = 0; i <= stacks; i++)
            {
                float sliceAngle = 0;
                for (int j = 0; j < slices; j++)
                {
                    float x = currentRadius * (float)Mathf.Cos(sliceAngle);
                    float y = currentHeight;
                    float z = currentRadius * (float)Mathf.Sin(sliceAngle);

                    Vector3 position = new Vector3(x, y, z);
                    cylinderVertices[currentVertex] = position;
                    currentVertex++;

                    sliceAngle += sliceStep;
                }
                currentHeight += length;
            }

            cylinderVertices[currentVertex] = new Vector3(0, length / 2, 0);

            OffsetVerticesFromCentroid(cylinderVertices, centroid);

            Mesh mesh = new Mesh();
            mesh.vertices = cylinderVertices;
            mesh.triangles = CreateIndexBuffer(vertexCount, indexCount, slices);
            mesh.Optimize();
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Mesh PrimitivesCreateCapsuleMesh(float radius,
            Vector3 baseCentroid,
            Vector3 topCentroid)
        {
            float height = 2.0f;
            int segments = 24;

            if (segments % 2 != 0)
                segments++;

            int points = segments + 1;

            float[] pX = new float[points];
            float[] pZ = new float[points];
            float[] pY = new float[points];
            float[] pR = new float[points];

            float calcH = 0f;
            float calcV = 0f;

            for (int i = 0; i < points; i++)
            {
                pX[i] = Mathf.Sin(calcH * Mathf.Deg2Rad);
                pZ[i] = Mathf.Cos(calcH * Mathf.Deg2Rad);
                pY[i] = Mathf.Cos(calcV * Mathf.Deg2Rad);
                pR[i] = Mathf.Sin(calcV * Mathf.Deg2Rad);

                calcH += 360f / (float)segments;
                calcV += 180f / (float)segments;
            }


            Vector3[] vertices = new Vector3[points * (points + 1)];
            int ind = 0;

            float yOff = (height - (radius * 2f)) * 0.5f;
            if (yOff < 0)
                yOff = 0;

            // Top Hemisphere
            int top = Mathf.CeilToInt((float)points * 0.5f);

            for (int y = 0; y < top; y++)
            {
                for (int x = 0; x < points; x++)
                {
                    vertices[ind] = new Vector3(pX[x] * pR[y], pY[y], pZ[x] * pR[y]) * radius;
                    vertices[ind].y = yOff + vertices[ind].y;
                    vertices[ind] += topCentroid;

                    ind++;
                }
            }

            // Bottom Hemisphere
            int btm = Mathf.FloorToInt((float)points * 0.5f);

            for (int y = btm; y < points; y++)
            {
                for (int x = 0; x < points; x++)
                {
                    vertices[ind] = new Vector3(pX[x] * pR[y], pY[y], pZ[x] * pR[y]) * radius;
                    vertices[ind].y = -yOff + vertices[ind].y;
                    vertices[ind] += baseCentroid;

                    ind++;
                }
            }

            // TODO: Rotate the hemisphere before the bridge

            // Triangles
            int[] triangles = new int[(segments * (segments + 1) * 2 * 3)];

            for (int y = 0, t = 0; y < segments + 1; y++)
            {
                for (int x = 0; x < segments; x++, t += 6)
                {
                    triangles[t + 0] = ((y + 0) * (segments + 1)) + x + 0;
                    triangles[t + 1] = ((y + 1) * (segments + 1)) + x + 0;
                    triangles[t + 2] = ((y + 1) * (segments + 1)) + x + 1;

                    triangles[t + 3] = ((y + 0) * (segments + 1)) + x + 1;
                    triangles[t + 4] = ((y + 0) * (segments + 1)) + x + 0;
                    triangles[t + 5] = ((y + 1) * (segments + 1)) + x + 1;
                }
            }
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            //mesh.bounds
            //mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.Optimize();
            return mesh;
        }

        public static Mesh PrimitivesCreateSpheroidMesh(float radius,
            Vector3 centroid)
        {
            int stacks = 16;
            int slices = 32;
            float sliceStep = (float)Mathf.PI * 2.0f / slices;
            float stackStep = (float)Mathf.PI / stacks;
            int vertexCount = slices * (stacks - 1) + 2;
            int triangleCount = slices * (stacks - 1) * 2;
            int indexCount = triangleCount * 3;

            Vector3[] sphereVertices = new Vector3[vertexCount];

            int currentVertex = 0;
            sphereVertices[currentVertex] = new Vector3(0, -radius, 0);
            currentVertex++;
            float stackAngle = (float)Mathf.PI - stackStep;
            for (int i = 0; i < stacks - 1; i++)
            {
                float sliceAngle = 0;
                for (int j = 0; j < slices; j++)
                {
                    //NOTE: y and z were switched from normal spherical coordinates because the sphere is "oriented" along the Y axis as opposed to the Z axis
                    float x = (float)(radius * Mathf.Sin(stackAngle) * Mathf.Cos(sliceAngle));
                    float y = (float)(radius * Mathf.Cos(stackAngle));
                    float z = (float)(radius * Mathf.Sin(stackAngle) * Mathf.Sin(sliceAngle));

                    Vector3 position = new Vector3(x, y, z);
                    sphereVertices[currentVertex] = position;

                    currentVertex++;

                    sliceAngle += sliceStep;
                }
                stackAngle -= stackStep;
            }
            sphereVertices[currentVertex] = new Vector3(0, radius, 0);

            OffsetVerticesFromCentroid(sphereVertices, centroid);

            Mesh mesh = new Mesh();
            mesh.vertices = sphereVertices;
            mesh.triangles = CreateIndexBuffer(vertexCount, indexCount, slices);
            mesh.Optimize();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void OffsetVerticesFromCentroid(Vector3[] vertices, Vector3 _centroid)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] += _centroid;
            }
        }

        private static int[] CreateIndexBuffer(int vertexCount, int indexCount, int slices)
        {
            int[] indices = new int[indexCount];
            int currentIndex = 0;

            // Bottom circle/cone of shape
            for (int i = 1; i <= slices; i++)
            {
                indices[currentIndex++] = i;
                indices[currentIndex++] = 0;
                if (i - 1 == 0)
                    indices[currentIndex++] = i + slices - 1;
                else
                    indices[currentIndex++] = i - 1;
            }

            // Middle sides of shape
            for (int i = 1; i < vertexCount - slices - 1; i++)
            {
                indices[currentIndex++] = i + slices;
                indices[currentIndex++] = i;
                if ((i - 1) % slices == 0)
                    indices[currentIndex++] = i + slices + slices - 1;
                else
                    indices[currentIndex++] = i + slices - 1;

                indices[currentIndex++] = i;
                if ((i - 1) % slices == 0)
                    indices[currentIndex++] = i + slices - 1;
                else
                    indices[currentIndex++] = i - 1;
                if ((i - 1) % slices == 0)
                    indices[currentIndex++] = i + slices + slices - 1;
                else
                    indices[currentIndex++] = i + slices - 1;
            }

            // Top circle/cone of shape
            for (int i = vertexCount - slices - 1; i < vertexCount - 1; i++)
            {
                indices[currentIndex++] = i;
                if ((i - 1) % slices == 0)
                    indices[currentIndex++] = i + slices - 1;
                else
                    indices[currentIndex++] = i - 1;
                indices[currentIndex++] = vertexCount - 1;
            }

            return indices;
        }

        public static GameObject PrimitivesCreateCuboidGameObject(float width,
            float height,
            float length,
            Vector3 centroid, 
            bool rendered = true)
        {
            Mesh _msh = PrimitivesCreateBoxMesh(width, height, length, centroid);
            GameObject _box = new GameObject("Cube");
            MeshFilter _filter = _box.AddComponent<MeshFilter>();
            _filter.mesh = _msh;
            if(rendered)
            {
                MeshRenderer _rdr = _box.AddComponent<MeshRenderer>();
                _rdr.material = new Material(Shader.Find("Diffuse"));
            }
            return _box;
        }

        public static GameObject PrimitivesCreatePlaneGameObject(float width,
            float length,
            Vector3 centroid,
            bool rendered = true)
        {
            Mesh _msh = PrimitivesCreatePlaneMesh(width, length, centroid);
            GameObject _plane = new GameObject("Plane");
            MeshFilter _filter = _plane.AddComponent<MeshFilter>();
            _filter.mesh = _msh;
            if (rendered)
            {
                MeshRenderer _rdr = _plane.AddComponent<MeshRenderer>();
                _rdr.material = new Material(Shader.Find("Diffuse"));
            }
            return _plane;
        }

        public static GameObject PrimitivesCreateCylinderGameObject(float radius,
            float height,
            Vector3 centroid,
            bool rendered = true)
        {
            Mesh _msh = PrimitivesCreateCylinderMesh(radius, height, centroid);
            GameObject _cylinder = new GameObject("Cylinder");
            MeshFilter _filter = _cylinder.AddComponent<MeshFilter>();
            _filter.mesh = _msh;
            if (rendered)
            {
                MeshRenderer _rdr = _cylinder.AddComponent<MeshRenderer>();
                _rdr.material = new Material(Shader.Find("Diffuse"));
            }
            return _cylinder;
        }

        public static GameObject PrimitivesCreateSpheroidGameObject(float radius,
            Vector3 centroid,
            bool rendered = true)
        {
            Mesh _msh = PrimitivesCreateSpheroidMesh(radius, centroid);
            GameObject _sphere = new GameObject("Sphere");
            MeshFilter _filter = _sphere.AddComponent<MeshFilter>();
            _filter.mesh = _msh;
            if (rendered)
            {
                MeshRenderer _rdr = _sphere.AddComponent<MeshRenderer>();
                _rdr.material = new Material(Shader.Find("Diffuse"));
            }
            return _sphere;
        }

        public static GameObject PrimitivesCreateCapsuleGameObject(float radius, 
            Vector3 baseCentroid,
            Vector3 topCentroid,
            bool rendered = true)
        {
            Mesh _msh = PrimitivesCreateCapsuleMesh(radius, baseCentroid, topCentroid);
            GameObject _capsule = new GameObject("Capsule");
            MeshFilter _filter = _capsule.AddComponent<MeshFilter>();
            _filter.mesh = _msh;
            if (rendered)
            {
                MeshRenderer _rdr = _capsule.AddComponent<MeshRenderer>();
                _rdr.material = new Material(Shader.Find("Diffuse"));
            }
            return _capsule;
        }
    }
}
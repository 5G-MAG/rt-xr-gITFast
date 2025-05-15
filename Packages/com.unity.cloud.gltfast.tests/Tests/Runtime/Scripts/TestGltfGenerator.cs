// SPDX-FileCopyrightText: 2025 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast.Export;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace GLTFast.Tests
{
    static class TestGltfGenerator
    {
        const string k_TestFileFolder = "gltf-perf";

        internal static string FlatHierarchyPath =>
            Path.Combine(Application.streamingAssetsPath, k_TestFileFolder, "flat-hierarchy.gltf");
        internal static string FlatHierarchyBinaryPath =>
            Path.Combine(Application.streamingAssetsPath, k_TestFileFolder, "flat-hierarchy.glb");
        internal static string BigCylinderPath =>
            Path.Combine(Application.streamingAssetsPath, k_TestFileFolder, "big-cylinder.gltf");
        internal static string BigCylinderBinaryPath =>
            Path.Combine(Application.streamingAssetsPath, k_TestFileFolder, "big-cylinder.glb");

#if UNITY_EDITOR
        static async void CreatePerformanceTestFiles()
        {
            try
            {
                await CreatePerformanceTestFilesAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static async Task CreatePerformanceTestFilesAsync()
        {
            var folder = Path.Combine(Application.streamingAssetsPath, k_TestFileFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            await CreateGltfFlatHierarchy(FlatHierarchyPath, 10_000, GltfFormat.Json);
            await CreateGltfFlatHierarchy(FlatHierarchyBinaryPath, 10_000, GltfFormat.Binary);
            await CreateGltfBigCylinderMesh(BigCylinderPath, 1_000_000, GltfFormat.Json);
            await CreateGltfBigCylinderMesh(BigCylinderBinaryPath, 1_000_000, GltfFormat.Binary);
            AssetDatabase.Refresh();
        }
#endif

        internal static bool CertifyPerformanceTestGltfs()
        {
            return File.Exists(FlatHierarchyPath)
                && File.Exists(FlatHierarchyBinaryPath)
                && File.Exists(BigCylinderPath)
                && File.Exists(BigCylinderBinaryPath);
        }

        static async Task CreateGltfFlatHierarchy(string path, int nodeCount, GltfFormat format)
        {
            var exportSettings = new ExportSettings
            {
                Format = format
            };
            var writer = new GltfWriter(exportSettings);
            var row = (int)math.ceil(math.pow(nodeCount, 1 / 3f));
            var count = 0;
            var nodes = new List<uint>();
            for (var x = 0; x < row && count < nodeCount; x++)
            {
                for (var y = 0; y < row && count < nodeCount; y++)
                {
                    for (var z = 0; z < row; z++)
                    {
                        if (count++ >= nodeCount)
                            break;
                        var nodeId = writer.AddNode(new float3(x, y, z), name: $"Node-{x}-{y}-{z}");
                        nodes.Add(nodeId);
                    }
                }
            }
            writer.AddScene(nodes.ToArray());
            await writer.SaveToFileAndDispose(path);
        }

        static async Task CreateGltfBigCylinderMesh(string path, uint triangleCount, GltfFormat format)
        {
            var exportSettings = new ExportSettings
            {
                Format = format
            };
            var writer = new GltfWriter(exportSettings);
            var nodeId = writer.AddNode(name: "Cylinder");
            var mesh = TestMeshGenerator.GenerateCylinderMesh(triangleCount);
            writer.AddMeshToNode(
                (int)nodeId,
                mesh,
                null,
                null
                );
            writer.AddScene(new[] { nodeId });
            await writer.SaveToFileAndDispose(path);
        }
    }
}

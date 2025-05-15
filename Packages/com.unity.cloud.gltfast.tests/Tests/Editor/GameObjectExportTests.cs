// SPDX-FileCopyrightText: 2025 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.IO;
using System.Threading.Tasks;
using GLTFast.Tests;
using GLTFast.Tests.Export;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GLTFast.Editor.Tests.Export
{
    [Category("Export")]
    class GameObjectExportTests
    {
        static GameObject s_NonReadableTriangle;

        [UnityTest]
        public IEnumerator NonReadableMesh()
        {
            ExportNonReadableTests.Certify();
            Selection.activeGameObject = s_NonReadableTriangle;
            var path = Path.Combine(Application.temporaryCachePath, "NonReadableMesh.gltf");
            var task = MenuEntries.Export(path, false, "NonReadableMesh", new[] { s_NonReadableTriangle });
            yield return AsyncWrapper.WaitForTask(task);
#if GLTF_VALIDATOR
            // glTF Validation has been performed in `MenuEntries.Export`
#else
            Assert.Inconclusive("glTF-Validator for Unity is not installed. Cannot validate exported glTF.");
#endif
        }

        [UnityTest]
        public IEnumerator MixedReadableMesh()
        {
            const string name = "MixedReadableMesh";
            ExportNonReadableTests.Certify();
            var nonReadable = Object.Instantiate(s_NonReadableTriangle);
            var readable = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = readable.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsTrue(mesh.isReadable);

            var task = ExportObjects(
                $"{name}-01",
                new[] { nonReadable, readable },
                false
            );
            yield return AsyncWrapper.WaitForTask(task);

            task = ExportObjects(
                $"{name}-10",
                new[] { readable, nonReadable },
                false
            );
            yield return AsyncWrapper.WaitForTask(task);
        }

        static async Task ExportObjects(
            string name,
            GameObject[] gameObjects,
            bool binary
        )
        {
            var ext = binary ? Constants.gltfBinaryExtension : Constants.gltfExtension;
            var path = Path.Combine(Application.temporaryCachePath, $"{name}.{ext}");
            await MenuEntries.Export(path, false, name, gameObjects);
#if GLTF_VALIDATOR
            // glTF Validation has been performed in `MenuEntries.Export`
#else
            Assert.Inconclusive("glTF-Validator for Unity is not installed. Cannot validate exported glTF.");
#endif
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            s_NonReadableTriangle = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Packages/{GltfGlobals.GltfPackageName}.tests/Tests/Resources/Export/Models/NonReadableTriangle.fbx");
            var mesh = s_NonReadableTriangle.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsFalse(mesh.isReadable);
        }
    }
}

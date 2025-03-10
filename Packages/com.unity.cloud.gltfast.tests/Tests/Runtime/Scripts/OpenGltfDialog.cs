// SPDX-FileCopyrightText: 2024 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GLTFast.Tests
{
    [RequireComponent(typeof(GltfAsset))]
    public class OpenGltfDialog : MonoBehaviour
    {
        [SerializeField]
        bool m_AutoLoadLastFile;

#if UNITY_EDITOR
        const string k_LastFilePathKey = "GLTFast.Tests.OpenGltfDialog.LastFilePath";

        static string LastFilePath
        {
            get => EditorPrefs.GetString(k_LastFilePathKey);
            set => EditorPrefs.SetString(k_LastFilePathKey, value);
        }

        async void Start()
        {
            if (m_AutoLoadLastFile)
            {
                var lastFilePath = LastFilePath;
                if (!string.IsNullOrEmpty(lastFilePath))
                {
                    if (File.Exists(lastFilePath))
                    {
                        await LoadGltfFile(lastFilePath);
                        return;
                    }

                    Debug.LogWarning($"Could not load glTF file from {lastFilePath}");
                }
            }
            await OpenFilePanel();
        }

        async void Update()
        {
            if (Input.GetKeyDown(KeyCode.G)
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                )
            {
                await OpenFilePanel();
            }
        }

        async Task OpenFilePanel() {

            var file = EditorUtility.OpenFilePanel("Open glTF file", LastFilePath, "gltf,glb");
            if (string.IsNullOrEmpty(file))
                return;

            LastFilePath = file;
            await LoadGltfFile(file);
        }

        async Task LoadGltfFile(string path)
        {
            var gltf = GetComponent<GltfAsset>();
            gltf.ClearScenes();
            gltf.Dispose();
            await gltf.Load(path);
        }
#else
        void Start()
        {
            Debug.LogWarning("GLTFast.Tests.OpenGltfDialog is not intended for use at runtime.");
        }
#endif
    }
}

# Open glTF files

During development there's often the need to load designated, custom glTF files.

## Open glTF File Dialog

A convenient way to do this is the *OpenGltfScene* scene, which is located in the project view under `Packages/Unity glTFast Tests/Tests/Runtime/Scenes` and enter play mode. An open file dialog for opening `.gltf`/`.glb` files will be displayed. The selected glTF file will be loaded and instantiated into the scene and can be inspected, if it succeeded.

Press *Control* and *G* to repeatedly open the same open file dialog within the same play mode session.

> [!NOTE]
> This convenient open file dialog works in the Editor only and won't work in a build.

You can copy and modify the scene to make further adjustments like camera position, lighting, etc.

## Custom Test Environment

If you have many test iterations on one file or you want to load multiple files at once the open file dialog above might not work well for you. An alternative is to create a custom test scene that utilizes the [GltfAsset](ImportRuntime.md#runtime-loading-via-component) component or [custom load scripting](ImportRuntime.md#runtime-loading-via-script) to run your tailored test procedure.

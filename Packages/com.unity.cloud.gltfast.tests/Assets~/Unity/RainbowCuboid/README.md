# Rainbow Cuboid

## Description

Contains an animated, skinned mesh with morph targets and the following vertex attributes:

- Positions
- Normals
- Tangents
- Texture coordinates
- Colors
- Morph target indices/weights

`RainbowCuboid.gltf` is the original glTF. The other glTFs are [meshopt][meshopt] compressed variants.

## Compression

The compressed variants have been created with [gltfpack][gltfpack] [0.20](https://github.com/zeux/meshoptimizer/releases/tag/v0.20) and the following parameters:

```shell
 gltfpack -c       -i RainbowCuboid.gltf  -o RainbowCuboid-meshopt-c.gltf
 gltfpack -cc      -i RainbowCuboid.gltf  -o RainbowCuboid-meshopt-cc.gltf
 gltfpack -c -vpn  -i RainbowCuboid.gltf  -o RainbowCuboid-meshopt-c-vpn.gltf
 gltfpack -cc -vpn -i RainbowCuboid.gltf  -o RainbowCuboid-meshopt-cc-vpn.gltf
```

## Extensions used

- [EXT_meshopt_compression][meshoptExt]

## Legal

Model: *Rainbow Cuboid*

Attribution notice for the files directly associated with the above-referenced model in this directory tree, including all text, image and binary files.

&copy; 2024, Unity Technologies and the glTFast authors. Licensed under Apache 2.0. See [license file at root](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/LICENSE.md).

[meshopt]: https://github.com/zeux/meshoptimizer
[meshoptExt]: https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Vendor/EXT_meshopt_compression
[gltfpack]: https://github.com/zeux/meshoptimizer/tree/master/gltf

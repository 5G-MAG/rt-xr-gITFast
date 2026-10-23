# 5G-MAG fork of Unity glTFast

This repository is a fork of [Unity glTFast](https://github.com/atteneder/glTFast) (`com.unity.cloud.gltfast`), kept by 5G-MAG for the XR/3D Scenes with MPEG-I Scene Description project. The root [README](README.md), the documentation under `Docs/`, the [licence](LICENSE.md) and the [third-party notices](<Third Party Notices.md>) are upstream's and are left as they are, so that the fork stays easy to compare with and update from upstream. This file says what the fork changes.

## What is different

| Area | Change | Where |
|---|---|---|
| Package | Renamed `rt.xr.gltfast`, display name "5G-MAG fork of com.unity.cloud.gltfast" | `Packages/com.unity.cloud.gltfast/package.json` |
| MPEG-I Scene Description | Support for extensions of ISO/IEC 23090-14: anchoring, interactivity, and media (video textures, timed accessors, circular buffers, spatial audio) | `Packages/com.unity.cloud.gltfast/Runtime/Scripts/Mpeg/` (`Anchoring`, `Interactivity`, `Media`, `MPEGGltfImport.cs`, `VirtualSceneGraph.cs`) |
| Schema classes | Classes for the MPEG extension objects, as `MPEG_anchor`, the interactivity objects, `MPEG_audio_spatial` and `MPEG_sampler_YCbCr` | `Packages/com.unity.cloud.gltfast/Runtime/Scripts/Schema/` |
| Dependencies | AR Foundation and Input System are dependencies of the package; the ARCore dependencies are optional | `package.json`, assembly definitions |
| Platforms | Unity 6000 compatibility; OpenXR KHR simple controller profile | commits of `rt-xr/development` |

The runtime behaviour of each extension, and what is not yet supported, is listed on the project's implementation page on the 5G-MAG website, not here.

## Branches

| Branch | Purpose |
|---|---|
| `rt-xr/development` | Default branch. Integration of the changes above; pull requests go here. |
| `rt-xr/main` | Released state of the fork. Updated by merging `rt-xr/development`. |
| `main`, `legacy/*`, `experimental/*` | Upstream's branches, kept as upstream has them. |

## Keeping up with upstream

The fork is updated by merging upstream into `rt-xr/development`. To keep these merges small, changes to files that exist upstream are kept to what the MPEG extensions need; new code goes in the `Mpeg` folders and in new files.

## Licence

Upstream's code and the additions of this fork are under the Apache License 2.0 (see [LICENSE.md](LICENSE.md)). Third-party components keep their own licences, listed in [Third Party Notices](<Third Party Notices.md>).

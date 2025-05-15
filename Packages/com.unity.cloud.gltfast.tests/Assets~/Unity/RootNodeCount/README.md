# Root Node Count

## Description

This is a collection of glTFs containing combinations of the following attributes

- Scene root nodes
  - One
  - Two
- Scenes
  - One file per scene
  - Two scenes in one file
- Animation
  - With
  - Without

When loading a glTF into a hierarchical node structure one can create a dedicated node representing the scene, or not. Furthermore, some animation systems work with target paths that depend on the hierarchy. This scene let's you test these aspects.

Animation clips are global, but assigned to nodes in glTF. Some animation systems assign an animation system/component per scene though. An animation clip might not be valid on all scenes, so this might cause problems. The `AllScenes.gltf` allows you to test this.

## Legal

Model: *Root Node Count*

Attribution notice for the files directly associated with the above-referenced model in this directory tree, including all text, image and binary files.

&copy; 2022, Andreas Atteneder. CC-BY 4.0 International <https://creativecommons.org/licenses/by/4.0/>.

# Sources

## Download Sources

*glTFast*'s sources are hosted in a [GitHub repository][UnityGltfastGitHub] that needs to be cloned (i.e. downloaded) first.

> [!TIP]
> See [Cloning a repository][GitHubCloning] for detailed instructions how to download a repository.
> [Git LFS](https://git-lfs.com/) is used for binary files, so make sure your Git client handles it correctly.

Clone via command line interface:

```sh
git clone git@github.com:Unity-Technologies/com.unity.cloud.gltfast.git
```

This will download the repository into a sub-folder named `com.unity.cloud.gltfast`.

> [!NOTE]
> Cloning requires authentication configured properly.

## Repository Structure

*glTFast* is part of a larger [Monorepo][Monorepo] and can be found in the subfolder `Packages/com.unity.cloud.gltfast`.

Here's an overview of the repository structure.

```none
<Root>
├── Packages
│   ├── com.unity.cloud.gltfast
│   └── com.unity.cloud.gltfast.tests
│       └── Assets~
└── Projects
    ├── glTFast-Test
    └── ...
```

- *Packages* - Unity&reg; packages
  - *com.unity.cloud.gltfast* - The actual *glTFast* package
  - *com.unity.cloud.gltfast.tests* - Test code and assets
    - *Assets~* - glTF&trade; test assets
- *Projects* - see [Test Projects](test-project-setup.md#test-projects)

## Trademarks

*Unity&reg;* is a registered trademark of [Unity Technologies][unity].

*Khronos&reg;* is a registered trademark and [glTF&trade;][gltf] is a trademark of [The Khronos Group Inc][khronos].

[GitHubCloning]: https://docs.github.com/en/repositories/creating-and-managing-repositories/cloning-a-repository
[gltf]: https://www.khronos.org/gltf
[khronos]: https://www.khronos.org
[Monorepo]: https://en.wikipedia.org/wiki/Monorepo
[UnityGltfastGitHub]: https://github.com/Unity-Technologies/com.unity.cloud.gltfast
[unity]: https://unity.com

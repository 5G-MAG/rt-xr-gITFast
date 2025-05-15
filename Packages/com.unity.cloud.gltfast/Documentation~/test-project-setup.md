# Test Project Setup

You can get started quickly by opening one of the provided [test projects](#test-projects) or [setup a custom project](#setup-a-custom-project).

## Test Projects

This repository comes with test projects that are pre-configured to have all required packages installed and ready to run [tests](tests.md).

### Project List

| Project                   | Description                                             |
|---------------------------|---------------------------------------------------------|
| glTFast-Test              | Default project with all [optional packages](installation.md#optional-packages) installed. |
| glTFast-Test-minimalistic | Minimalistic setup with none of the optional dependency packages installed. |

### Open a Project

To open a project open [Unity&reg; Hub][UnityHub], go to *Projects* and pick *Add* → *Add project from disk*. Select the respective project folder from within the `Projects` directory in your local copy.

Upon opening the project you can pick a Unity version of your choice (within the range of [supported versions](features.md#unity-version-support)). In fact you might have to, if the predefined version is not installed. If you decide to work on more recent versions, keep in mind that for contributions your changes have to be backwards compatible to all supported versions.

## Setup a Custom Project

The setup of an existing project can give you a development context that is not covered by any of the provided [test projects](test-project-setup.md#test-projects). Examples are custom scripts for import/export of glTFs, the constellation of installed packages, the choice of [render pipeline][RenderPipelines] or use of [Entities][Entities].

In such scenarios it makes sense to setup the existing project for *glTFast* development.

Prerequisite is that you have a [local copy of the repository](sources.md#download-sources).

With your project opened, open the *Package Manager* and click the ➕ symbol at the top left. Select *Add package from disk* and navigate to `Packages/com.unity.cloud.gltfast/package.json` within your local copy.

Repeat the same steps for the tests package at `Packages/com.unity.cloud.gltfast.tests/package.json`.

You're now ready to start modifying *glTFast* and [run the tests](tests.md).

## Trademarks

*Unity&reg;* is a registered trademark of [Unity Technologies][unity].

[Entities]: https://docs.unity3d.com/Packages/com.unity.entities@latest/
[RenderPipelines]: https://docs.unity3d.com/Manual/render-pipelines.html
[unity]: https://unity.com
[UnityHub]: https://unity.com/unity-hub

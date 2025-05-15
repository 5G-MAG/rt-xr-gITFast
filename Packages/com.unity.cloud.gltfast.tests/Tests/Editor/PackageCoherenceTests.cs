// SPDX-FileCopyrightText: 2024 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace GLTFast.Editor.Tests
{
    class PackageCoherenceTests
    {
        struct Package
        {
            public string version;
            public string unity;
            public string unityRelease;

            public string MinimumRequiredVersion =>
                string.IsNullOrEmpty(unityRelease)
                    ? unity
                    : $"{unity}.{unityRelease}";
        }

        static Package s_Package;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var json = File.ReadAllText($"Packages/{GltfGlobals.GltfPackageName}/package.json");
            s_Package = JsonUtility.FromJson<Package>(json);
        }

        [Test]
        public void ExportVersionTest()
        {
            Assert.AreEqual(
                GLTFast.Export.Constants.version,
                s_Package.version,
                "GLTFast.Export.Constants.version does not match package version " +
                $"(is \"{GLTFast.Export.Constants.version}\", should be \"{s_Package.version}\") ");
        }

        [Test]
        public void DocumentationMinRequiredVersionTest()
        {
            var path = $"Packages/{GltfGlobals.GltfPackageName}/Documentation~/features.md";
            var features = File.ReadAllText(path);
            var regex = new Regex(@"## Unity Version Support\n\n\*Unity glTFast\* requires Unity (?<version>.*) or newer\.");
            var match = regex.Match(features);
            Assert.IsTrue(match.Success, $"Couldn't find minimum required Unity version in {path}");

            var docMinVersion = match.Groups["version"].Value;
            var expectedMinVersion = s_Package.MinimumRequiredVersion;

            Assert.AreEqual(
                expectedMinVersion,
                docMinVersion,
                "Difference in minimum required Unity version between package and documentation " +
                $"(is \"{docMinVersion}\", should be \"{expectedMinVersion}\") ");
        }
    }
}

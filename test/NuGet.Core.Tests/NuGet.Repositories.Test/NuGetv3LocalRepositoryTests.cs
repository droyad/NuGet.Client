// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Repositories.Test
{
    public class NuGetv3LocalRepositoryTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NuGetv3LocalRepository_FindPackagesById_ReturnsEmptySequenceWithIdNotFound(bool lowercase)
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var target = new NuGetv3LocalRepository(workingDir, lowercase);

                // Act
                var packages = target.FindPackagesById("Foo");

                // Assert
                Assert.Empty(packages);
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackagesById_UsesProvidedIdCase()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var lowercase = true;
                var target = new NuGetv3LocalRepository(workingDir, lowercase);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    lowercase,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext("foo", "1.0.0"));

                // Act
                var packages = target.FindPackagesById(id);

                // Assert
                Assert.Equal(1, packages.Count());
                Assert.Equal(id, packages.ElementAt(0).Id);
                Assert.Equal("1.0.0", packages.ElementAt(0).Version.ToNormalizedString());
            }
        }

        [Theory]
        [InlineData(true, "2.0.0-beta")]
        [InlineData(false, "2.0.0-Beta")]
        public async Task NuGetv3LocalRepository_FindPackagesById_LeavesVersionCaseFoundOnFileSystem(bool lowercase, string versionWithCase)
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir, lowercase);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    lowercase,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var packages = target.FindPackagesById(id);

                // Assert
                Assert.Equal(2, packages.Count());
                packages = packages.OrderBy(x => x.Version);
                Assert.Equal(id, packages.ElementAt(0).Id);
                Assert.Equal("1.0.0", packages.ElementAt(0).Version.ToNormalizedString());
                Assert.Equal(id, packages.ElementAt(1).Id);
                Assert.Equal(versionWithCase, packages.ElementAt(1).Version.ToNormalizedString());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NuGetv3LocalRepository_FindPackage_ReturnsNullWithIdNotFound(bool lowercase)
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var target = new NuGetv3LocalRepository(workingDir, lowercase);

                // Act
                var package = target.FindPackage("Foo", NuGetVersion.Parse("2.0.0-BETA"));

                // Assert
                Assert.Null(package);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NuGetv3LocalRepository_FindPackage_ReturnsNullWithVersionNotFound(bool lowercase)
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir, lowercase);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    lowercase,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var package = target.FindPackage(id, NuGetVersion.Parse("3.0.0-BETA"));

                // Assert
                Assert.Null(package);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NuGetv3LocalRepository_FindPackage_UsesProvidedVersionCase(bool lowercase)
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir, lowercase);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    lowercase,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var package = target.FindPackage(id, NuGetVersion.Parse("2.0.0-BETA"));

                // Assert
                Assert.NotNull(package);
                Assert.Equal(id, package.Id);
                Assert.Equal("2.0.0-BETA", package.Version.ToNormalizedString());
            }
        }
    }
}

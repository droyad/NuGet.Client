using System.IO;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class ToolPathResolverTests
    {
        [Fact]
        public void ToolPathResolver_BuildsLowercaseLockFilePath()
        {
            // Arrange
            var target = new ToolPathResolver("packages", lowercase: true);
            var expected = Path.Combine(
                "packages",
                ".tools",
                "Packagea",
                "3.1.4-beta",
                "netstandard1.3",
                "project.lock.json");

            // Act
            var actual = target.GetLockFilePath(
                "PackageA",
                NuGetVersion.Parse("3.1.4-BETA"),
                FrameworkConstants.CommonFrameworks.NetStandard13);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToolPathResolver_BuildsOriginalCaseLockFilePath()
        {
            // Arrange
            var target = new ToolPathResolver("packages", lowercase: false);
            var expected = Path.Combine(
                "packages",
                ".tools",
                "PackageA",
                "3.1.4-BETA",
                "netstandard1.3",
                "project.lock.json");

            // Act
            var actual = target.GetLockFilePath(
                "PackageA",
                NuGetVersion.Parse("3.1.4-BETA"),
                FrameworkConstants.CommonFrameworks.NetStandard13);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}

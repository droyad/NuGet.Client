// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class ToolPathResolver
    {
        private readonly string _packagesDirectory;
        private readonly bool _lowercase;

        public ToolPathResolver(string packagesDirectory)
            : this(packagesDirectory, lowercase: true)
        {
        }

        public ToolPathResolver(string packagesDirectory, bool lowercase)
        {
            _packagesDirectory = packagesDirectory;
            _lowercase = lowercase;
        }

        public string GetLockFilePath(string packageId, NuGetVersion version, NuGetFramework framework)
        {
            var versionString = version.ToNormalizedString();
            var frameworkString = framework.GetShortFolderName();

            if (_lowercase)
            {
                packageId = packageId.ToLowerInvariant();
                versionString = versionString.ToLowerInvariant();
                frameworkString = frameworkString.ToLowerInvariant();
            }
            
            return Path.Combine(
                _packagesDirectory,
                ".tools",
                packageId,
                versionString,
                frameworkString,
                LockFileFormat.LockFileName);
        }
    }
}

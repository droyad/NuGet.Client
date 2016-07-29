using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.BuildTasks
{
    /// <summary>
    /// .NET Core compatible restore task for csproj + project.json.
    /// </summary>
    public class RestoreTask : Task
    {
        /// <summary>
        /// DG file entries
        /// </summary>
        [Required]
        public ITaskItem[] RestoreGraphItems { get; set; }

        /// <summary>
        /// NuGet sources
        /// </summary>
        public ITaskItem[] RestoreSources { get; set; }

        /// <summary>
        /// User packages folder
        /// </summary>
        public ITaskItem RestorePackagesPath { get; set; }

        public ITaskItem RestoreDisableParallel { get; set; }

        public ITaskItem RestoreConfigFile { get; set; }

        public ITaskItem RestoreNoCache { get; set; }

        public ITaskItem RestoreIgnoreFailedSource { get; set; }

        public ITaskItem RestoreForceEnglishOutput { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);
            var graphLines = GetStrings(RestoreGraphItems);
            var providerCache = new RestoreCommandProvidersCache();

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = IsTrue(RestoreNoCache);
                cacheContext.IgnoreFailedSources = IsTrue(RestoreIgnoreFailedSource);

                // Pre-loaded request provider containing the graph file
                var providers = new List<IPreLoadedRestoreRequestProvider>();
                providers.Add(new PreLoadedRestoreRequestProvider(providerCache, graphLines));

                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = cacheContext,
                    LockFileVersion = LockFileFormat.Version,
                    ConfigFile = GetString(RestoreConfigFile),
                    DisableParallel = IsTrue(RestoreDisableParallel),
                    GlobalPackagesFolder = GetString(RestorePackagesPath),
                    Log = log,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    PreLoadedRequestProviders = providers,
                    Sources = new List<string>(GetStrings(RestoreSources)),
                    CachingSourceProvider = sourceProvider
                };

                if (restoreContext.DisableParallel)
                {
                    HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
                }

                var restoreSummaries = RestoreRunner.Run(restoreContext).Result;

                // Summary
                RestoreSummary.Log(log, restoreSummaries);

                return restoreSummaries.All(x => x.Success);
            }
        }

        private static string GetString(ITaskItem item)
        {
            if (item != null)
            {
                var s = item.ToString();

                return string.IsNullOrEmpty(s) ? null : s;
            }

            return null;
        }

        private static string[] GetStrings(ITaskItem[] items)
        {
            if (items != null)
            {
                return items.Select(item => GetString(item)).ToArray();
            }

            return new string[0];
        }

        private static bool IsTrue(ITaskItem item)
        {
            return Boolean.TrueString.Equals(GetString(item), StringComparison.OrdinalIgnoreCase);
        }
    }
}

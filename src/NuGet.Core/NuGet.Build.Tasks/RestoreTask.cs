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

namespace NuGet.Build.Tasks
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
        public string[] RestoreGraphItems { get; set; }

        /// <summary>
        /// NuGet sources, ; delimited
        /// </summary>
        public string RestoreSources { get; set; }

        /// <summary>
        /// User packages folder
        /// </summary>
        public string RestorePackagesPath { get; set; }

        public bool RestoreDisableParallel { get; set; }

        public string RestoreConfigFile { get; set; }

        public bool RestoreNoCache { get; set; }

        public bool RestoreIgnoreFailedSource { get; set; }

        public bool RestoreForceEnglishOutput { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);
            var graphLines = RestoreGraphItems;
            var providerCache = new RestoreCommandProvidersCache();

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = RestoreNoCache;
                cacheContext.IgnoreFailedSources = RestoreIgnoreFailedSource;

                // Pre-loaded request provider containing the graph file
                var providers = new List<IPreLoadedRestoreRequestProvider>();
                providers.Add(new PreLoadedRestoreRequestProvider(providerCache, graphLines));

                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = cacheContext,
                    LockFileVersion = LockFileFormat.Version,
                    ConfigFile = GetNullForEmpty(RestoreConfigFile),
                    DisableParallel = RestoreDisableParallel,
                    GlobalPackagesFolder = RestorePackagesPath,
                    Log = log,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    PreLoadedRequestProviders = providers,
                    CachingSourceProvider = sourceProvider
                };

                if (!string.IsNullOrEmpty(RestoreSources))
                {
                    var sources = RestoreSources.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    restoreContext.Sources.AddRange(sources);
                }

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

        public static string GetNullForEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}

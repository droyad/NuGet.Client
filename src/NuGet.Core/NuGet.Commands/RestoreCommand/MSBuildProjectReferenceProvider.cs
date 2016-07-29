using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class MSBuildProjectReferenceProvider : IExternalProjectReferenceProvider
    {
        private readonly Dictionary<string, Dictionary<string, ExternalProjectReference>> _cache
            = new Dictionary<string, Dictionary<string, ExternalProjectReference>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, PackageSpec> _projectJsonCache
            = new Dictionary<string, PackageSpec>(StringComparer.OrdinalIgnoreCase);

        public MSBuildProjectReferenceProvider(IEnumerable<string> msbuildOutputLines)
        {
            if (msbuildOutputLines == null)
            {
                throw new ArgumentNullException(nameof(msbuildOutputLines));
            }

            var lookup = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

            var firstLine = msbuildOutputLines.FirstOrDefault();

            if (!string.IsNullOrEmpty(firstLine))
            {
                if (firstLine.StartsWith("{"))
                {
                    var json = GetGraphFile(msbuildOutputLines);

                    foreach (var projectObject in json["projects"].Select(token => (JObject)token))
                    {
                        var entryPoint = projectObject["project"].ToObject<string>();

                        Debug.Assert(!lookup.ContainsKey(entryPoint), "Duplicate entry point in msbuild results");

                        if (!lookup.ContainsKey(entryPoint))
                        {
                            lookup.Add(entryPoint,
                                new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));
                        }

                        foreach (var projectGraphToken in json["projectGraph"])
                        {
                            var parts = projectGraphToken.Value<string>().Split('|');

                            if (parts.Length == 2)
                            {
                                var parent = parts[0];
                                var child = parts[1];

                                var projectReferences = lookup[entryPoint];

                                HashSet<string> children;
                                if (!projectReferences.TryGetValue(parent, out children))
                                {
                                    children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    projectReferences.Add(parent, children);
                                }

                                children.Add(child);
                            }
                        }
                    }
                }
                else if (firstLine.StartsWith("#:"))
                {
                    string entryPoint = null;

                    foreach (var line in msbuildOutputLines)
                    {
                        if (line.StartsWith("#:", StringComparison.Ordinal))
                        {
                            entryPoint = line.Substring(2, line.Length - 2);

                            Debug.Assert(!lookup.ContainsKey(entryPoint), "Duplicate entry point in msbuild results");

                            if (!lookup.ContainsKey(entryPoint))
                            {
                                lookup.Add(entryPoint,
                                    new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));
                            }

                            continue;
                        }

                        var parts = line.TrimEnd().Split('|');

                        if (parts.Length == 2)
                        {
                            var parent = parts[0];
                            var child = parts[1];

                            var projectReferences = lookup[entryPoint];

                            HashSet<string> children;
                            if (!projectReferences.TryGetValue(parent, out children))
                            {
                                children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                projectReferences.Add(parent, children);
                            }

                            children.Add(child);
                        }
                        else
                        {
                            Debug.Fail("Invalid: " + line);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("Invalid dg file format!");
                }
            }
            else
            {
                throw new InvalidOperationException("Empty dg file!");
            }

            foreach (var entryPointKey in lookup.Keys)
            {
                var references = GetExternalProjectReferences(entryPointKey, lookup[entryPointKey]);

                _cache.Add(entryPointKey, references);
            }
        }

        public static MSBuildProjectReferenceProvider Load(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var lines = File.ReadAllLines(path);

            return new MSBuildProjectReferenceProvider(lines);
        }

        public IReadOnlyList<ExternalProjectReference> GetReferences(string entryPointPath)
        {
            var results = new List<ExternalProjectReference>();

            Dictionary<string, ExternalProjectReference> lookup;
            if (_cache.TryGetValue(entryPointPath, out lookup))
            {
                results.AddRange(lookup.Values);
            }

            return results;
        }

        public IReadOnlyList<ExternalProjectReference> GetEntryPoints()
        {
            var results = new List<ExternalProjectReference>();

            foreach (var path in _cache.Keys)
            {
                results.Add(_cache[path][path]);
            }

            return results;
        }

        public PackageSpec GetProjectJson(string path)
        {
            PackageSpec projectJson;
            _projectJsonCache.TryGetValue(path, out projectJson);

            return projectJson;
        }

        private static JObject GetGraphFile(IEnumerable<string> lines)
        {
            using (var stream = new MemoryStream())
            {
                // Write the graph file lines into a memory stream
                using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
                {
                    foreach (var line in lines)
                    {
                        writer.Write(line);
                    }
                }

                stream.Seek(0, SeekOrigin.Begin);

                // Read the memory stream into a JObject
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    return JObject.Load(jsonReader);
                }
            }
        }

        /// <summary>
        /// MSBuild project -> ExternalProjectReference
        /// </summary>
        private Dictionary<string, ExternalProjectReference> GetExternalProjectReferences(
            string entryPoint,
            Dictionary<string, HashSet<string>> projectReferences)
        {
            var results = new Dictionary<string, ExternalProjectReference>(StringComparer.OrdinalIgnoreCase);

            // Get the set of all possible projects
            var allProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allProjects.Add(entryPoint);
            allProjects.UnionWith(projectReferences.Keys);
            allProjects.UnionWith(projectReferences.Values.SelectMany(children => children));

            // Load up all package specs
            foreach (var projectPath in allProjects)
            {
                var projectJson = GetPackageSpec(projectPath);

                var childProjectNames = new List<string>();
                HashSet<string> children;
                if (projectReferences.TryGetValue(projectPath, out children))
                {
                    childProjectNames.AddRange(children);
                }

                var projectReference = new ExternalProjectReference(
                    projectPath,
                    projectJson,
                    projectPath,
                    childProjectNames);

                Debug.Assert(!results.ContainsKey(projectPath), "dupe: " + projectPath);

                results.Add(projectPath, projectReference);
            }

            return results;
        }

        /// <summary>
        /// Load a project.json for an msbuild project file.
        /// This allows projectName.project.json for csproj but not for xproj.
        /// </summary>
        private PackageSpec GetPackageSpec(string msbuildProjectPath)
        {
            PackageSpec result = null;
            string path = null;
            var directory = Path.GetDirectoryName(msbuildProjectPath);
            var projectName = Path.GetFileNameWithoutExtension(msbuildProjectPath);

            if (msbuildProjectPath.EndsWith(XProjUtility.XProjExtension, StringComparison.OrdinalIgnoreCase))
            {
                // Only project.json is allowed
                path = Path.Combine(
                    directory,
                    PackageSpec.PackageSpecFileName);
            }
            else
            {
                // Allow project.json or projectName.project.json
                path = ProjectJsonPathUtilities.GetProjectConfigPath(directory, projectName);
            }

            // Read the file if it exists and is not cached already
            if (!_projectJsonCache.TryGetValue(path, out result))
            {
                if (File.Exists(path))
                {
                    result = JsonPackageSpecReader.GetPackageSpec(projectName, path);
                }

                _projectJsonCache.Add(path, result);
            }

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MCPForUnity.Editor.Setup
{
    /// <summary>Installs the small first-party skill bundle shipped with the Unity package.</summary>
    public static class BundledSkillInstaller
    {
        private const string SkillBundlePath = "Editor/Setup/Skills";

        public static IReadOnlyList<string> Install(string packageRoot, string installRoot)
        {
            if (string.IsNullOrWhiteSpace(packageRoot) || string.IsNullOrWhiteSpace(installRoot))
                throw new ArgumentException("Package and install paths are required.");

            string sourceRoot = Path.GetFullPath(Path.Combine(packageRoot, SkillBundlePath));
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"Bundled skills were not found at {sourceRoot}.");

            string destinationRoot = Path.GetFullPath(installRoot);
            Directory.CreateDirectory(destinationRoot);

            var installed = new List<string>();
            foreach (string skillDirectory in Directory.GetDirectories(sourceRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string skillName = Path.GetFileName(skillDirectory);
                string[] files = Directory.GetFiles(skillDirectory, "*.md", SearchOption.AllDirectories);
                if (!files.Any(path => string.Equals(Path.GetFileName(path), "SKILL.md", StringComparison.OrdinalIgnoreCase)))
                    continue;

                foreach (string sourceFile in files)
                {
                    string relativePath = sourceFile.Substring(skillDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string destination = Path.GetFullPath(Path.Combine(destinationRoot, skillName, relativePath));
                    string destinationPrefix = destinationRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                               + Path.DirectorySeparatorChar;
                    if (!destination.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Skill file path escapes install directory: {relativePath}");

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(sourceFile, destination, overwrite: true);
                }

                installed.Add(skillName);
            }

            if (installed.Count == 0)
                throw new InvalidOperationException("No valid Novahn skills were found in the package.");

            return installed;
        }
    }
}

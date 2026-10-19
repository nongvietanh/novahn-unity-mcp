using MCPForUnity.Editor.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Services.Hub
{
    public static class StudioSkillCatalog
    {
        public static string SourceRoot => Path.GetFullPath(Path.Combine(AssetPathUtility.GetMcpPackageRootPath(), "Editor", "Setup", "Skills"));
        public static string DefaultPath(HubAgentEntry agent)
        {
            string relative;
            switch (agent.clientKey)
            {
                case "claudecode": case "claudedesktop": relative = ".claude"; break;
                case "cursor": relative = ".cursor"; break;
                case "geminicli": relative = ".gemini"; break;
                case "vscodegithubcopilot": case "vscodeinsidersgithubcopilot": relative = ".github"; break;
                default: relative = ".agents"; break;
            }
            return Path.Combine(StudioWorkspace.ProjectRoot, relative, "skills");
        }
        public static string InstallPath(HubAgentEntry agent) => string.IsNullOrWhiteSpace(agent.skillInstallPath) ? DefaultPath(agent) : agent.skillInstallPath;
        public static IEnumerable<string> ForAgent(HubAgentEntry agent) =>
            agent.isLeader
                ? new[] { "novahn-unity", "novahn-unity-scene", "novahn-unity-gameplay", "novahn-coordinator", "novahn-desktop" }
                : new[] { "novahn-unity", "novahn-unity-scene", "novahn-unity-gameplay", "novahn-worker" };

        public static int InstalledCount(HubAgentEntry agent) => ForAgent(agent).Count(name => File.Exists(Path.Combine(InstallPath(agent), name, "SKILL.md")));

        public static int InstallBundle(HubAgentEntry agent)
        {
            int count = 0;
            foreach (string name in ForAgent(agent)) { InstallFolder(Path.Combine(SourceRoot, name), InstallPath(agent)); count++; }
            return count;
        }

        public static string InstallFolder(string source, string destinationRoot)
        {
            source = Path.GetFullPath(source);
            string skill = Path.Combine(source, "SKILL.md");
            if (!File.Exists(skill)) throw new InvalidOperationException(StudioText.T("The folder must contain SKILL.md at its root."));
            string text = File.ReadAllText(skill);
            var nameMatch = Regex.Match(text, @"(?m)^name:\s*([a-z0-9-]{1,64})\s*$");
            if (!text.StartsWith("---") || !nameMatch.Success || !Regex.IsMatch(text, @"(?m)^description:\s*\S"))
                throw new InvalidOperationException(StudioText.T("SKILL.md needs frontmatter with a valid name and description."));
            string destination = Path.GetFullPath(Path.Combine(destinationRoot, nameMatch.Groups[1].Value));
            if (destination.Equals(source, StringComparison.OrdinalIgnoreCase)) return nameMatch.Groups[1].Value;
            if (source.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(StudioText.T("Source and target directories must not contain each other."));
            if (Directory.Exists(destination))
            {
                string backup = Path.Combine(StudioWorkspace.SettingsRoot, "SkillBackups", Guid.NewGuid().ToString("N"), Path.GetFileName(destination));
                CopyTree(destination, backup);
            }
            CopyTree(source, destination);
            return nameMatch.Groups[1].Value;
        }

        private static void CopyTree(string source, string destination)
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(StudioText.T("Choose a real skill directory, not a directory link."));
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
            foreach (string child in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(child);
                if (name == ".git" || name == "__pycache__" || name == ".venv") continue;
                CopyTree(child, Path.Combine(destination, name));
            }
        }
    }
}

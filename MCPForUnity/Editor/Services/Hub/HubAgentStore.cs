using System;
using System.IO;
using MCPForUnity.Editor.Helpers;
using UnityEngine;

namespace MCPForUnity.Editor.Services.Hub
{
    /// <summary>
    /// Persists the <see cref="HubAgentRegistry"/> as JSON under the project's UserSettings folder
    /// (per-user, normally not committed) so the agent set and its tokens survive domain reloads.
    /// </summary>
    public static class HubAgentStore
    {
        private const string FileName = "NovahnHubAgents.json";

        private static string FilePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "UserSettings", FileName);
        }

        public static HubAgentRegistry Load()
        {
            try
            {
                string path = FilePath();
                if (!File.Exists(path))
                    return new HubAgentRegistry();
                var registry = JsonUtility.FromJson<HubAgentRegistry>(File.ReadAllText(path));
                return registry ?? new HubAgentRegistry();
            }
            catch (Exception ex)
            {
                McpLog.Warn($"Failed to load hub agents: {ex.Message}");
                return new HubAgentRegistry();
            }
        }

        public static void Save(HubAgentRegistry registry)
        {
            try
            {
                string path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(registry, true));
            }
            catch (Exception ex)
            {
                McpLog.Warn($"Failed to save hub agents: {ex.Message}");
            }
        }
    }
}

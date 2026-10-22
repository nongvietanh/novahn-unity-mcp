using MCPForUnity.Editor.Constants;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services.Server;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services.Hub
{
    public static class StudioHubRuntime
    {
        public static string InstanceId => Hash128.Compute(StudioWorkspace.ProjectRoot.ToLowerInvariant()).ToString();
        public static string LogPath => Path.Combine(StudioWorkspace.ProjectRoot, "Library", "NovahnStudio", "hub.log");
        private static string PidPath => Path.Combine(StudioWorkspace.SettingsRoot, "hub-process.txt");
        private static Process process;

        public static bool IsRunning
        {
            get
            {
                try
                {
                    if (process != null) return !process.HasExited;
                    if (!File.Exists(PidPath)) return false;
                    string[] identity = File.ReadAllText(PidPath).Split('|');
                    var candidate = Process.GetProcessById(int.Parse(identity[0]));
                    if (candidate.StartTime.ToUniversalTime().Ticks.ToString() != identity[1]) return false;
                    process = candidate;
                    return !process.HasExited;
                }
                catch { return false; }
            }
        }

        public static void Start()
        {
            if (IsRunning) throw new InvalidOperationException(StudioText.T("The Hub is running. Stop it before changing the team."));
            var registry = StudioWorkspace.Team;
            if (!registry.IsValid(out string invalid)) throw new InvalidOperationException(StudioText.T(invalid));
            int dashboardPort = StudioWorkspace.Data.dashboardPort;
            if (dashboardPort < 1024 || dashboardPort > 65535 || registry.agents.Any(a => a.port == dashboardPort))
                throw new InvalidOperationException(StudioText.T("The dashboard port must differ from agent ports and be between 1024 and 65535."));
            var used = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(e => e.Port).ToArray();
            var conflict = registry.agents.Select(a => a.port).Concat(new[] { dashboardPort }).FirstOrDefault(used.Contains);
            if (conflict != 0) throw new InvalidOperationException(StudioText.T("Port ") + conflict + StudioText.T(" is in use. Choose another port in AI Team."));
            string uvx = MCPServiceLocator.Paths.GetUvxPath();
            if (string.IsNullOrEmpty(uvx)) throw new InvalidOperationException(StudioText.T("uvx was not found. Install uv or set its path in Settings."));
            string server = Path.GetFullPath(Path.Combine(AssetPathUtility.GetMcpPackageRootPath(), "Editor", "StudioServer~"));
            if (!File.Exists(Path.Combine(server, "pyproject.toml"))) throw new DirectoryNotFoundException(StudioText.T("The Novahn Studio Hub backend is missing."));
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            Directory.CreateDirectory(StudioWorkspace.SettingsRoot);
            File.WriteAllText(LogPath, "Novahn Studio Hub\n");
            var start = new TerminalLauncher().CreateHeadlessProcessStartInfo(
                "\"" + uvx + "\" --from \"" + server + "\" novahn-studio-hub", LogPath);
            start.WorkingDirectory = StudioWorkspace.ProjectRoot;
            // Load the editable project copy even when uvx reuses its dependency environment.
            start.EnvironmentVariables["PYTHONPATH"] = Path.Combine(server, "src");
            foreach (var pair in registry.BuildEnvironment(HttpEndpointUtility.GetLocalBaseUrl()))
                start.EnvironmentVariables[pair.Key] = pair.Value;
            start.EnvironmentVariables["UNITY_HUB_LEADER"] = registry.Leader.id;
            start.EnvironmentVariables["UNITY_HUB_DASHBOARD_PORT"] = dashboardPort.ToString();
            start.EnvironmentVariables["UNITY_HUB_INSTANCE_ID"] = InstanceId;
            start.EnvironmentVariables["UNITY_HUB_DB_PATH"] = Path.Combine(StudioWorkspace.SettingsRoot, "hub.sqlite3");
            process = new Process { StartInfo = start };
            process.Start();
            File.WriteAllText(PidPath, process.Id + "|" + process.StartTime.ToUniversalTime().Ticks);
            StudioWorkspace.Notify(StudioText.T("Starting Hub; dependencies may need downloading on first launch…"));
            _ = StudioWorkspace.RefreshAsync();
        }

        public static void Stop()
        {
            if (IsRunning)
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // uvx launches Python as a child; stop the owned tree, not a shared MCP server.
                    using (var killer = Process.Start(new ProcessStartInfo {
                        FileName = "taskkill.exe", Arguments = "/PID " + process.Id + " /T /F",
                        UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                        killer?.WaitForExit(3000);
                }
                else process.Kill();
            }
            process?.Dispose(); process = null;
            if (File.Exists(PidPath)) File.Delete(PidPath);
            StudioWorkspace.Notify(StudioText.T("Hub stopped"));
            _ = StudioWorkspace.RefreshAsync();
        }

    }
}

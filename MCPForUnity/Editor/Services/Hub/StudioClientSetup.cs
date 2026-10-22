using MCPForUnity.Editor.Constants;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using MCPForUnity.External.Tommy;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Services.Hub
{
    /// <summary>Merge a named endpoint into a client's configuration, retaining its other servers.</summary>
    public static class StudioClientSetup
    {
        public static List<IMcpClientConfigurator> AvailableClients() => MCPServiceLocator.Client.GetAllClients()
            .Where(c => c.SupportedTransports.Contains(ConfiguredTransport.Http) || c is JsonFileMcpConfigurator)
            .OrderBy(c => c.DisplayName).ToList();
        // Command-line AIs, by client ID, that can run without a desktop window.
        public static readonly IReadOnlyDictionary<string, string> CliExecutables = new Dictionary<string, string> {
            ["claudecode"] = "claude", ["codex"] = "codex", ["geminicli"] = "gemini", ["githubcopilotcli"] = "copilot",
            ["codebuddycli"] = "codebuddy", ["qwencode"] = "qwen", ["opencode"] = "opencode", ["kimicode"] = "kimi" };

        public sealed class DetectedClient
        {
            public IMcpClientConfigurator Client;
            public string CliPath;
        }

        public static List<IMcpClientConfigurator> CliClients() => AvailableClients().Where(c => CliExecutables.ContainsKey(c.Id)).ToList();

        public static List<DetectedClient> ScanMachine() => ScanMachine(CliClients(), exe => ExecPath.FindInPath(exe));

        // Team members run without a window, so only command-line AIs found on PATH qualify.
        public static List<DetectedClient> ScanMachine(IEnumerable<IMcpClientConfigurator> clients, Func<string, string> findInPath)
        {
            var found = new List<DetectedClient>();
            foreach (var client in clients)
            {
                if (!CliExecutables.TryGetValue(client.Id, out string exe)) continue;
                string path = findInPath(exe);
                if (!string.IsNullOrEmpty(path)) found.Add(new DetectedClient { Client = client, CliPath = path });
            }
            return found.OrderBy(d => d.Client.DisplayName).ToList();
        }

        public static IMcpClientConfigurator RequireClient(string key) => AvailableClients().FirstOrDefault(c => c.Id == key)
            ?? throw new ArgumentException("Select a supported AI client.");
        private static string AutomaticName(IMcpClientConfigurator client, HubAgentEntry except = null)
        {
            string name = client.DisplayName;
            int index = 2;
            while (StudioWorkspace.Team.agents.Any(a => a != except && a.displayName == name)) name = client.DisplayName + " " + index++;
            return name;
        }
        public static HubAgentEntry AddWorker(string key, string name = null)
        {
            var client = RequireClient(key);
            if (!CliExecutables.ContainsKey(client.Id)) throw new ArgumentException(StudioText.T("Team members must be command-line AIs."));
            return StudioWorkspace.Team.AddAgent(string.IsNullOrWhiteSpace(name) ? AutomaticName(client) : name, client.Id);
        }

        /// <summary>The AI chatting with the user is the primary. Keeps the leader's id, port and token so its tasks stay valid.</summary>
        public static HubAgentEntry ClaimPrimary(string key)
        {
            var client = RequireClient(key);
            var leader = StudioWorkspace.Team.Leader;
            if (leader == null)
            {
                leader = StudioWorkspace.Team.AddAgent(AutomaticName(client), client.Id);
                StudioWorkspace.Team.SetLeader(leader.id);
            }
            else ChangeClient(leader, client.Id);
            return leader;
        }
        public static void ChangeClient(HubAgentEntry agent, string key)
        {
            var client = RequireClient(key);
            if (agent.clientKey == key) return;
            agent.clientKey = key; agent.displayName = AutomaticName(client, agent);
            agent.configuredAt = null; agent.configuredPath = null;
        }
        public static IMcpClientConfigurator Client(HubAgentEntry agent) =>
            MCPServiceLocator.Client.GetAllClients().FirstOrDefault(c => c.Id == agent.clientKey);
        public static string ServerKey(HubAgentEntry agent) => "novahn-" + StudioHubRuntime.InstanceId.Substring(0, 8) + "-" + agent.id;
        public static string Endpoint(HubAgentEntry agent) => "http://127.0.0.1:" + agent.port + "/mcp";
        private static string UnityKey => "novahn-unity-" + StudioHubRuntime.InstanceId.Substring(0, 8);
        private static string UnityUrl => HttpEndpointUtility.GetLocalBaseUrl().TrimEnd('/') + "/mcp";

        public static bool CanConfigure(HubAgentEntry agent) => Client(agent) is JsonFileMcpConfigurator ||
            agent.clientKey == "codex" || agent.clientKey == "claudecode";

        public static string ConfigPath(HubAgentEntry agent)
        {
            if (agent.clientKey == "claudecode") return Path.Combine(StudioWorkspace.ProjectRoot, ".mcp.json");
            return Client(agent)?.GetConfigPath();
        }

        public static string Snippet(HubAgentEntry agent)
        {
            if (agent.clientKey == "codex")
                return "[mcp_servers." + ServerKey(agent) + "]\nurl = \"" + Endpoint(agent) +
                    "\"\nhttp_headers = { Authorization = \"Bearer " + agent.token + "\" }\n\n" +
                    "[mcp_servers." + UnityKey + "]\nurl = \"" + UnityUrl + "\"\n";
            var model = (Client(agent) as McpClientConfiguratorBase)?.Client;
            string container = model?.IsVsCodeLayout == true ? "servers" : model?.ServerContainerKey ?? "mcpServers";
            return new JObject { [container] = new JObject { [ServerKey(agent)] = JsonEntry(agent), [UnityKey] = UnityEntry(agent) } }.ToString();
        }

        private static JObject JsonEntry(HubAgentEntry agent)
        {
            if (!Client(agent).SupportedTransports.Contains(ConfiguredTransport.Http)) return StdioEntry(Endpoint(agent), agent.token);
            var model = (Client(agent) as McpClientConfiguratorBase)?.Client;
            var entry = new JObject {
                ["type"] = model?.HttpTypeValue ?? "http", [model?.HttpUrlProperty ?? "url"] = Endpoint(agent),
                ["headers"] = new JObject { ["Authorization"] = "Bearer " + agent.token } };
            if (model != null)
                foreach (var field in model.DefaultUnityFields) entry[field.Key] = JToken.FromObject(field.Value);
            return entry;
        }

        private static JObject UnityEntry(HubAgentEntry agent)
        {
            if (!Client(agent).SupportedTransports.Contains(ConfiguredTransport.Http)) return StdioEntry(UnityUrl, null);
            var entry = JsonEntry(agent);
            entry.Remove("headers");
            var model = (Client(agent) as McpClientConfiguratorBase)?.Client;
            entry[model?.HttpUrlProperty ?? "url"] = UnityUrl;
            return entry;
        }

        private static JObject StdioEntry(string url, string token)
        {
            string uvx = MCPServiceLocator.Paths.GetUvxPath();
            if (string.IsNullOrEmpty(uvx)) throw new InvalidOperationException(StudioText.T("uvx was not found. Install uv or set its path in Settings."));
            string backend = Path.GetFullPath(Path.Combine(AssetPathUtility.GetMcpPackageRootPath(), "Editor", "StudioServer~"));
            return new JObject {
                ["command"] = uvx,
                ["args"] = new JArray("--from", backend, "python", "-m", "hub.stdio_proxy"),
                ["env"] = new JObject { ["PYTHONPATH"] = Path.Combine(backend, "src"),
                    ["NOVAHN_PROXY_URL"] = url, ["NOVAHN_PROXY_TOKEN"] = token ?? "" }
            };
        }

        public static void Configure(HubAgentEntry agent, string pathOverride = null)
        {
            if (!CanConfigure(agent)) throw new InvalidOperationException(StudioText.T("This client uses manual configuration. Copy the configuration and follow the client instructions."));
            string path = Path.GetFullPath(pathOverride ?? ConfigPath(agent));
            string existing = File.Exists(path) ? File.ReadAllText(path) : "";
            string updated = agent.clientKey == "codex" ? MergeToml(existing, agent) : MergeJson(existing, agent);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                string backup = Path.Combine(StudioWorkspace.SettingsRoot, "ConfigBackups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(backup);
                File.Copy(path, Path.Combine(backup, agent.id + "-" + Path.GetFileName(path)), true);
            }
            File.WriteAllText(path, updated);
            if (pathOverride == null)
            {
                agent.configuredPath = path; agent.configuredAt = DateTime.UtcNow.ToString("o");
                StudioWorkspace.Save();
            }
        }

        public static string MergeJson(string existing, HubAgentEntry agent)
        {
            // JObject accepts JSONC comments. Invalid configuration must fail before writing.
            var root = string.IsNullOrWhiteSpace(existing) ? new JObject() : JObject.Parse(existing);
            var model = (Client(agent) as McpClientConfiguratorBase)?.Client;
            string key = model?.IsVsCodeLayout == true ? "servers" : model?.ServerContainerKey ?? "mcpServers";
            if (root[key] != null && !(root[key] is JObject)) throw new InvalidOperationException(StudioText.T("Section ") + key + StudioText.T(" is not a JSON object."));
            var servers = root[key] as JObject ?? new JObject();
            servers[ServerKey(agent)] = JsonEntry(agent);
            var urlKey = model?.HttpUrlProperty ?? "url";
            if (!servers.Properties().Any(p => (string)(p.Value as JObject)?[urlKey] == UnityUrl))
                servers[UnityKey] = UnityEntry(agent);
            root[key] = servers;
            return root.ToString();
        }

        public static string MergeToml(string existing, HubAgentEntry agent)
        {
            var root = string.IsNullOrWhiteSpace(existing) ? new TomlTable() : TOML.Parse(new StringReader(existing));
            TomlTable servers;
            if (root.TryGetNode("mcp_servers", out var node))
                servers = node as TomlTable ?? throw new InvalidOperationException(StudioText.T("mcp_servers is not a TOML table."));
            else { servers = new TomlTable(); root["mcp_servers"] = servers; }
            var entry = new TomlTable();
            entry["url"] = new TomlString { Value = Endpoint(agent) };
            var headers = new TomlTable { IsInline = true };
            headers["Authorization"] = new TomlString { Value = "Bearer " + agent.token };
            entry["http_headers"] = headers;
            servers[ServerKey(agent)] = entry;
            if (!servers.Children.OfType<TomlTable>().Any(t => t.TryGetNode("url", out var url) && (url as TomlString)?.Value == UnityUrl))
            {
                var unity = new TomlTable(); unity["url"] = new TomlString { Value = UnityUrl }; servers[UnityKey] = unity;
            }
            using (var writer = new StringWriter()) { root.WriteTo(writer); return writer.ToString(); }
        }

        public static string JoinPrompt(HubAgentEntry agent)
        {
            string role = agent.isLeader ? StudioText.T("Primary AI, team coordinator") : StudioText.T("Worker AI, assigned task execution");
            return StudioText.T("Join Novahn Studio for project ") + StudioWorkspace.ProjectRoot + ".\n" +
                StudioText.T("You are ") + agent.id + " — " + role + StudioText.T(". Only use endpoint ") + ServerKey(agent) + StudioText.T(" for hub.*.\n") +
                StudioText.T("Read skill ") + (agent.isLeader ? "novahn-coordinator" : "novahn-worker") + StudioText.T(" and novahn-unity.\n") +
                StudioText.T("Responsibility: ") + (string.IsNullOrWhiteSpace(agent.responsibility) ? StudioText.T("As assigned per task.") : agent.responsibility) + "\n" +
                StudioText.T("Call hub.agents.list to verify your identity. Keep receiving work with hub.task.wait(timeout_seconds=45) until I stop the session; wait again after a timeout.\n") +
                StudioText.T("Use the existing Unity MCP connection to edit the project; use hub.* to delegate and report results. ") +
                StudioText.T("Follow AGENTS.md and select project ") + new DirectoryInfo(StudioWorkspace.ProjectRoot).Name + StudioText.T(" when multiple Editors are connected.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MCPForUnity.Editor.Services.Hub
{
    /// <summary>One AI connected to the Multi-Agent Hub: its id, port, token and role.</summary>
    [Serializable]
    public class HubAgentEntry
    {
        public string id;
        public string displayName;
        public string clientKey;   // which IMcpClientConfigurator this maps to
        public int port;
        public string token;
        public bool isLeader;
        public string responsibility;
        public string skillInstallPath;
        public string configuredPath;
        public string configuredAt;
    }

    /// <summary>
    /// The set of agents the Multi-Agent Hub will expose. Pure, serializable logic:
    /// port allocation, token generation, the single-leader invariant and the environment
    /// variables that launch <c>python -m hub</c>. No Unity or file IO so it is fully
    /// unit-testable; persistence lives in <see cref="HubAgentStore"/>.
    /// </summary>
    [Serializable]
    public class HubAgentRegistry
    {
        public const int BasePort = 4317;
        public const int BridgePort = 8080;
        private static readonly Regex IdPattern = new Regex("[^a-z0-9_-]+");

        public List<HubAgentEntry> agents = new List<HubAgentEntry>();

        public HubAgentEntry Leader => agents.FirstOrDefault(agent => agent.isLeader);

        /// <summary>Add a worker, allocating a free port and a fresh token. Only <see cref="SetLeader"/> picks the leader.</summary>
        public HubAgentEntry AddAgent(string displayName, string clientKey)
        {
            string id = UniqueId(displayName, clientKey);
            var entry = new HubAgentEntry
            {
                id = id,
                displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim(),
                clientKey = clientKey,
                port = NextFreePort(),
                token = GenerateToken(),
            };
            agents.Add(entry);
            return entry;
        }

        public void RemoveAgent(string id)
        {
            // A worker never inherits the leader role: the chatting AI claims it by connecting.
            agents.RemoveAll(agent => agent.id == id);
        }

        public void SetLeader(string id)
        {
            if (!agents.Any(agent => agent.id == id)) throw new ArgumentException("Unknown agent: " + id);
            foreach (var agent in agents) agent.isLeader = agent.id == id;
        }

        public int NextFreePort()
        {
            var used = new HashSet<int>(agents.Select(agent => agent.port)) { BridgePort, 4320 };
            int port = BasePort;
            while (used.Contains(port)) port++;
            return port;
        }

        /// <summary>The UNITY_HUB_AGENTS value: "id:port,id:port,...".</summary>
        public string BuildAgentsEnvValue()
        {
            return string.Join(",", agents.Select(agent => agent.id + ":" + agent.port));
        }

        /// <summary>All environment variables needed to launch the hub for this agent set.</summary>
        public Dictionary<string, string> BuildEnvironment(string bridgeUrl)
        {
            var env = new Dictionary<string, string>
            {
                ["UNITY_HUB_BRIDGE_URL"] = bridgeUrl,
                ["UNITY_HUB_AGENTS"] = BuildAgentsEnvValue(),
            };
            foreach (var agent in agents)
                env["UNITY_HUB_TOKEN_" + agent.id.ToUpperInvariant().Replace("-", "_")] = agent.token;
            return env;
        }

        /// <summary>True when the set can launch: 1+ agents, unique ids/ports, exactly one leader.</summary>
        public bool IsValid(out string error)
        {
            if (agents.Count == 0) { error = "Add at least one agent."; return false; }
            if (agents.Any(a => a.port < 1024 || a.port > 65535))
            { error = "Cổng AI phải nằm trong khoảng 1024–65535."; return false; }
            if (agents.Any(a => string.IsNullOrEmpty(a.token) || a.token.Length < 32))
            { error = "Token của AI không hợp lệ. Xóa và thêm lại AI đó."; return false; }
            if (agents.Select(a => a.id).Distinct().Count() != agents.Count)
            { error = "Agent ids must be unique."; return false; }
            if (agents.Select(a => a.id.Replace('-', '_')).Distinct().Count() != agents.Count)
            { error = "ID của AI không được chỉ khác nhau dấu gạch ngang/gạch dưới."; return false; }
            var ports = agents.Select(a => a.port).ToList();
            ports.Add(BridgePort);
            if (ports.Distinct().Count() != ports.Count)
            { error = "Agent ports must be distinct and must not use the bridge port."; return false; }
            if (agents.Count(a => a.isLeader) != 1)
            { error = "Connect from your chat AI first: it becomes the primary AI."; return false; }
            error = null;
            return true;
        }

        private string UniqueId(string displayName, string clientKey)
        {
            string seed = !string.IsNullOrWhiteSpace(displayName) ? displayName
                : (!string.IsNullOrWhiteSpace(clientKey) ? clientKey : "agent");
            string slug = IdPattern.Replace(seed.Trim().ToLowerInvariant(), "-").Trim('-');
            if (string.IsNullOrEmpty(slug)) slug = "agent";
            if (slug.Length > 32) slug = slug.Substring(0, 32).Trim('-');
            slug = slug.Replace('_', '-');
            var existing = new HashSet<string>(agents.Select(a => a.id.Replace('_', '-')));
            if (!existing.Contains(slug)) return slug;
            for (int n = 2; ; n++)
            {
                string candidate = (slug.Length > 30 ? slug.Substring(0, 30) : slug) + "-" + n;
                if (!existing.Contains(candidate)) return candidate;
            }
        }

        /// <summary>A 48-char URL-safe token (ASCII, no whitespace) — satisfies the hub's token rules.</summary>
        public static string GenerateToken()
        {
            byte[] bytes = new byte[36];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            var sb = new StringBuilder(Convert.ToBase64String(bytes));
            sb.Replace('+', '-').Replace('/', '_').Replace("=", string.Empty);
            return sb.ToString();
        }
    }
}

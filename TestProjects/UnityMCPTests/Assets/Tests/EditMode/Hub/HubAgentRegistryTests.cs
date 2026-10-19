using System.Linq;
using MCPForUnity.Editor.Services.Hub;
using NUnit.Framework;

namespace MCPForUnityTests.Editor.Hub
{
    /// <summary>
    /// Covers the pure Multi-Agent Hub registry logic: port/token allocation, the
    /// single-leader invariant, environment-variable building and validation.
    /// </summary>
    public class HubAgentRegistryTests
    {
        [Test]
        public void AddAgent_FirstBecomesLeaderAndGetsBasePort()
        {
            var registry = new HubAgentRegistry();
            var first = registry.AddAgent("Claude Code", "claude-code");

            Assert.IsTrue(first.isLeader);
            Assert.AreEqual(HubAgentRegistry.BasePort, first.port);
            Assert.AreEqual("claude-code", first.id);
            Assert.GreaterOrEqual(first.token.Length, 32);
            Assert.IsFalse(first.token.Any(char.IsWhiteSpace));
        }

        [Test]
        public void AddAgent_AllocatesDistinctPortsAndIds()
        {
            var registry = new HubAgentRegistry();
            registry.AddAgent("Codex", "codex");
            registry.AddAgent("Codex", "codex"); // same display name -> id must be de-duped

            var ids = registry.agents.Select(a => a.id).ToList();
            var ports = registry.agents.Select(a => a.port).ToList();
            Assert.AreEqual(ids.Distinct().Count(), ids.Count);
            Assert.AreEqual(ports.Distinct().Count(), ports.Count);
            Assert.AreEqual(new[] { 4317, 4318 }, ports.ToArray());
            Assert.AreEqual(1, registry.agents.Count(a => a.isLeader));
        }

        [Test]
        public void NextFreePort_SkipsBridgeAndUsedPorts()
        {
            var registry = new HubAgentRegistry();
            registry.agents.Add(new HubAgentEntry { id = "a", port = HubAgentRegistry.BasePort });
            registry.agents.Add(new HubAgentEntry { id = "b", port = HubAgentRegistry.BasePort + 1 });
            Assert.AreEqual(HubAgentRegistry.BasePort + 2, registry.NextFreePort());

            var bridgeCase = new HubAgentRegistry();
            bridgeCase.agents.Add(new HubAgentEntry { id = "c", port = HubAgentRegistry.BridgePort });
            Assert.AreNotEqual(HubAgentRegistry.BridgePort, bridgeCase.NextFreePort());
        }

        [Test]
        public void RemoveAgent_PromotesANewLeader()
        {
            var registry = new HubAgentRegistry();
            var leader = registry.AddAgent("A", "a");
            registry.AddAgent("B", "b");

            registry.RemoveAgent(leader.id);
            Assert.AreEqual(1, registry.agents.Count);
            Assert.IsTrue(registry.agents[0].isLeader);
        }

        [Test]
        public void SetLeader_KeepsExactlyOneLeader()
        {
            var registry = new HubAgentRegistry();
            registry.AddAgent("A", "a");
            var b = registry.AddAgent("B", "b");

            registry.SetLeader(b.id);
            Assert.AreEqual(1, registry.agents.Count(x => x.isLeader));
            Assert.IsTrue(b.isLeader);
            Assert.AreEqual(b.id, registry.Leader.id);
        }

        [Test]
        public void BuildEnvironment_HasAgentsAndPerAgentTokens()
        {
            var registry = new HubAgentRegistry();
            var codex = registry.AddAgent("Codex", "codex");
            var claude = registry.AddAgent("Claude Code", "claude-code");

            var env = registry.BuildEnvironment("http://127.0.0.1:8080");
            Assert.AreEqual("http://127.0.0.1:8080", env["UNITY_HUB_BRIDGE_URL"]);
            Assert.AreEqual("codex:4317,claude-code:4318", env["UNITY_HUB_AGENTS"]);
            Assert.AreEqual(codex.token, env["UNITY_HUB_TOKEN_CODEX"]);
            // id "claude-code" -> env key UNITY_HUB_TOKEN_CLAUDE_CODE
            Assert.AreEqual(claude.token, env["UNITY_HUB_TOKEN_CLAUDE_CODE"]);
        }

        [Test]
        public void IsValid_RejectsEmptyAndNoLeader()
        {
            var empty = new HubAgentRegistry();
            Assert.IsFalse(empty.IsValid(out _));

            var noLeader = new HubAgentRegistry();
            noLeader.AddAgent("A", "a");
            noLeader.agents[0].isLeader = false;
            Assert.IsFalse(noLeader.IsValid(out _));

            var ok = new HubAgentRegistry();
            ok.AddAgent("A", "a");
            ok.AddAgent("B", "b");
            Assert.IsTrue(ok.IsValid(out _));
        }

        [Test]
        public void GenerateToken_IsUrlSafeAndLongEnough()
        {
            string token = HubAgentRegistry.GenerateToken();
            Assert.GreaterOrEqual(token.Length, 32);
            Assert.IsTrue(token.All(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            Assert.AreNotEqual(token, HubAgentRegistry.GenerateToken());
        }
    }
}

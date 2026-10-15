using MCPForUnity.Editor.Windows;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MCPForUnityTests.Editor.Windows
{
    /// <summary>
    /// Covers the pure AI Console logic: forward-only cursor merging, agent filtering,
    /// and the stable per-agent colour.
    /// </summary>
    public class AgentConsoleModelTests
    {
        [Test]
        public void AdvanceCursor_MovesForwardOnly()
        {
            Assert.AreEqual(42L, AgentConsoleModel.AdvanceCursor(10, JObject.Parse("{\"nextCursor\":42}")));
            // A smaller or equal cursor never rewinds the view.
            Assert.AreEqual(42L, AgentConsoleModel.AdvanceCursor(42, JObject.Parse("{\"nextCursor\":7}")));
            // A payload without a cursor keeps the current one.
            Assert.AreEqual(5L, AgentConsoleModel.AdvanceCursor(5, JObject.Parse("{}")));
        }

        [Test]
        public void Matches_AllAgents_ReturnsEverything()
        {
            JObject row = JObject.Parse("{\"actor\":\"codex\",\"recipient\":\"claude\"}");
            Assert.IsTrue(AgentConsoleModel.Matches(row, AgentConsoleModel.AllAgents));
            Assert.IsTrue(AgentConsoleModel.Matches(row, null));
        }

        [Test]
        public void Matches_FiltersByAnyParticipant()
        {
            JObject task = JObject.Parse("{\"actor\":\"codex\",\"assignee\":\"claude\"}");
            Assert.IsTrue(AgentConsoleModel.Matches(task, "codex"));
            Assert.IsTrue(AgentConsoleModel.Matches(task, "claude"));
            Assert.IsFalse(AgentConsoleModel.Matches(task, "gemini"));

            JObject message = JObject.Parse("{\"actor\":\"claude\",\"recipient\":\"gemini\"}");
            Assert.IsTrue(AgentConsoleModel.Matches(message, "gemini"));
        }

        [Test]
        public void AgentColor_IsStablePerAgentAndDiffersBetweenAgents()
        {
            Assert.AreEqual(AgentConsoleModel.AgentColor("codex"), AgentConsoleModel.AgentColor("codex"));
            Assert.AreNotEqual(AgentConsoleModel.AgentColor("codex"), AgentConsoleModel.AgentColor("claude"));
            Assert.AreEqual(Color.gray, AgentConsoleModel.AgentColor(""));
        }
    }
}

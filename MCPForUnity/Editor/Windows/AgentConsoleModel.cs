using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Windows
{
    /// <summary>
    /// Pure, UI-free logic for the AI Console: cursor merging, agent filtering and the
    /// deterministic per-agent colour. Kept separate from <see cref="AgentConsoleWindow"/>
    /// so it can be exercised in EditMode tests without an EditorWindow.
    /// </summary>
    public static class AgentConsoleModel
    {
        public const string AllAgents = "All";

        /// <summary>The transcript cursor only ever moves forward.</summary>
        public static long AdvanceCursor(long current, JObject snapshot)
        {
            long next = (long?)(snapshot?["nextCursor"]) ?? current;
            return next > current ? next : current;
        }

        /// <summary>True when a transcript row involves the filtered agent.</summary>
        public static bool Matches(JObject row, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == AllAgents)
                return true;
            return (string)row["actor"] == filter
                   || (string)row["assignee"] == filter
                   || (string)row["recipient"] == filter;
        }

        /// <summary>Stable colour derived from the agent id, so each AI keeps one hue.</summary>
        public static Color AgentColor(string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
                return Color.gray;
            int hash = 0;
            foreach (char c in agentId)
                hash = hash * 31 + c;
            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.95f);
        }
    }
}

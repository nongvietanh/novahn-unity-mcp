using MCPForUnity.Editor.Constants;
using System;
using System.Linq;
using MCPForUnity.Editor.Services.Hub;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Studio
{
    internal sealed class StudioDashboardView
    {
        private readonly Label leader, workers, pending, complete, status;
        public StudioDashboardView(VisualElement root, Action<StudioPage> navigate)
        {
            root.Add(StudioUi.Note(StudioText.T("Chat with your primary AI in its own app. It connects through Unity MCP; command-line members receive tasks while their session is active.")));
            var stats = StudioUi.Box("studio-stat-row");
            leader = Stat(stats, StudioText.T("PRIMARY AI")); workers = Stat(stats, StudioText.T("WORKER AI"));
            pending = Stat(stats, StudioText.T("QUEUED / RUNNING")); complete = Stat(stats, StudioText.T("COMPLETED")); root.Add(stats);
            status = StudioUi.Text("", "studio-muted"); root.Add(status);
            root.Add(StudioUi.Text(StudioText.T("Start here"), "studio-section-heading"));
            Step(root, "1", StudioText.T("Build your team"), StudioText.T("Set roles, working scope and MCP connections for each AI."), () => navigate(StudioPage.Team));
            Step(root, "2", StudioText.T("Equip your agents"), StudioText.T("Install Unity, coordination and task execution skills for each client."), () => navigate(StudioPage.Skills));
            Step(root, "3", StudioText.T("Assign and follow progress"), StudioText.T("Give your primary AI a goal in desktop chat. Track tasks and scheduled work here."), () => navigate(StudioPage.Tasks));
            Refresh();
        }
        private static Label Stat(VisualElement parent, string title)
        { var box = StudioUi.Box("studio-stat"); var value = StudioUi.Text("0", "studio-stat-value"); box.Add(value); box.Add(StudioUi.Text(title, "studio-stat-label")); parent.Add(box); return value; }
        private static void Step(VisualElement parent, string number, string title, string description, Action action)
        {
            var row = StudioUi.Box("studio-step"); row.Add(StudioUi.Text(number, "studio-step-number"));
            var copy = StudioUi.Box("studio-step-copy"); copy.Add(StudioUi.Text(title, "studio-card-title")); copy.Add(StudioUi.Text(description));
            row.Add(copy); row.Add(StudioUi.Button(StudioText.T("Open"), action)); parent.Add(row);
        }
        public void Refresh()
        {
            leader.text = StudioWorkspace.Team.Leader == null ? "—" : StudioWorkspace.Team.Leader.displayName;
            workers.text = StudioWorkspace.Team.agents.Count(a => !a.isLeader).ToString();
            var counts = StudioWorkspace.Snapshot?["taskCounts"];
            pending.text = counts == null ? "—" : (((int?)counts["queued"] ?? 0) + ((int?)counts["running"] ?? 0)).ToString();
            complete.text = counts == null ? "—" : ((int?)counts["completed"] ?? 0).ToString();
            status.text = StudioWorkspace.Status;
        }
    }
}

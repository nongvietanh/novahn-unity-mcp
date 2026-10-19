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
            var hero = StudioUi.Box("studio-hero");
            hero.Add(StudioUi.Text(StudioText.T("YOUR TEAM. YOUR WORKFLOW."), "studio-kicker"));
            hero.Add(StudioUi.Text(StudioText.T("One goal.\nYour whole team at work."), "studio-hero-title"));
            hero.Add(StudioUi.Text(StudioText.T("Choose a primary AI to plan, delegate to workers and track progress from your desktop chat."), "studio-hero-copy"));
            var actions = StudioUi.Box("studio-row");
            actions.Add(StudioUi.Button(StudioText.T("Set up AI team  →"), () => navigate(StudioPage.Team), true));
            actions.Add(StudioUi.Button(StudioText.T("Assign a goal"), () => navigate(StudioPage.Tasks)));
            hero.Add(actions); root.Add(hero);
            var desktop = StudioUi.Card(StudioText.T("Desktop chat → Unity"));
            desktop.Add(StudioUi.Text(StudioText.T("Give your request in Codex Desktop or another connected AI client. The AI can connect, accept a role and delegate through novahn_workspace.")));
            desktop.Add(StudioUi.Note(StudioText.T("Unity must be open with MCP connected. Workers need an active AI session to receive work; the Hub does not launch AI models.")));
            root.Add(desktop);
            var stats = StudioUi.Box("studio-stat-row");
            leader = Stat(stats, StudioText.T("PRIMARY AI")); workers = Stat(stats, StudioText.T("WORKER AI"));
            pending = Stat(stats, StudioText.T("QUEUED / RUNNING")); complete = Stat(stats, StudioText.T("COMPLETED")); root.Add(stats);
            root.Add(StudioUi.Text(StudioText.T("Start here"), "studio-section-heading"));
            Step(root, "01", StudioText.T("Build your team"), StudioText.T("Set roles, working scope and MCP connections for each AI."), () => navigate(StudioPage.Team));
            Step(root, "02", StudioText.T("Equip your agents"), StudioText.T("Install Unity, coordination and task execution skills for each client."), () => navigate(StudioPage.Skills));
            Step(root, "03", StudioText.T("Assign and follow progress"), StudioText.T("Give your primary AI a goal in desktop chat. Track tasks and scheduled work here."), () => navigate(StudioPage.Tasks));
            status = StudioUi.Note(""); root.Add(status);
            Refresh();
        }
        private static Label Stat(VisualElement parent, string title)
        { var box = StudioUi.Box("studio-stat"); var value = StudioUi.Text("0", "studio-stat-value"); box.Add(value); box.Add(StudioUi.Text(title, "studio-stat-label")); parent.Add(box); return value; }
        private static void Step(VisualElement parent, string number, string title, string description, Action action)
        {
            var row = StudioUi.Box("studio-step"); row.Add(StudioUi.Text(number, "studio-step-number"));
            var copy = StudioUi.Box("studio-step-copy"); copy.Add(StudioUi.Text(title, "studio-card-title")); copy.Add(StudioUi.Text(description));
            row.Add(copy); row.Add(StudioUi.Button(StudioText.T("Open  ↗"), action)); parent.Add(row);
        }
        public void Refresh()
        {
            leader.text = StudioWorkspace.Team.Leader == null ? "0" : "1";
            workers.text = StudioWorkspace.Team.agents.Count(a => !a.isLeader).ToString();
            var counts = StudioWorkspace.Snapshot?["taskCounts"];
            pending.text = counts == null ? "—" : (((int?)counts["queued"] ?? 0) + ((int?)counts["running"] ?? 0)).ToString();
            complete.text = counts == null ? "—" : ((int?)counts["completed"] ?? 0).ToString();
            status.text = StudioWorkspace.Status + StudioText.T(". Chat with your AI in its desktop app. Connected workers receive tasks while their receive loop is active.");
        }
    }
}

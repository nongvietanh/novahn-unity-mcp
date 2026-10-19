using MCPForUnity.Editor.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Hub;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Studio
{
    internal sealed class StudioTeamView
    {
        private readonly VisualElement roster, addForm;
        private readonly Label feedback, status;
        private readonly Button toggleHub;
        private readonly IntegerField dashboardPort;
        private readonly DropdownField primaryClient;
        private readonly List<IMcpClientConfigurator> clients;
        private readonly List<VisualElement> locked = new List<VisualElement>();
        private readonly Dictionary<string, Label> activity = new Dictionary<string, Label>();
        private string signature;

        public StudioTeamView(VisualElement root)
        {
            clients = StudioClientSetup.AvailableClients();
            root.Add(StudioUi.Note(StudioText.T("Connect from your desktop AI chat to set up roles and receive work. You can also add members here. Each session uses its assigned identity.")));
            var controls = StudioUi.Card(); var row = StudioUi.Box("studio-row");
            toggleHub = StudioUi.Button(StudioText.T("Start Hub"), () => StudioUi.Safe(() => {
                if (StudioHubRuntime.IsRunning) StudioHubRuntime.Stop(); else StudioHubRuntime.Start(); Refresh();
            }, feedback), true);
            row.Add(toggleHub);
            row.Add(StudioUi.Button(StudioText.T("Refresh"), () => StudioUi.Run(StudioWorkspace.RefreshAsync, feedback)));
            row.Add(StudioUi.Button(StudioText.T("Open Hub log"), () => {
                if (File.Exists(StudioHubRuntime.LogPath)) EditorUtility.RevealInFinder(StudioHubRuntime.LogPath);
                else feedback.text = StudioText.T("No log yet. Start the Hub first.");
            }));
            controls.Add(row);
            primaryClient = new DropdownField(StudioText.T("Primary AI"), ClientChoices(), 0) { name = "primary-ai-client" };
            primaryClient.RegisterValueChangedCallback(e => StudioUi.Safe(() => {
                var chosen = clients.FirstOrDefault(c => c.DisplayName == e.newValue);
                if (chosen == null) return;
                ChangeTeam(() => {
                    var leader = StudioWorkspace.Team.Leader;
                    var member = StudioWorkspace.Team.agents.FirstOrDefault(a => a.clientKey == chosen.Id);
                    if (member != null) StudioWorkspace.Team.SetLeader(member.id);
                    else if (leader != null) StudioClientSetup.ChangeClient(leader, chosen.Id);
                    else StudioClientSetup.AddClient(chosen.Id);
                });
            }, feedback));
            controls.Add(StudioUi.Field(primaryClient));
            status = StudioUi.Text(""); controls.Add(status);
            dashboardPort = new IntegerField(StudioText.T("Dashboard port")) { value = StudioWorkspace.Data.dashboardPort, isDelayed = true };
            dashboardPort.RegisterValueChangedCallback(e => { StudioWorkspace.Data.dashboardPort = e.newValue; StudioWorkspace.Save(); });
            controls.Add(dashboardPort); root.Add(controls);
            addForm = StudioUi.Card(StudioText.T("Add member"));
            var picker = new DropdownField(StudioText.T("AI client"), ClientChoices(), 0) { name = "new-ai-client" };
            addForm.Add(StudioUi.Field(picker));
            addForm.Add(StudioUi.Button(StudioText.T("+ Add AI"), () => StudioUi.Safe(() => {
                var chosen = clients.FirstOrDefault(c => c.DisplayName == picker.value);
                if (chosen == null) { feedback.text = StudioText.T("Select a client first."); return; }
                ChangeTeam(() => StudioClientSetup.AddClient(chosen.Id));
            }, feedback), true)); root.Add(addForm);
            feedback = StudioUi.Text("", "studio-feedback"); root.Add(feedback);
            root.Add(StudioUi.Text(StudioText.T("Members & roles"), "studio-section-heading"));
            roster = new VisualElement(); root.Add(roster);
            Rebuild(); Refresh();
        }

        private void Rebuild()
        {
            signature = TeamSignature(); roster.Clear(); locked.Clear(); activity.Clear();
            if (StudioWorkspace.Team.agents.Count == 0)
                roster.Add(StudioUi.Text(StudioText.T("No AI yet. The first member is the primary AI. Connect from desktop chat or add members here."), "studio-empty"));
            foreach (var agent in StudioWorkspace.Team.agents.OrderByDescending(a => a.isLeader))
                roster.Add(AgentCard(agent));
        }
        private VisualElement AgentCard(HubAgentEntry agent)
        {
            var card = StudioUi.Card(); card.name = "agent-" + agent.id;
            var header = StudioUi.Box("studio-agent-header");
            header.Add(StudioUi.Text(agent.displayName.Substring(0, Math.Min(2, agent.displayName.Length)).ToUpperInvariant(), "studio-agent-avatar"));
            var meta = StudioUi.Box("studio-agent-meta"); meta.Add(StudioUi.Text(agent.displayName, "studio-card-title"));
            var lastSeen = StudioUi.Text("", "studio-agent-status"); activity[agent.id] = lastSeen; meta.Add(lastSeen); header.Add(meta);
            var role = StudioUi.Text(agent.isLeader ? StudioText.T("PRIMARY AI") : StudioText.T("WORKER AI"), "studio-chip"); role.EnableInClassList("worker", !agent.isLeader); header.Add(role); card.Add(header);
            var form = StudioUi.Box("studio-agent-form");
            var client = new DropdownField(StudioText.T("AI client"), ClientChoices(), 0);
            client.SetValueWithoutNotify(StudioClientSetup.Client(agent)?.DisplayName ?? ClientChoices()[0]);
            client.RegisterValueChangedCallback(e => StudioUi.Safe(() => {
                var chosen = clients.FirstOrDefault(c => c.DisplayName == e.newValue);
                if (chosen != null) ChangeTeam(() => StudioClientSetup.ChangeClient(agent, chosen.Id));
            }, feedback));
            var port = new IntegerField(StudioText.T("Port")) { value = agent.port, isDelayed = true };
            port.RegisterValueChangedCallback(e => { agent.port = e.newValue; agent.configuredAt = null; StudioWorkspace.Save(); });
            form.Add(client); form.Add(port); card.Add(form); locked.Add(port);
            var responsibility = new TextField(StudioText.T("Responsibility")) { value = agent.responsibility ?? "", isDelayed = true };
            responsibility.RegisterValueChangedCallback(e => { agent.responsibility = e.newValue; StudioWorkspace.Save(); }); card.Add(responsibility); locked.Add(responsibility);
            card.Add(StudioUi.Text("ID: " + agent.id + "  /  " + (StudioClientSetup.Client(agent)?.DisplayName ?? agent.clientKey) + "  /  " + StudioClientSetup.Endpoint(agent), "studio-skill-path"));
            var buttons = StudioUi.Box("studio-agent-actions");
            var setup = StudioUi.Button(StudioText.T("Setup MCP + Skills"), () => StudioUi.Safe(() => {
                StudioClientSetup.Configure(agent); int count = StudioSkillCatalog.InstallBundle(agent);
                feedback.text = StudioText.T("Configured ") + agent.displayName + StudioText.T(" and installed ") + count + StudioText.T(" skills. Reopen the client, then use the join prompt."); Refresh();
            }, feedback), true);
            setup.SetEnabled(StudioClientSetup.CanConfigure(agent)); buttons.Add(setup);
            buttons.Add(StudioUi.Button(StudioText.T("Copy configuration"), () => { EditorGUIUtility.systemCopyBuffer = StudioClientSetup.Snippet(agent); feedback.text = StudioText.T("Copied configuration for ") + agent.displayName + "."; }));
            buttons.Add(StudioUi.Button(StudioText.T("Join prompt"), () => { EditorGUIUtility.systemCopyBuffer = StudioClientSetup.JoinPrompt(agent); feedback.text = StudioText.T("Copied the prompt. Paste it into the session for ") + agent.displayName + StudioText.T(" to accept its role and check its inbox."); }));
            if (!agent.isLeader)
            {
                var lead = StudioUi.Button(StudioText.T("Make primary AI"), () => { StudioWorkspace.Team.SetLeader(agent.id); StudioWorkspace.Save(); Rebuild(); }); buttons.Add(lead); locked.Add(lead);
            }
            var remove = StudioUi.Button(StudioText.T("Remove from team"), () => StudioUi.Safe(() => {
                bool assigned = StudioWorkspace.Data.tasks.Any(t => t.assignee == agent.id && t.state != "completed" && t.state != "cancelled");
                if (assigned) throw new InvalidOperationException(StudioText.T("This AI has unfinished work. Complete or cancel its tasks first."));
                StudioWorkspace.Team.RemoveAgent(agent.id); StudioWorkspace.Save(); Rebuild();
            }, feedback)); remove.AddToClassList("danger"); buttons.Add(remove); locked.Add(remove);
            card.Add(buttons); return card;
        }
        private static string TeamSignature() => string.Join("|", StudioWorkspace.Team.agents.Select(a => a.id + a.isLeader + a.displayName));
        private List<string> ClientChoices() => new[] { StudioText.T("Choose AI client…") }.Concat(clients.Select(c => c.DisplayName)).ToList();
        private void ChangeTeam(Action change)
        {
            bool running = StudioHubRuntime.IsRunning;
            if (running) StudioHubRuntime.Stop();
            change(); StudioWorkspace.Save();
            if (running && StudioWorkspace.Team.agents.Count > 0) StudioHubRuntime.Start();
            Rebuild(); Refresh();
        }
        public void Refresh()
        {
            if (signature != TeamSignature()) Rebuild();
            bool running = StudioHubRuntime.IsRunning;
            primaryClient.SetValueWithoutNotify(StudioWorkspace.Team.Leader == null ? ClientChoices()[0] :
                StudioClientSetup.Client(StudioWorkspace.Team.Leader)?.DisplayName ?? ClientChoices()[0]);
            toggleHub.text = running ? StudioText.T("Stop Hub") : StudioText.T("Start Hub");
            status.text = StudioWorkspace.Status + (running ? StudioText.T(" • Stop the Hub before changing team members or ports.") : "");
            dashboardPort.SetEnabled(!running);
            foreach (var element in locked) element.SetEnabled(!running);
            foreach (var agent in StudioWorkspace.Team.agents)
            {
                var row = (StudioWorkspace.Snapshot?["agents"] as JArray)?.FirstOrDefault(a => (string)a["agentId"] == agent.id);
                double? seen = (double?)row?["lastSeenAt"];
                bool recent = seen.HasValue && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seen.Value < 60;
                if (activity.TryGetValue(agent.id, out var label))
                    label.text = (string.IsNullOrEmpty(agent.configuredAt) ? StudioText.T("Client not configured") : StudioText.T("Configuration saved")) +
                        (recent ? StudioText.T("  •  Recent activity") : StudioText.T("  •  No recent AI activity"));
            }
        }
    }
}

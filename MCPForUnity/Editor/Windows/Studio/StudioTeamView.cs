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
        private readonly VisualElement roster, addForm, scanResults;
        private readonly Label feedback, status;
        private readonly Button toggleHub;
        private readonly IntegerField dashboardPort;
        private readonly List<IMcpClientConfigurator> clients;
        private readonly List<VisualElement> locked = new List<VisualElement>();
        private readonly Dictionary<string, Label> activity = new Dictionary<string, Label>();
        private string signature;

        public StudioTeamView(VisualElement root)
        {
            clients = StudioClientSetup.CliClients();
            root.Add(StudioUi.Note(StudioText.T("The AI you chat with becomes the primary AI when it connects. Add command-line AIs below as team members.")));
            var controls = StudioUi.Card(StudioText.T("Team Hub")); var row = StudioUi.Box("studio-toolbar");
            status = StudioUi.Text("", "studio-toolbar-label"); row.Add(status);
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
            var advanced = new Foldout { text = StudioText.T("Advanced"), value = false };
            dashboardPort = new IntegerField(StudioText.T("Dashboard port")) { value = StudioWorkspace.Data.dashboardPort, isDelayed = true };
            dashboardPort.RegisterValueChangedCallback(e => { StudioWorkspace.Data.dashboardPort = e.newValue; StudioWorkspace.Save(); });
            advanced.Add(StudioUi.Field(dashboardPort)); controls.Add(advanced); root.Add(controls);
            feedback = StudioUi.Text("", "studio-feedback"); root.Add(feedback);
            root.Add(StudioUi.Text(StudioText.T("Members & roles"), "studio-section-heading"));
            roster = new VisualElement(); root.Add(roster);
            addForm = StudioUi.Card(StudioText.T("Add member"));
            var addRow = StudioUi.Box("studio-toolbar");
            var picker = new DropdownField(ClientChoices(), 0) { name = "new-ai-client" }; picker.AddToClassList("studio-grow"); addRow.Add(picker);
            addRow.Add(StudioUi.Button(StudioText.T("+ Add AI"), () => StudioUi.Safe(() => {
                var chosen = clients.FirstOrDefault(c => c.DisplayName == picker.value);
                if (chosen == null) { feedback.text = StudioText.T("Select a client first."); return; }
                ChangeTeam(() => StudioClientSetup.AddWorker(chosen.Id));
            }, feedback)));
            addRow.Add(StudioUi.Button(StudioText.T("Scan AI on this machine"), () => StudioUi.Safe(ShowScan, feedback)));
            addForm.Add(addRow);
            scanResults = new VisualElement(); addForm.Add(scanResults); root.Add(addForm);
            Rebuild(); Refresh();
        }

        private void Rebuild()
        {
            signature = TeamSignature(); roster.Clear(); locked.Clear(); activity.Clear();
            if (StudioWorkspace.Team.agents.Count == 0)
                roster.Add(StudioUi.Text(StudioText.T("No AI yet. Ask your chat AI to connect to Novahn Studio, then add command-line AIs as members."), "studio-empty"));
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
            card.tooltip = "ID: " + agent.id + "  /  " + StudioClientSetup.Endpoint(agent);
            var form = StudioUi.Box("studio-agent-form");
            if (!agent.isLeader)
            {
                var client = new DropdownField(StudioText.T("AI client"), ClientChoices(), 0);
                client.SetValueWithoutNotify(StudioClientSetup.Client(agent)?.DisplayName ?? ClientChoices()[0]);
                client.RegisterValueChangedCallback(e => StudioUi.Safe(() => {
                    var chosen = clients.FirstOrDefault(c => c.DisplayName == e.newValue);
                    if (chosen != null) ChangeTeam(() => StudioClientSetup.ChangeClient(agent, chosen.Id));
                }, feedback));
                form.Add(client);
            }
            var port = new IntegerField(StudioText.T("Port")) { value = agent.port, isDelayed = true };
            port.RegisterValueChangedCallback(e => { agent.port = e.newValue; agent.configuredAt = null; StudioWorkspace.Save(); });
            port.AddToClassList("studio-port-field"); form.Add(port); locked.Add(port);
            var responsibility = new TextField(StudioText.T("Responsibility")) { value = agent.responsibility ?? "", isDelayed = true };
            responsibility.RegisterValueChangedCallback(e => { agent.responsibility = e.newValue; StudioWorkspace.Save(); });
            responsibility.AddToClassList("studio-wide-field"); form.Add(responsibility); locked.Add(responsibility); card.Add(form);
            var buttons = StudioUi.Box("studio-agent-actions");
            var setup = StudioUi.Button(StudioText.T("Setup MCP + Skills"), () => StudioUi.Safe(() => {
                StudioClientSetup.Configure(agent); int count = StudioSkillCatalog.InstallBundle(agent);
                feedback.text = StudioText.T("Configured ") + agent.displayName + StudioText.T(" and installed ") + count + StudioText.T(" skills. Reopen the client, then use the join prompt."); Refresh();
            }, feedback));
            setup.SetEnabled(StudioClientSetup.CanConfigure(agent)); buttons.Add(setup);
            buttons.Add(StudioUi.Button(StudioText.T("Copy configuration"), () => { EditorGUIUtility.systemCopyBuffer = StudioClientSetup.Snippet(agent); feedback.text = StudioText.T("Copied configuration for ") + agent.displayName + "."; }));
            buttons.Add(StudioUi.Button(StudioText.T("Join prompt"), () => { EditorGUIUtility.systemCopyBuffer = StudioClientSetup.JoinPrompt(agent); feedback.text = StudioText.T("Copied the prompt. Paste it into the session for ") + agent.displayName + StudioText.T(" to accept its role and check its inbox."); }));
            buttons.Add(StudioUi.Box("studio-spacer"));
            var remove = StudioUi.Button(StudioText.T("Remove from team"), () => StudioUi.Safe(() => {
                bool assigned = StudioWorkspace.Data.tasks.Any(t => t.assignee == agent.id && t.state != "completed" && t.state != "cancelled");
                if (assigned) throw new InvalidOperationException(StudioText.T("This AI has unfinished work. Complete or cancel its tasks first."));
                StudioWorkspace.Team.RemoveAgent(agent.id); StudioWorkspace.Save(); Rebuild();
            }, feedback)); remove.AddToClassList("danger"); buttons.Add(remove); locked.Add(remove);
            card.Add(buttons); return card;
        }
        private void ShowScan()
        {
            scanResults.Clear();
            var found = StudioClientSetup.ScanMachine();
            feedback.text = StudioText.T("Found ") + found.Count + StudioText.T(" command-line AIs on this machine.");
            if (found.Count == 0) scanResults.Add(StudioUi.Text(StudioText.T("No supported command-line AI was found on PATH."), "studio-empty"));
            foreach (var item in found)
            {
                var row = StudioUi.Box("studio-step");
                var copy = StudioUi.Box("studio-step-copy");
                copy.Add(StudioUi.Text(item.Client.DisplayName, "studio-card-title"));
                copy.Add(StudioUi.Text(item.CliPath, "studio-skill-path"));
                row.Add(copy);
                if (StudioWorkspace.Team.agents.Any(a => !a.isLeader && a.clientKey == item.Client.Id)) row.Add(StudioUi.Text(StudioText.T("In team"), "studio-chip"));
                else
                {
                    string id = item.Client.Id;
                    row.Add(StudioUi.Button(StudioText.T("+ Add AI"), () => StudioUi.Safe(() => { ChangeTeam(() => StudioClientSetup.AddWorker(id)); ShowScan(); }, feedback)));
                }
                scanResults.Add(row);
            }
        }
        private static string TeamSignature() => string.Join("|", StudioWorkspace.Team.agents.Select(a => a.id + a.isLeader + a.displayName));
        private List<string> ClientChoices() => new[] { StudioText.T("Choose AI client…") }.Concat(clients.Select(c => c.DisplayName)).ToList();
        private void ChangeTeam(Action change)
        {
            bool running = StudioHubRuntime.IsRunning;
            if (running) StudioHubRuntime.Stop();
            change(); StudioWorkspace.Save();
            if (running && StudioWorkspace.Team.Leader != null) StudioHubRuntime.Start();
            Rebuild(); Refresh();
        }
        public void Refresh()
        {
            if (signature != TeamSignature()) Rebuild();
            bool running = StudioHubRuntime.IsRunning;
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

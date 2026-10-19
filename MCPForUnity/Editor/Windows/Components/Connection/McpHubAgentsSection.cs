using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Hub;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Components.Connection
{
    /// <summary>
    /// "Multi-Agent Hub" panel: register several AI clients as distinct agents (each with its
    /// own port + token), pick one leader (coordinator), and start/stop the local hub. It is a
    /// self-contained foldout so it never interferes with the single-server transport controls.
    /// </summary>
    public sealed class McpHubAgentsSection
    {
        private static readonly Color Indigo = new Color(0.486f, 0.361f, 0.988f); // #7C5CFC

        private readonly HubAgentRegistry registry;
        private readonly List<IMcpClientConfigurator> configurators;
        private readonly VisualElement agentsList;
        private readonly Label statusLabel;
        private Button startStopButton;

        public McpHubAgentsSection(VisualElement parent)
        {
            registry = HubAgentStore.Load();
            configurators = MCPServiceLocator.Client.GetAllClients().ToList();

            var foldout = new Foldout { text = "Multi-Agent Hub", value = false };
            foldout.style.marginTop = 10;
            parent.Add(foldout);

            var hint = new Label(
                "Đăng ký nhiều AI, mỗi con một agent + port riêng. Chọn 1 leader để điều phối (tự chia task qua hub). Bật hub rồi cấu hình mỗi client vào port của nó.");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.color = new Color(0.6f, 0.6f, 0.6f);
            hint.style.marginBottom = 6;
            foldout.Add(hint);

            agentsList = new VisualElement();
            foldout.Add(agentsList);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 6;
            var addButton = new Button(AddAgent) { text = "+ Add Agent" };
            buttonRow.Add(addButton);
            startStopButton = new Button(ToggleHub);
            startStopButton.style.marginLeft = 6;
            startStopButton.style.backgroundColor = Indigo;
            startStopButton.style.color = Color.white;
            startStopButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            buttonRow.Add(startStopButton);
            foldout.Add(buttonRow);

            statusLabel = new Label();
            statusLabel.style.marginTop = 4;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            foldout.Add(statusLabel);

            RebuildRows();
            RefreshStatus();
        }

        private void AddAgent()
        {
            var configurator = configurators.FirstOrDefault();
            string clientKey = configurator?.Id ?? "agent";
            string display = configurator?.DisplayName ?? "Agent";
            registry.AddAgent(display, clientKey);
            Persist();
            RebuildRows();
        }

        private void Persist() => HubAgentStore.Save(registry);

        private void RebuildRows()
        {
            agentsList.Clear();
            if (registry.agents.Count == 0)
            {
                var empty = new Label("Chưa có agent. Bấm \"+ Add Agent\".");
                empty.style.color = new Color(0.55f, 0.55f, 0.55f);
                agentsList.Add(empty);
                return;
            }
            foreach (var agent in registry.agents.ToList())
                agentsList.Add(BuildRow(agent));
        }

        private VisualElement BuildRow(HubAgentEntry agent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            // Leader radio.
            var leader = new Toggle { value = agent.isLeader };
            leader.tooltip = "Leader (điều phối)";
            leader.style.marginRight = 4;
            leader.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    registry.SetLeader(agent.id);
                    Persist();
                    RebuildRows();
                }
                else
                {
                    // Keep exactly one leader: re-check it.
                    leader.SetValueWithoutNotify(true);
                }
            });
            row.Add(leader);

            var leaderTag = new Label(agent.isLeader ? "★" : " ");
            leaderTag.style.width = 14;
            leaderTag.style.color = Indigo;
            leaderTag.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(leaderTag);

            // Client picker.
            var names = configurators.Select(c => c.DisplayName).ToList();
            int index = System.Math.Max(0, configurators.FindIndex(c => c.Id == agent.clientKey));
            var picker = new PopupField<string>(names, index);
            picker.style.minWidth = 130;
            picker.style.marginRight = 6;
            picker.RegisterValueChangedCallback(evt =>
            {
                var chosen = configurators.FirstOrDefault(c => c.DisplayName == evt.newValue);
                if (chosen != null)
                {
                    agent.clientKey = chosen.Id;
                    agent.displayName = chosen.DisplayName;
                    Persist();
                }
            });
            row.Add(picker);

            var portLabel = new Label(":" + agent.port);
            portLabel.style.width = 48;
            portLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row.Add(portLabel);

            var copyButton = new Button(() => CopyAgentConfig(agent)) { text = "Copy config" };
            copyButton.style.marginRight = 4;
            row.Add(copyButton);

            var removeButton = new Button(() =>
            {
                registry.RemoveAgent(agent.id);
                Persist();
                RebuildRows();
            })
            { text = "✕" };
            row.Add(removeButton);

            return row;
        }

        private void CopyAgentConfig(HubAgentEntry agent)
        {
            string url = $"http://127.0.0.1:{agent.port}/mcp";
            string snippet =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"novahn-hub\": {\n" +
                "      \"type\": \"http\",\n" +
                $"      \"url\": \"{url}\",\n" +
                "      \"headers\": {\n" +
                $"        \"Authorization\": \"Bearer {agent.token}\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}";
            EditorGUIUtility.systemCopyBuffer = snippet;
            statusLabel.text = $"Đã copy config cho agent \"{agent.id}\" (port {agent.port}). Dán vào client của nó.";
            statusLabel.style.color = Indigo;
        }

        private void ToggleHub()
        {
            var server = MCPServiceLocator.Server;
            if (server.IsHubRunning())
            {
                server.StopHub();
                RefreshStatus();
                return;
            }
            if (!registry.IsValid(out string invalid))
            {
                statusLabel.text = invalid;
                statusLabel.style.color = new Color(0.85f, 0.4f, 0.4f);
                return;
            }
            string bridgeUrl = HttpEndpointUtility.GetLocalBaseUrl();
            var env = registry.BuildEnvironment(bridgeUrl);
            if (server.StartHub(env, out string error))
            {
                statusLabel.text = "Đang khởi động hub… mở AI Console để xem agent nói chuyện.";
                statusLabel.style.color = Indigo;
            }
            else
            {
                statusLabel.text = "Không khởi động được hub: " + error;
                statusLabel.style.color = new Color(0.85f, 0.4f, 0.4f);
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            bool running = MCPServiceLocator.Server.IsHubRunning();
            startStopButton.text = running ? "Stop Hub" : "Start Hub";
            startStopButton.style.backgroundColor = running ? new Color(0.78f, 0.25f, 0.25f) : Indigo;
        }
    }
}

using System;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows
{
    /// <summary>A metadata-only view of the local Multi-Agent Hub.</summary>
    public sealed class MultiAgentHubWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 3;
        private Label statusLabel;
        private Label endpointLabel;
        private VisualElement instancesList;
        private VisualElement agentsList;
        private VisualElement tasksList;
        private Label messageLabel;
        private UnityWebRequest pendingRequest;
        private double nextRefreshTime;
        private string renderedLanguage;

        [MenuItem(ProductInfo.MenuRoot + "/Multi-Agent Hub", priority = 4)]
        public static void ShowWindow()
        {
            MultiAgentHubWindow window = GetWindow<MultiAgentHubWindow>("Multi-Agent Hub");
            window.minSize = new Vector2(440, 360);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            nextRefreshTime = 0;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (pendingRequest != null)
            {
                pendingRequest.Dispose();
                pendingRequest = null;
            }
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            Label title = new Label("Multi-Agent Hub");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.flexGrow = 1;
            header.Add(title);
            header.Add(new Button(RefreshNow) { text = T("Refresh") });
            root.Add(header);

            statusLabel = new Label(T("Checking Hub..."));
            statusLabel.style.marginTop = 6;
            root.Add(statusLabel);
            endpointLabel = new Label();
            endpointLabel.style.marginBottom = 8;
            root.Add(endpointLabel);

            ScrollView scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            root.Add(scroll);

            instancesList = AddSection(scroll, T("Connected Unity Editors"));
            agentsList = AddSection(scroll, T("Agents"));
            tasksList = AddSection(scroll, T("Recent Tasks"));
            VisualElement messageSection = AddSection(scroll, T("Unread Messages"));
            messageLabel = new Label(T("No data"));
            messageSection.Add(messageLabel);

            Label hint = new Label(T("This dashboard shows metadata only. Task and message bodies never enter the Editor."));
            hint.style.marginTop = 8;
            hint.style.whiteSpace = WhiteSpace.Normal;
            root.Add(hint);
            endpointLabel.text = HttpEndpointUtility.GetLocalBaseUrl() + "/hub/status";
            renderedLanguage = McpUiLocalization.CurrentLanguage;
            nextRefreshTime = 0;
        }

        private static VisualElement AddSection(VisualElement parent, string title)
        {
            VisualElement section = new VisualElement();
            section.style.marginTop = 10;
            section.style.paddingTop = 7;
            section.style.paddingBottom = 7;
            section.style.paddingLeft = 8;
            section.style.paddingRight = 8;
            section.style.borderBottomWidth = 1;
            section.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.45f);
            Label heading = new Label(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 4;
            section.Add(heading);
            parent.Add(section);
            return section;
        }

        private void RefreshNow()
        {
            nextRefreshTime = 0;
            Tick();
        }

        private void Tick()
        {
            if (statusLabel == null)
                return;

            if (renderedLanguage != McpUiLocalization.CurrentLanguage)
            {
                McpUiLocalization.Apply(rootVisualElement);
                renderedLanguage = McpUiLocalization.CurrentLanguage;
            }

            if (pendingRequest != null)
            {
                if (!pendingRequest.isDone)
                    return;
                try
                {
                    if (pendingRequest.responseCode == 200 && string.IsNullOrEmpty(pendingRequest.error))
                        Render(JObject.Parse(pendingRequest.downloadHandler.text));
                    else
                        ShowUnavailable(T("Hub unavailable: ") + pendingRequest.error);
                }
                catch (Exception exception)
                {
                    ShowUnavailable(T("Invalid Hub data: ") + exception.Message);
                }
                finally
                {
                    pendingRequest.Dispose();
                    pendingRequest = null;
                }
            }

            if (EditorApplication.timeSinceStartup < nextRefreshTime)
                return;

            string baseUrl = HttpEndpointUtility.GetLocalBaseUrl();
            endpointLabel.text = baseUrl + "/hub/status";
            pendingRequest = UnityWebRequest.Get(endpointLabel.text);
            pendingRequest.timeout = 3;
            pendingRequest.SendWebRequest();
            nextRefreshTime = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
        }

        private void ShowUnavailable(string reason)
        {
            statusLabel.text = reason;
            SetEmpty(instancesList, T("No connection"));
            SetEmpty(agentsList, T("No data"));
            SetEmpty(tasksList, T("No data"));
            messageLabel.text = T("No data");
        }

        private void Render(JObject snapshot)
        {
            if ((string)snapshot["status"] != "ready")
                throw new InvalidOperationException(T("Hub is not ready"));
            statusLabel.text = T("Hub running") + " • " + DateTime.Now.ToString("HH:mm:ss");

            ClearRows(instancesList);
            JArray instances = snapshot["instances"] as JArray;
            if (instances == null || instances.Count == 0)
                instancesList.Add(new Label(T("No Unity Editors connected")));
            else
                foreach (JToken instance in instances)
                    instancesList.Add(new Label((string)instance["project"] + " • Unity " +
                                                (string)instance["unityVersion"] + " • " +
                                                (string)instance["hash"]));

            ClearRows(agentsList);
            JArray agents = snapshot["agents"] as JArray;
            if (agents == null || agents.Count == 0)
                agentsList.Add(new Label(T("No agents")));
            else
                foreach (JToken agent in agents)
                    agentsList.Add(new Label((string)agent["agentId"] + " • :" +
                                             (string)agent["port"] + " • " +
                                             FormatLastSeen(agent["lastSeenAt"])));

            ClearRows(tasksList);
            JArray tasks = snapshot["tasks"] as JArray;
            if (tasks == null || tasks.Count == 0)
                tasksList.Add(new Label(T("No tasks")));
            else
                foreach (JToken task in tasks)
                    tasksList.Add(new Label(ShortId((string)task["task_id"]) + " • " +
                                            (string)task["state"] + " • " +
                                            (string)task["creator"] + " → " +
                                            (string)task["assignee"]));

            JObject unread = snapshot["unreadMessages"] as JObject;
            int codexUnread = unread == null ? 0 : (int?)unread["codex"] ?? 0;
            int claudeUnread = unread == null ? 0 : (int?)unread["claude"] ?? 0;
            messageLabel.text = "Codex: " + codexUnread + "    Claude Code: " + claudeUnread;
        }

        private static void ClearRows(VisualElement section)
        {
            while (section.childCount > 1)
                section.RemoveAt(1);
        }

        private static void SetEmpty(VisualElement section, string message)
        {
            ClearRows(section);
            section.Add(new Label(message));
        }

        private static string ShortId(string id)
        {
            return string.IsNullOrEmpty(id) ? "?" : id.Substring(0, Math.Min(8, id.Length));
        }

        private static string FormatLastSeen(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return T("no requests seen");
            DateTimeOffset seen = DateTimeOffset.FromUnixTimeMilliseconds(
                (long)(double)value * 1000);
            return T("last request") + " " + seen.ToLocalTime().ToString("HH:mm:ss");
        }

        private static string T(string value)
        {
            return McpUiLocalization.Translate(value);
        }
    }
}

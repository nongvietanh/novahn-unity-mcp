using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows
{
    /// <summary>
    /// Live console that streams the agent-to-agent conversation (task hand-offs and
    /// messages, with bodies) from the local Hub's /hub/transcript endpoint. Open it to
    /// watch; close it and the agents keep working in the background — this window only
    /// reads the timeline, it never drives the agents.
    /// </summary>
    public sealed class AgentConsoleWindow : EditorWindow
    {
        private const double PollIntervalSeconds = 1.5;
        private const int MaxRows = 2000;
        private const int FetchLimit = 200;
        private const string AllAgents = "All";

        private long lastCursor;
        private bool paused;
        private bool autoScroll = true;
        private string agentFilter = AllAgents;
        private readonly List<JObject> events = new();
        private readonly List<string> knownAgents = new();
        private UnityWebRequest pendingRequest;
        private double nextPollTime;
        private string renderedLanguage;

        private Label statusLabel;
        private ScrollView logScroll;
        private PopupField<string> filterField;
        private Button pauseButton;

        [MenuItem(ProductInfo.MenuRoot + "/AI Console %#j", priority = 5)]
        public static void ShowWindow()
        {
            AgentConsoleWindow window = GetWindow<AgentConsoleWindow>("AI Console");
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            nextPollTime = 0;
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
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            Label title = new Label("AI Console");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            title.style.color = new Color(0.486f, 0.361f, 0.988f); // Novahn indigo #7C5CFC
            title.style.flexGrow = 1;
            header.Add(title);

            filterField = new PopupField<string>(new List<string> { AllAgents }, 0);
            filterField.style.minWidth = 110;
            filterField.style.marginRight = 6;
            filterField.RegisterValueChangedCallback(change =>
            {
                agentFilter = change.newValue;
                RebuildLog();
            });
            header.Add(filterField);

            pauseButton = new Button(TogglePause) { text = T("Pause") };
            header.Add(pauseButton);
            header.Add(new Button(ClearView) { text = T("Clear") });

            Toggle autoScrollToggle = new Toggle(T("Auto-scroll")) { value = autoScroll };
            autoScrollToggle.style.marginLeft = 6;
            autoScrollToggle.RegisterValueChangedCallback(change => autoScroll = change.newValue);
            header.Add(autoScrollToggle);
            root.Add(header);

            statusLabel = new Label(T("Connecting to Hub..."));
            statusLabel.style.marginTop = 4;
            statusLabel.style.marginBottom = 6;
            statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            root.Add(statusLabel);

            logScroll = new ScrollView(ScrollViewMode.Vertical);
            logScroll.style.flexGrow = 1;
            logScroll.style.borderTopWidth = 1;
            logScroll.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.45f);
            logScroll.style.paddingTop = 4;
            root.Add(logScroll);

            renderedLanguage = McpUiLocalization.CurrentLanguage;
            RebuildLog();
            nextPollTime = 0;
        }

        private void TogglePause()
        {
            paused = !paused;
            pauseButton.text = paused ? T("Resume") : T("Pause");
            if (!paused)
                nextPollTime = 0;
        }

        private void ClearView()
        {
            events.Clear();
            RebuildLog();
        }

        private void Tick()
        {
            if (statusLabel == null)
                return;

            if (renderedLanguage != McpUiLocalization.CurrentLanguage)
            {
                McpUiLocalization.Apply(rootVisualElement);
                pauseButton.text = paused ? T("Resume") : T("Pause");
                renderedLanguage = McpUiLocalization.CurrentLanguage;
            }

            if (pendingRequest != null)
            {
                if (!pendingRequest.isDone)
                    return;
                try
                {
                    if (pendingRequest.responseCode == 200 && string.IsNullOrEmpty(pendingRequest.error))
                        Ingest(JObject.Parse(pendingRequest.downloadHandler.text));
                    else
                        statusLabel.text = T("Hub unavailable: ") + pendingRequest.error;
                }
                catch (Exception exception)
                {
                    statusLabel.text = T("Invalid Hub data: ") + exception.Message;
                }
                finally
                {
                    pendingRequest.Dispose();
                    pendingRequest = null;
                }
            }

            if (paused || EditorApplication.timeSinceStartup < nextPollTime)
                return;

            string url = HttpEndpointUtility.GetLocalBaseUrl() +
                         "/hub/transcript?after=" + lastCursor + "&limit=" + FetchLimit;
            pendingRequest = UnityWebRequest.Get(url);
            pendingRequest.timeout = 3;
            pendingRequest.SendWebRequest();
            nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;
        }

        private void Ingest(JObject snapshot)
        {
            JArray agents = snapshot["agents"] as JArray;
            if (agents != null)
            {
                bool changed = false;
                foreach (JToken agent in agents)
                {
                    string id = (string)agent;
                    if (!string.IsNullOrEmpty(id) && !knownAgents.Contains(id))
                    {
                        knownAgents.Add(id);
                        changed = true;
                    }
                }
                if (changed)
                    RefreshFilterChoices();
            }

            JArray incoming = snapshot["events"] as JArray;
            int appended = 0;
            if (incoming != null)
            {
                foreach (JToken token in incoming)
                {
                    if (token is JObject row)
                    {
                        TrackAgent((string)row["actor"]);
                        TrackAgent((string)row["assignee"]);
                        TrackAgent((string)row["recipient"]);
                        events.Add(row);
                        if (Matches(row))
                            logScroll.Add(BuildRow(row));
                        appended++;
                    }
                }
            }

            lastCursor = AgentConsoleModel.AdvanceCursor(lastCursor, snapshot);

            if (events.Count > MaxRows)
            {
                events.RemoveRange(0, events.Count - MaxRows);
                RebuildLog();
            }
            else if (appended > 0 && autoScroll)
            {
                ScrollToBottom();
            }

            statusLabel.text = T("Hub connected") + " • " + events.Count + " " +
                               T("events") + " • " + DateTime.Now.ToString("HH:mm:ss") +
                               (paused ? "  (" + T("paused") + ")" : string.Empty);
        }

        private void TrackAgent(string id)
        {
            if (!string.IsNullOrEmpty(id) && !knownAgents.Contains(id))
            {
                knownAgents.Add(id);
                RefreshFilterChoices();
            }
        }

        private void RefreshFilterChoices()
        {
            List<string> choices = new() { AllAgents };
            choices.AddRange(knownAgents);
            filterField.choices = choices;
            if (!choices.Contains(agentFilter))
            {
                agentFilter = AllAgents;
                filterField.SetValueWithoutNotify(AllAgents);
            }
        }

        private bool Matches(JObject row)
        {
            return AgentConsoleModel.Matches(row, agentFilter);
        }

        private void RebuildLog()
        {
            if (logScroll == null)
                return;
            logScroll.Clear();
            if (events.Count == 0)
            {
                Label empty = new Label(T("No conversation yet. Agents talk here as they hand off tasks."));
                empty.style.color = new Color(0.55f, 0.55f, 0.55f);
                empty.style.whiteSpace = WhiteSpace.Normal;
                logScroll.Add(empty);
                return;
            }
            foreach (JObject row in events)
                if (Matches(row))
                    logScroll.Add(BuildRow(row));
            if (autoScroll)
                ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            logScroll.schedule.Execute(() =>
                logScroll.scrollOffset = new Vector2(0, logScroll.contentContainer.layout.height));
        }

        private static VisualElement BuildRow(JObject row)
        {
            VisualElement line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.marginBottom = 2;
            line.style.flexWrap = Wrap.Wrap;

            Label time = new Label(FormatTime(row["at"]));
            time.style.color = new Color(0.5f, 0.5f, 0.5f);
            time.style.marginRight = 8;
            time.style.minWidth = 64;
            line.Add(time);

            string actor = (string)row["actor"] ?? "?";
            Label actorLabel = new Label(actor);
            actorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            actorLabel.style.color = AgentConsoleModel.AgentColor(actor);
            actorLabel.style.marginRight = 6;
            line.Add(actorLabel);

            Label verb = new Label(VerbFor(row));
            verb.style.color = new Color(0.7f, 0.7f, 0.7f);
            verb.style.marginRight = 6;
            line.Add(verb);

            string text = (string)row["text"];
            if (!string.IsNullOrEmpty(text))
            {
                Label body = new Label(text);
                body.style.whiteSpace = WhiteSpace.Normal;
                body.style.flexShrink = 1;
                line.Add(body);
            }
            return line;
        }

        private static string VerbFor(JObject row)
        {
            string kind = (string)row["kind"] ?? string.Empty;
            switch (kind)
            {
                case "task.created": return "──task──▶ " + ((string)row["assignee"] ?? "?");
                case "task.claimed": return T("claimed");
                case "task.completed": return "✓ " + T("done");
                case "task.cancelled": return "✕ " + T("cancelled");
                case "message.sent": return "──msg──▶ " + ((string)row["recipient"] ?? "?");
                case "message.acknowledged": return T("read");
                default: return kind;
            }
        }

        private static string FormatTime(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return string.Empty;
            DateTimeOffset at = DateTimeOffset.FromUnixTimeMilliseconds((long)((double)value * 1000));
            return at.ToLocalTime().ToString("HH:mm:ss");
        }

        private static string T(string value)
        {
            return McpUiLocalization.Translate(value);
        }
    }
}

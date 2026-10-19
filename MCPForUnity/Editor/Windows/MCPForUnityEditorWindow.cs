using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Hub;
using MCPForUnity.Editor.Windows.Components.Advanced;
using MCPForUnity.Editor.Windows.Components.AssetGen;
using MCPForUnity.Editor.Windows.Components.ClientConfig;
using MCPForUnity.Editor.Windows.Components.Connection;
using MCPForUnity.Editor.Windows.Components.Resources;
using MCPForUnity.Editor.Windows.Components.Tools;
using MCPForUnity.Editor.Windows.Components.Validation;
using MCPForUnity.Editor.Windows.Studio;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows
{
    public enum StudioPage { Overview, Team, Tasks, Skills, Connection, Tools, Resources, AssetGen, Settings }

    public partial class MCPForUnityEditorWindow : EditorWindow
    {
        private static readonly HashSet<MCPForUnityEditorWindow> OpenWindows = new HashSet<MCPForUnityEditorWindow>();
        private readonly Dictionary<StudioPage, VisualElement> pages = new Dictionary<StudioPage, VisualElement>();
        private readonly Dictionary<StudioPage, Button> navigation = new Dictionary<StudioPage, Button>();
        private readonly Dictionary<StudioPage, Action> refreshers = new Dictionary<StudioPage, Action>();
        [SerializeField] private StudioPage currentPage;
        private VisualElement content;
        private Label pageTitle, pageSubtitle, connectionBadge;
        private McpConnectionSection connectionSection;
        private McpClientConfigSection clientConfigSection;
        private McpAdvancedSection advancedSection;
        private bool created;
        private double lastUpdate;
        private StudioTasksView taskView;

        public static void ShowWindow() => ShowPage(StudioPage.Overview);
        public static void ShowPage(StudioPage page)
        {
            var window = GetWindow<MCPForUnityEditorWindow>();
            window.titleContent = new GUIContent(ProductInfo.ProductName);
            window.minSize = new Vector2(900, 600);
            window.Show();
            if (window.created) window.Navigate(page);
            else window.currentPage = page;
            window.Focus();
        }
        public static bool HasAnyOpenWindow() => OpenWindows.Any(w => w != null);
        public static void CloseAllOpenWindows() => CloseAllWindows();
        internal static void CloseAllWindows()
        { foreach (var window in OpenWindows.Where(w => w != null).ToArray()) window.Close(); }

        public void CreateGUI()
        {
            pages.Clear(); navigation.Clear(); refreshers.Clear();
            // Keep Unity's editor theme: it supplies the default font and control metrics.
            rootVisualElement.Clear();
            StudioUi.ApplyFont(rootVisualElement);
            string path = AssetPathUtility.GetMcpPackageRootPath() + "/Editor/Windows";
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path + "/MCPForUnityEditorWindow.uxml");
            if (tree == null) { rootVisualElement.Add(new HelpBox(StudioText.T("The Novahn Studio layout is missing."), HelpBoxMessageType.Error)); return; }
            tree.CloneTree(rootVisualElement);
            // At this point the tree contains authored shell text only, before user data is bound.
            rootVisualElement.Query<Label>().ForEach(label => label.text = StudioText.T(label.text));
            rootVisualElement.Q<Label>("preview-label").text = "NOVAHN STUDIO";
            rootVisualElement.Q<Label>("preview-caption").text = StudioText.T("A workspace for your AI team.");
            rootVisualElement.Q<Button>("console-button").text = StudioText.T("AI Log ↗");
            var language = rootVisualElement.Q<DropdownField>("studio-language");
            language.choices = new List<string> { "EN", "VI" };
            language.SetValueWithoutNotify(McpUiLocalization.CurrentLanguage == "vi" ? "VI" : "EN");
            language.RegisterValueChangedCallback(e => McpUiLocalization.SetLanguage(e.newValue == "VI" ? "vi" : "en"));
            var common = AssetDatabase.LoadAssetAtPath<StyleSheet>(path + "/Components/Common.uss");
            if (common != null && !rootVisualElement.styleSheets.Contains(common)) rootVisualElement.styleSheets.Add(common);
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(path + "/MCPForUnityEditorWindow.uss");
            if (styles != null && !rootVisualElement.styleSheets.Contains(styles)) rootVisualElement.styleSheets.Add(styles);
            content = rootVisualElement.Q("studio-content");
            pageTitle = rootVisualElement.Q<Label>("page-title");
            pageSubtitle = rootVisualElement.Q<Label>("page-subtitle");
            connectionBadge = rootVisualElement.Q<Label>("connection-badge");
            rootVisualElement.Q<Label>("project-name").text = new System.IO.DirectoryInfo(StudioWorkspace.ProjectRoot).Name;
            rootVisualElement.Q<Label>("version-label").text = "NOVAHN STUDIO  /  " + AssetPathUtility.GetPackageVersion();
            rootVisualElement.Q<Button>("console-button").clicked += AgentConsoleWindow.ShowWindow;
            var nav = rootVisualElement.Q("workspace-nav");
            AddNavigation(nav, StudioPage.Overview, "01", StudioText.T("Overview"));
            AddNavigation(nav, StudioPage.Team, "02", StudioText.T("AI Team"));
            AddNavigation(nav, StudioPage.Tasks, "03", StudioText.T("Tasks"));
            AddNavigation(nav, StudioPage.Skills, "04", StudioText.T("Skills"));
            var engineNav = rootVisualElement.Q("engine-nav");
            AddNavigation(engineNav, StudioPage.Connection, "05", StudioText.T("Connection"));
            AddNavigation(engineNav, StudioPage.Tools, "06", StudioText.T("Tools"));
            AddNavigation(engineNav, StudioPage.Resources, "07", StudioText.T("Resources"));
            AddNavigation(engineNav, StudioPage.AssetGen, "08", StudioText.T("Asset generation"));
            AddNavigation(engineNav, StudioPage.Settings, "09", StudioText.T("Settings"));
            created = true;
            Navigate(currentPage);
        }

        private void AddNavigation(VisualElement parent, StudioPage page, string number, string title)
        {
            var button = new Button(() => Navigate(page)) { name = "nav-" + page.ToString().ToLowerInvariant() };
            button.AddToClassList("studio-nav-button");
            button.Add(new Label(number) { pickingMode = PickingMode.Ignore, name = "nav-number" });
            button.Add(new Label(title) { pickingMode = PickingMode.Ignore });
            navigation[page] = button; parent.Add(button);
        }

        public void Navigate(StudioPage page)
        {
            if (!created) { currentPage = page; return; }
            currentPage = page;
            foreach (var item in navigation) item.Value.EnableInClassList("selected", item.Key == page);
            var title = Titles(page); pageTitle.text = title.Item1; pageSubtitle.text = title.Item2;
            content.Clear();
            if (!pages.TryGetValue(page, out var panel))
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "page-" + page.ToString().ToLowerInvariant() };
                scroll.AddToClassList("studio-scroll");
                scroll.contentContainer.AddToClassList("studio-page");
                BuildPage(page, scroll.contentContainer);
                panel = scroll; pages[page] = panel;
            }
            content.Add(panel);
            RefreshState();
        }

        private void BuildPage(StudioPage page, VisualElement body)
        {
            switch (page)
            {
                case StudioPage.Overview:
                    var dashboard = new StudioDashboardView(body, Navigate); refreshers[page] = dashboard.Refresh; break;
                case StudioPage.Team:
                    var team = new StudioTeamView(body); refreshers[page] = team.Refresh; break;
                case StudioPage.Tasks:
                    taskView = new StudioTasksView(body); refreshers[page] = taskView.Refresh; break;
                case StudioPage.Skills:
                    var skills = new StudioSkillsView(body); refreshers[page] = skills.Refresh; break;
                case StudioPage.Connection: BuildConnection(body); break;
                case StudioPage.Tools:
                    var toolsRoot = LoadSection(body, "Tools/McpToolsSection");
                    if (toolsRoot != null) { var tools = new McpToolsSection(toolsRoot); tools.Refresh(); }
                    break;
                case StudioPage.Resources:
                    var resourcesRoot = LoadSection(body, "Resources/McpResourcesSection");
                    if (resourcesRoot != null) { var resources = new McpResourcesSection(resourcesRoot); resources.Refresh(); }
                    break;
                case StudioPage.AssetGen:
                    var genRoot = LoadSection(body, "AssetGen/McpAssetGenSection");
                    if (genRoot != null) { var gen = new McpAssetGenSection(genRoot); gen.Refresh(); }
                    break;
                case StudioPage.Settings: BuildSettings(body); break;
            }
        }

        private VisualElement LoadSection(VisualElement parent, string relative)
        {
            string path = AssetPathUtility.GetMcpPackageRootPath() + "/Editor/Windows/Components/" + relative + ".uxml";
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (asset == null) { parent.Add(new HelpBox(StudioText.T("Cannot load ") + relative, HelpBoxMessageType.Error)); return null; }
            var element = asset.Instantiate(); parent.Add(element);
            return element;
        }

        private void BuildConnection(VisualElement body)
        {
            body.Add(StudioUi.Note(StudioText.T("Unity MCP connects to the Editor. The AI Team Hub coordinates tasks between AI clients.")));
            var connection = LoadSection(body, "Connection/McpConnectionSection");
            var clients = LoadSection(body, "ClientConfig/McpClientConfigSection");
            if (connection != null) connectionSection = new McpConnectionSection(connection);
            if (clients != null) clientConfigSection = new McpClientConfigSection(clients);
            if (connectionSection != null && clientConfigSection != null)
            {
                connectionSection.OnManualConfigUpdateRequested += () => clientConfigSection.UpdateManualConfiguration();
                connectionSection.OnTransportChanged += () => clientConfigSection.RefreshSelectedClient(forceImmediate: true);
                clientConfigSection.OnClientTransportDetected += connectionSection.UpdateTransportMismatchWarning;
                clientConfigSection.OnClientConfigMismatch += connectionSection.UpdateVersionMismatchWarning;
                connectionSection.SetHealthStatusUpdateCallback((healthy, status) => advancedSection?.UpdateHealthStatus(healthy, status));
            }
            McpUiLocalization.Apply(body);
        }

        private void BuildSettings(VisualElement body)
        {
            body.Add(StudioUi.Note(StudioText.T("Configure your workspace runtime, diagnostics and optional Unity packages.")));
            var advanced = LoadSection(body, "Advanced/McpAdvancedSection");
            if (advanced != null)
            {
                advancedSection = new McpAdvancedSection(advanced);
                advancedSection.OnGitUrlChanged += () => clientConfigSection?.UpdateManualConfiguration();
                advancedSection.OnHttpServerCommandUpdateRequested += () => connectionSection?.UpdateHttpServerCommandDisplay();
                advancedSection.OnTestConnectionRequested += async () => { if (connectionSection != null) await connectionSection.VerifyBridgeConnectionAsync(); };
                foreach (string id in new[] { "deploy-source-path", "browse-deploy-source-button", "clear-deploy-source-button", "deploy-target-label",
                    "deploy-backup-label", "deploy-button", "deploy-restore-button", "deploy-status-label" })
                { var element = advanced.Q(id); if (element != null) element.style.display = DisplayStyle.None; }
                foreach (var label in advanced.Query<Label>().ToList())
                    if (label.text == "Package Source:") label.parent.style.display = DisplayStyle.None;
            }
            var optional = new Foldout { text = StudioText.T("Optional Unity packages"), value = false }; body.Add(optional);
            BuildDependenciesSection(optional);
            var validation = LoadSection(body, "Validation/McpValidationSection");
            if (validation != null) new McpValidationSection(validation);
            McpUiLocalization.Apply(body);
        }

        private static Tuple<string, string> Titles(StudioPage page)
        {
            switch (page)
            {
                case StudioPage.Team: return Tuple.Create(StudioText.T("AI Team"), StudioText.T("One primary AI coordinates. Worker AIs focus on their assigned tasks."));
                case StudioPage.Tasks: return Tuple.Create(StudioText.T("Tasks"), StudioText.T("Give goals in desktop chat, delegate tasks and track results in this workspace."));
                case StudioPage.Skills: return Tuple.Create(StudioText.T("Skills"), StudioText.T("Install Novahn skills and add custom skills for each AI."));
                case StudioPage.Connection: return Tuple.Create(StudioText.T("Unity connection"), StudioText.T("Set up the MCP server and connect AI clients to the Editor."));
                case StudioPage.Tools: return Tuple.Create(StudioText.T("Unity tools"), StudioText.T("Choose the operations AI can use in this project."));
                case StudioPage.Resources: return Tuple.Create(StudioText.T("MCP resources"), StudioText.T("Choose the project data AI can read."));
                case StudioPage.AssetGen: return Tuple.Create(StudioText.T("Asset generation"), StudioText.T("Configure image, model and audio generation services."));
                case StudioPage.Settings: return Tuple.Create(StudioText.T("Settings"), StudioText.T("Runtime environment, diagnostics and optional Unity packages."));
                default: return Tuple.Create(StudioText.T("Workspace"), StudioText.T("Connect your AI team to Unity, from goal to result."));
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(ProductInfo.ProductName); minSize = new Vector2(900, 600);
            OpenWindows.Add(this); StudioWorkspace.Changed += RefreshState; EditorApplication.update += UpdateEditor;
            McpUiLocalization.LanguageChanged += RefreshLanguage;
        }
        private void OnDisable()
        {
            StudioWorkspace.Changed -= RefreshState; EditorApplication.update -= UpdateEditor;
            McpUiLocalization.LanguageChanged -= RefreshLanguage;
            OpenWindows.Remove(this); created = false; pages.Clear(); refreshers.Clear();
        }
        private void OnFocus() { if (created) RefreshState(); }
        private void RefreshLanguage()
        {
            if (!created) return;
            var selected = currentPage;
            string editing = taskView?.EditingId;
            var inputs = pages.Where(p => p.Key == StudioPage.Team || p.Key == StudioPage.Tasks || p.Key == StudioPage.Skills)
                .ToDictionary(p => p.Key, p => StudioInputSnapshot.Capture(p.Value));
            CreateGUI();
            foreach (var pair in inputs)
            {
                Navigate(pair.Key);
                pair.Value.Restore(pages[pair.Key]);
                if (pair.Key == StudioPage.Tasks) { taskView.EditingId = editing; taskView.Refresh(); }
            }
            Navigate(selected);
        }
        private void RefreshState()
        {
            if (!created) return;
            connectionBadge.text = StudioWorkspace.Online ? "●  HUB ONLINE" : "○  HUB OFFLINE";
            connectionBadge.EnableInClassList("online", StudioWorkspace.Online);
            if (refreshers.TryGetValue(currentPage, out var refresh)) refresh();
        }
        private void UpdateEditor()
        {
            if (!created || EditorApplication.timeSinceStartup - lastUpdate < 2) return;
            lastUpdate = EditorApplication.timeSinceStartup;
            if (currentPage == StudioPage.Connection) connectionSection?.UpdateConnectionStatus();
        }
        internal static void RequestHealthVerification()
        {
            foreach (var window in OpenWindows.ToArray())
                if (window != null && window.connectionSection != null) _ = window.connectionSection.VerifyBridgeConnectionAsync();
        }
    }
}

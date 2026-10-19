using MCPForUnity.Editor.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Services.Hub;
using UnityEditor;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Studio
{
    internal sealed class StudioSkillsView
    {
        private readonly VisualElement targets;
        private readonly Label feedback;
        private readonly Dictionary<string, Label> counts = new Dictionary<string, Label>();
        private string signature;
        public StudioSkillsView(VisualElement root)
        {
            root.Add(StudioUi.Note(StudioText.T("Each AI gets Unity skills and instructions for its role. Skills are installed in the project by default; choose another directory supported by your client if needed.")));
            var actions = StudioUi.Box("studio-row");
            actions.Add(StudioUi.Button(StudioText.T("Install Novahn skills for the team"), () => StudioUi.Safe(() => {
                if (StudioWorkspace.Team.agents.Count == 0) throw new InvalidOperationException(StudioText.T("Add an AI to the team before installing skills."));
                foreach (var agent in StudioWorkspace.Team.agents) StudioSkillCatalog.InstallBundle(agent);
                feedback.text = StudioText.T("Installed skills for ") + StudioWorkspace.Team.agents.Count + StudioText.T(" AI. Start a new session or reload skills in the client."); Refresh();
            }, feedback), true));
            actions.Add(StudioUi.Button(StudioText.T("Open bundled library"), () => EditorUtility.RevealInFinder(StudioSkillCatalog.SourceRoot))); root.Add(actions);
            feedback = StudioUi.Text("", "studio-feedback"); root.Add(feedback);
            root.Add(StudioUi.Text(StudioText.T("Install per AI"), "studio-section-heading"));
            targets = new VisualElement(); root.Add(targets);
            root.Add(StudioUi.Text(StudioText.T("Novahn skill library"), "studio-section-heading"));
            foreach (var skill in new[] {
                new[] { "novahn-unity", StudioText.T("Use Unity safely, target the correct Editor and inspect the Console.") },
                new[] { "novahn-unity-scene", StudioText.T("Scenes, GameObjects, components and prefabs.") },
                new[] { "novahn-unity-gameplay", StudioText.T("Develop gameplay within the project structure.") },
                new[] { "novahn-coordinator", StudioText.T("Primary AI: receive goals, delegate and combine results.") },
                new[] { "novahn-worker", StudioText.T("Worker AI: receive tasks, report progress and return results.") },
                new[] { "novahn-desktop", StudioText.T("Desktop chat → Unity") } })
            {
                var item = StudioUi.Box("studio-library-item"); item.Add(StudioUi.Text(skill[0], "studio-card-title")); item.Add(StudioUi.Text(skill[1])); root.Add(item);
            }
            Refresh();
        }
        public void Refresh()
        {
            string current = string.Join("|", StudioWorkspace.Team.agents.Select(a => a.id + a.isLeader + a.displayName));
            if (signature != current)
            {
                signature = current; targets.Clear(); counts.Clear();
                if (StudioWorkspace.Team.agents.Count == 0) targets.Add(StudioUi.Text(StudioText.T("No agents yet. Open AI Team to add a member, or connect from desktop chat."), "studio-empty"));
                foreach (var agent in StudioWorkspace.Team.agents) targets.Add(Target(agent));
            }
            foreach (var agent in StudioWorkspace.Team.agents)
                if (counts.TryGetValue(agent.id, out var count)) count.text = StudioSkillCatalog.InstalledCount(agent) + "/" + StudioSkillCatalog.ForAgent(agent).Count() + StudioText.T(" skills present in the target directory");
        }
        private VisualElement Target(HubAgentEntry agent)
        {
            var card = StudioUi.Card(agent.displayName + (agent.isLeader ? StudioText.T("  /  Primary AI") : StudioText.T("  /  Worker AI")));
            var path = new TextField(StudioText.T("Skills directory")) { value = StudioSkillCatalog.InstallPath(agent), isDelayed = true };
            path.RegisterValueChangedCallback(e => { agent.skillInstallPath = e.newValue; StudioWorkspace.Save(); Refresh(); }); card.Add(path);
            var count = StudioUi.Text("", "studio-skill-path"); counts[agent.id] = count; card.Add(count);
            var buttons = StudioUi.Box("studio-agent-actions");
            buttons.Add(StudioUi.Button(StudioText.T("Install / update role skills"), () => StudioUi.Safe(() => {
                StudioSkillCatalog.InstallBundle(agent); feedback.text = StudioText.T("Installed the skill bundle for ") + agent.displayName + "."; Refresh();
            }, feedback), true));
            buttons.Add(StudioUi.Button(StudioText.T("+ Import custom skill"), () => StudioUi.Safe(() => {
                string source = EditorUtility.OpenFolderPanel(StudioText.T("Choose a folder containing SKILL.md"), StudioWorkspace.ProjectRoot, "");
                if (string.IsNullOrEmpty(source)) return;
                string installed = StudioSkillCatalog.InstallFolder(source, StudioSkillCatalog.InstallPath(agent));
                feedback.text = StudioText.T("Added ") + installed + StudioText.T(" for ") + agent.displayName + "."; Refresh();
            }, feedback)));
            buttons.Add(StudioUi.Button(StudioText.T("Choose target directory"), () => {
                string chosen = EditorUtility.OpenFolderPanel(StudioText.T("AI skills directory"), StudioWorkspace.ProjectRoot, "");
                if (string.IsNullOrEmpty(chosen)) return;
                path.value = chosen;
            }));
            buttons.Add(StudioUi.Button(StudioText.T("Open directory"), () => {
                string directory = StudioSkillCatalog.InstallPath(agent);
                if (Directory.Exists(directory)) EditorUtility.RevealInFinder(directory); else feedback.text = StudioText.T("The directory does not exist yet. Install skills to create it.");
            }));
            card.Add(buttons); return card;
        }
    }
}

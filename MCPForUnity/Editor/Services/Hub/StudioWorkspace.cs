using MCPForUnity.Editor.Constants;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services.Hub
{
    [Serializable]
    public sealed class StudioTask
    {
        public string id = Guid.NewGuid().ToString("N");
        public string title;
        public string request;
        public string assignee;
        public string creator;
        public string parentId;
        public string scheduledUtc;
        public string remoteId;
        public string state = "draft";
        public string error;
        public string result;
        public string createdUtc = DateTime.UtcNow.ToString("o");
    }

    [Serializable]
    public sealed class StudioWorkspaceData
    {
        public int dashboardPort = 4320;
        public List<StudioTask> tasks = new List<StudioTask>();
    }

    /// <summary>Project-local team, task drafts and schedule. Network state is always observed.</summary>
    [InitializeOnLoad]
    public static class StudioWorkspace
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string SettingsRoot => Path.Combine(ProjectRoot, "UserSettings", "NovahnStudio");
        public static HubAgentRegistry Team { get; private set; }
        public static StudioWorkspaceData Data { get; private set; }
        public static JObject Snapshot { get; private set; }
        public static bool Online { get; private set; }
        public static bool Busy { get; private set; }
        private static string status = "Hub not running";
        public static string Status { get => StudioText.T(status); private set => status = value; }
        public static string DashboardUrl => "http://127.0.0.1:" + Data.dashboardPort;
        public static event Action Changed;
        private static double nextPoll;
        private static bool polling;

        static StudioWorkspace()
        {
            Team = HubAgentStore.Load();
            try
            {
                string file = Path.Combine(SettingsRoot, "workspace.json");
                Data = File.Exists(file) ? JsonUtility.FromJson<StudioWorkspaceData>(File.ReadAllText(file)) : null;
            }
            catch (Exception e) { Debug.LogWarning(StudioText.T("[Novahn] Cannot read the task schedule: ") + e.Message); }
            Data = Data ?? new StudioWorkspaceData();
            Data.tasks = Data.tasks ?? new List<StudioTask>();
            // A domain reload can interrupt an HTTP response after the server stored the task.
            // Keep the same idempotency key when the user retries it.
            foreach (var task in Data.tasks.Where(t => t.state == "sending"))
            { task.state = "failed"; task.error = StudioText.T("Unity reloaded. Retry to resume delivery using the same task identity."); }
            EditorApplication.update += Update;
        }

        public static void Save()
        {
            HubAgentStore.Save(Team);
            Directory.CreateDirectory(SettingsRoot);
            string path = Path.Combine(SettingsRoot, "workspace.json");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(Data, true));
            File.Copy(temporary, path, true);
            File.Delete(temporary);
            Changed?.Invoke();
        }

        public static void Notify(string status)
        { Status = status; Changed?.Invoke(); }

        public static StudioTask CreateTask(string title, string request, string assignee,
            string parentId = null, DateTime? scheduledLocal = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(request))
                throw new InvalidOperationException(StudioText.T("Enter a task title and request."));
            if (!Team.agents.Any(a => a.id == assignee))
                throw new InvalidOperationException(StudioText.T("Choose an assignee from the team."));
            var task = new StudioTask { title = title.Trim(), request = request.Trim(), assignee = assignee,
                creator = Team.Leader?.id, parentId = parentId,
                scheduledUtc = scheduledLocal?.ToUniversalTime().ToString("o"),
                state = scheduledLocal.HasValue ? "scheduled" : "draft" };
            Data.tasks.Add(task);
            Save();
            return task;
        }

        public static async Task DispatchAsync(StudioTask task)
        {
            if (Busy) throw new InvalidOperationException(StudioText.T("Another task is being sent. Try again shortly."));
            if (!Online) throw new InvalidOperationException(StudioText.T("Start the Hub in AI Team before sending work."));
            var leader = Team.agents.FirstOrDefault(a => a.id == task.creator) ?? Team.Leader;
            if (leader == null || !leader.isLeader)
                throw new InvalidOperationException(StudioText.T("The primary AI changed. Create a new draft with the current primary AI."));
            if (!Team.agents.Any(a => a.id == task.assignee))
                throw new InvalidOperationException(StudioText.T("The assignee is no longer in the team."));
            Busy = true; task.state = "sending"; task.creator = leader.id; task.error = null; Save();
            try
            {
                JObject result = await StudioHubClient.CallAsync(leader, "hub.task.create", new JObject {
                    ["assignee"] = task.assignee, ["request"] = BuildRequest(task), ["idempotency_key"] = "studio-" + task.id });
                task.remoteId = (string)result["data"]?["task_id"];
                if (string.IsNullOrEmpty(task.remoteId)) throw new InvalidOperationException(StudioText.T("The Hub did not return a task ID."));
                task.state = (string)result["data"]?["state"] ?? "queued";
                task.error = null;
            }
            catch (Exception e) { task.state = "failed"; task.error = e.Message; }
            finally { Busy = false; Save(); nextPoll = 0; }
        }

        public static string BuildRequest(StudioTask task)
        {
            string scope = "Project Unity: " + ProjectRoot + StudioText.T("\nOnly use instance: ") + new DirectoryInfo(ProjectRoot).Name +
                StudioText.T("\nFollow the project AGENTS.md.\n");
            return "[Novahn Studio | " + task.title + "]\n" + scope +
                (string.IsNullOrEmpty(task.parentId) ? "" : StudioText.T("Parent goal: ") + task.parentId + "\n") + task.request;
        }

        public static async Task CancelAsync(StudioTask task)
        {
            if (!string.IsNullOrEmpty(task.remoteId))
            {
                var creator = Team.agents.FirstOrDefault(a => a.id == task.creator);
                if (creator == null) throw new InvalidOperationException(StudioText.T("The task creator is no longer in the team."));
                await StudioHubClient.CallAsync(creator, "hub.task.cancel", new JObject { ["task_id"] = task.remoteId });
            }
            task.state = "cancelled"; task.error = null; Save();
        }

        public static async Task ReadResultAsync(StudioTask task)
        {
            var actor = Team.agents.FirstOrDefault(a => a.id == task.creator || a.id == task.assignee);
            if (actor == null) throw new InvalidOperationException(StudioText.T("The task's AI is no longer in the team."));
            var result = await StudioHubClient.CallAsync(actor, "hub.task.get", new JObject { ["task_id"] = task.remoteId });
            task.result = (string)result["data"]?["result"];
            task.state = (string)result["data"]?["state"] ?? task.state;
            Save();
        }

        public static async Task RefreshAsync()
        {
            if (polling) return;
            polling = true;
            try
            {
                var snapshot = await StudioHubClient.GetAsync(DashboardUrl + "/hub/status");
                if ((string)snapshot["product"] != "novahn-studio" ||
                    (string)snapshot["instanceId"] != StudioHubRuntime.InstanceId)
                    throw new InvalidOperationException(StudioText.T("Another project is using this Hub port."));
                Snapshot = snapshot; Online = true; Status = StudioText.T("Hub running");
                var remote = snapshot["tasks"] as JArray;
                if (remote != null)
                    foreach (var task in Data.tasks.Where(t => !string.IsNullOrEmpty(t.remoteId)))
                    {
                        var row = remote.FirstOrDefault(t => (string)t["task_id"] == task.remoteId);
                        if (row != null) task.state = (string)row["state"];
                    }
            }
            catch (Exception e)
            {
                Online = false; Snapshot = null;
                Status = StudioHubRuntime.IsRunning ? StudioText.T("Hub starting…") : StudioText.T("Hub not running");
                if (e.Message.StartsWith(StudioText.T("Another project"))) Status = e.Message;
            }
            finally { polling = false; Changed?.Invoke(); }
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup >= nextPoll)
            {
                nextPoll = EditorApplication.timeSinceStartup + (StudioHubRuntime.IsRunning || Online ? 5 : 15);
                if (StudioHubRuntime.IsRunning || Online) _ = RefreshAsync();
            }
            if (!Online || Busy) return;
            var due = Data.tasks.FirstOrDefault(t => t.state == "scheduled" &&
                DateTime.TryParse(t.scheduledUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)
                && date.ToUniversalTime() <= DateTime.UtcNow);
            if (due != null) _ = DispatchScheduledAsync(due);
        }

        private static async Task DispatchScheduledAsync(StudioTask task)
        {
            try { await DispatchAsync(task); }
            catch (Exception e) { task.state = "failed"; task.error = e.Message; Save(); }
        }
    }
}

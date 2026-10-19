using MCPForUnity.Editor.Constants;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services.Hub;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Studio
{
    internal sealed class StudioTasksView
    {
        private readonly DropdownField kind, assignee, parent;
        private readonly TextField title, brief, scheduledAt, search;
        private readonly Toggle schedule;
        private readonly Label feedback, hubStatus;
        private readonly VisualElement board, live;
        private readonly Button send;
        private string editingId;
        internal string EditingId { get => editingId; set => editingId = value; }

        public StudioTasksView(VisualElement root)
        {
            root.Add(StudioUi.Note(StudioText.T("Give goals to your primary AI in desktop chat, or use this optional form. Scheduled tasks use the computer clock and are sent while Unity and the Hub are running.")));
            var form = StudioUi.Card(StudioText.T("Assign work to your team"));
            kind = StudioUi.Field(new DropdownField(StudioText.T("Work type"), new System.Collections.Generic.List<string> { StudioText.T("Goal for primary AI"), StudioText.T("Delegated task") }, 0));
            title = StudioUi.Field(new TextField(StudioText.T("Task title")) { name = "task-title" });
            brief = StudioUi.Field(new TextField(StudioText.T("Request & expected result")) { multiline = true, name = "task-request" }); brief.AddToClassList("studio-brief");
            assignee = StudioUi.Field(new DropdownField(StudioText.T("Assignee"))); parent = StudioUi.Field(new DropdownField(StudioText.T("Parent goal")));
            schedule = new Toggle(StudioText.T("Schedule delivery"));
            scheduledAt = StudioUi.Field(new TextField(StudioText.T("Date & time")) { value = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm") });
            scheduledAt.tooltip = StudioText.T("yyyy-MM-dd HH:mm • Current computer timezone: ") + TimeZoneInfo.Local.DisplayName;
            scheduledAt.style.display = DisplayStyle.None;
            schedule.RegisterValueChangedCallback(e => { scheduledAt.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None; RefreshSendLabel(); });
            kind.RegisterValueChangedCallback(e => UpdateTargetVisibility());
            form.Add(kind); form.Add(title); form.Add(brief); form.Add(assignee); form.Add(parent); form.Add(schedule); form.Add(scheduledAt);
            var actions = StudioUi.Box("studio-row");
            send = StudioUi.Button(StudioText.T("Send goal"), () => StudioUi.Run(() => Submit(true), feedback), true); actions.Add(send);
            actions.Add(StudioUi.Button(StudioText.T("Save draft"), () => StudioUi.Run(() => Submit(false), feedback)));
            actions.Add(StudioUi.Button(StudioText.T("New"), ClearForm)); form.Add(actions);
            feedback = StudioUi.Text("", "studio-feedback"); form.Add(feedback); root.Add(form);
            var toolbar = StudioUi.Box("studio-row");
            search = new TextField { name = "task-search", tooltip = StudioText.T("Filter by title or assignee") }; search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(e => RebuildBoard()); toolbar.Add(search);
            toolbar.Add(StudioUi.Button(StudioText.T("Refresh Hub"), () => StudioUi.Run(StudioWorkspace.RefreshAsync, feedback))); root.Add(toolbar);
            hubStatus = StudioUi.Text("", "studio-feedback"); root.Add(hubStatus);
            board = StudioUi.Box("studio-board"); root.Add(board);
            live = new VisualElement(); root.Add(live); Refresh();
        }

        private void UpdateTargetVisibility()
        {
            bool child = kind.index == 1;
            assignee.style.display = child ? DisplayStyle.Flex : DisplayStyle.None;
            parent.style.display = child ? DisplayStyle.Flex : DisplayStyle.None; RefreshSendLabel();
        }
        private void RefreshSendLabel()
        { if (send != null) send.text = schedule.value ? StudioText.T("Schedule delivery") : kind.index == 0 ? StudioText.T("Send goal to primary AI") : StudioText.T("Send task"); }
        private async Task Submit(bool dispatch)
        {
            var leader = StudioWorkspace.Team.Leader;
            if (leader == null) throw new InvalidOperationException(StudioText.T("Add a primary AI in AI Team before assigning work."));
            if (string.IsNullOrWhiteSpace(title.value) || string.IsNullOrWhiteSpace(brief.value)) throw new InvalidOperationException(StudioText.T("Enter a task title and request."));
            string target = kind.index == 0 ? leader.id : AgentId(assignee.value);
            string parentId = kind.index == 0 || parent.index <= 0 ? null : parent.value.Split('|')[0].Trim();
            DateTime? date = null;
            if (schedule.value && dispatch)
            {
                if (!DateTime.TryParseExact(scheduledAt.value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local) || local <= DateTime.Now)
                    throw new InvalidOperationException(StudioText.T("Enter a future time using yyyy-MM-dd HH:mm."));
                date = local;
            }
            if (dispatch && !date.HasValue && !StudioWorkspace.Online) throw new InvalidOperationException(StudioText.T("Start the Hub before sending, or save a draft for later."));
            string request = brief.value;
            if (kind.index == 0)
                request += StudioText.T("\n\nPrimary AI role: receive this goal, plan, delegate self-contained tasks to workers through hub.task.create, monitor and combine results. Report completion only when the goal is achieved.");
            StudioTask task = StudioWorkspace.Data.tasks.FirstOrDefault(t => t.id == editingId && t.state == "draft");
            if (task == null) task = StudioWorkspace.CreateTask(title.value, request, target, parentId, date);
            else
            {
                task.title = title.value.Trim(); task.request = request.Trim(); task.assignee = target; task.parentId = parentId;
                task.creator = leader.id; task.scheduledUtc = date?.ToUniversalTime().ToString("o"); task.state = date.HasValue ? "scheduled" : "draft";
                StudioWorkspace.Save();
            }
            ClearForm();
            if (dispatch && !date.HasValue) await StudioWorkspace.DispatchAsync(task);
            feedback.text = task.state == "failed" ? task.error : date.HasValue ? StudioText.T("Schedule saved. Keep Unity and the Hub running at the scheduled time.") : dispatch ? StudioText.T("The Hub saved the task. The AI will receive it when it checks its inbox.") : StudioText.T("Draft saved.");
            Refresh();
        }
        private static string AgentId(string choice) => choice?.Split('|')[0].Trim();
        private void ClearForm()
        { editingId = null; title.value = ""; brief.value = ""; schedule.value = false; feedback.text = ""; }

        public void Refresh()
        {
            scheduledAt.style.display = schedule.value ? DisplayStyle.Flex : DisplayStyle.None;
            string selected = assignee.value;
            assignee.choices = StudioWorkspace.Team.agents.Select(a => a.id + " | " + a.displayName).ToList();
            assignee.SetValueWithoutNotify(assignee.choices.Contains(selected) ? selected : assignee.choices.FirstOrDefault());
            string selectedParent = parent.value;
            parent.choices = new[] { StudioText.T("None") }.Concat(StudioWorkspace.Data.tasks.Select(t => t.id + " | " + t.title)).ToList();
            parent.SetValueWithoutNotify(parent.choices.Contains(selectedParent) ? selectedParent : parent.choices[0]);
            send.SetEnabled(!StudioWorkspace.Busy);
            hubStatus.text = StudioWorkspace.Online ? StudioText.T("Hub connected • the columns below show task states.") : StudioText.T("Hub disconnected • showing saved data; current progress is unconfirmed.");
            UpdateTargetVisibility(); RebuildBoard();
        }
        private void RebuildBoard()
        {
            board.Clear(); live.Clear();
            var draft = Column(StudioText.T("DRAFTS & SCHEDULE")); var working = Column(StudioText.T("IN PROGRESS")); var done = Column(StudioText.T("RESULTS"));
            string query = search?.value ?? "";
            var tasks = StudioWorkspace.Data.tasks.Where(t => (t.title + " " + t.assignee).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).Reverse();
            foreach (var task in tasks)
            {
                var column = task.state == "completed" || task.state == "cancelled" ? done :
                    task.state == "queued" || task.state == "running" || task.state == "sending" ? working : draft;
                column.Add(TaskCard(task));
            }
            foreach (var column in new[] { draft, working, done })
                if (column.childCount == 1) column.Add(StudioUi.Text(StudioText.T("No tasks yet"), "studio-empty"));
            var remote = (StudioWorkspace.Snapshot?["tasks"] as JArray)?.Where(t => !StudioWorkspace.Data.tasks.Any(local => local.remoteId == (string)t["task_id"])).ToList();
            if (remote?.Count > 0)
            {
                live.Add(StudioUi.Text(StudioText.T("Tasks delegated by AI through the Hub"), "studio-section-heading"));
                foreach (var task in remote.Take(20))
                {
                    string id = (string)task["task_id"];
                    var row = StudioUi.Card(id.Substring(0, Math.Min(8, id.Length)) + "  →  " + (string)task["assignee"]);
                    row.Add(StudioUi.Text(StudioUi.State((string)task["state"]) + StudioText.T(" • Created by: ") + (string)task["creator"]));
                    row.Add(StudioUi.Button(StudioText.T("View conversation"), AgentConsoleWindow.ShowWindow)); live.Add(row);
                }
            }
        }
        private VisualElement Column(string titleText)
        { var column = StudioUi.Box("studio-column"); column.Add(StudioUi.Text(titleText, "studio-column-title")); board.Add(column); return column; }
        private VisualElement TaskCard(StudioTask task)
        {
            var card = StudioUi.Card(task.title); card.name = "task-" + task.id;
            card.Add(StudioUi.Text(StudioUi.State(task.state) + "  /  " + task.assignee, "studio-chip"));
            string preview = task.request ?? "";
            card.Add(StudioUi.Text(preview.Length > 220 ? preview.Substring(0, 220) + "…" : preview, "studio-task-body"));
            if (!string.IsNullOrEmpty(task.scheduledUtc) && DateTime.TryParse(task.scheduledUtc, out DateTime date)) card.Add(StudioUi.Text(StudioText.T("Scheduled: ") + date.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
            if (!string.IsNullOrEmpty(task.error)) card.Add(StudioUi.Text(task.error, "studio-feedback"));
            if (!string.IsNullOrEmpty(task.result)) card.Add(StudioUi.Text(task.result, "studio-task-body"));
            var actions = StudioUi.Box("studio-agent-actions");
            if (task.state == "draft" || task.state == "failed")
                actions.Add(StudioUi.Button(task.state == "failed" ? StudioText.T("Retry") : StudioText.T("Send"), () => StudioUi.Run(() => StudioWorkspace.DispatchAsync(task), feedback)));
            if (task.state == "draft")
                actions.Add(StudioUi.Button(StudioText.T("Edit"), () => { editingId = task.id; kind.index = 1; title.value = task.title; brief.value = task.request; assignee.value = assignee.choices.FirstOrDefault(c => AgentId(c) == task.assignee); feedback.text = StudioText.T("Editing draft: ") + task.title; }));
            if (task.state != "cancelled") actions.Add(StudioUi.Button(StudioText.T("Split task"), () => {
                ClearForm(); kind.index = 1; parent.value = parent.choices.FirstOrDefault(c => c.StartsWith(task.id));
                title.value = task.title + " / "; feedback.text = StudioText.T("Enter the child task and choose its worker AI.");
            }));
            if (!string.IsNullOrEmpty(task.remoteId)) actions.Add(StudioUi.Button(StudioText.T("Read result"), () => StudioUi.Run(async () => { await StudioWorkspace.ReadResultAsync(task); Refresh(); }, feedback)));
            if (task.state != "completed" && task.state != "cancelled" && task.state != "sending")
                actions.Add(StudioUi.Button(StudioText.T("Cancel"), () => StudioUi.Run(() => StudioWorkspace.CancelAsync(task), feedback)));
            card.Add(actions); return card;
        }
    }
}

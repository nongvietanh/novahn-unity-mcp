using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services.Hub;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>The trusted desktop coordinator uses the existing Unity MCP connection.</summary>
    [McpForUnityTool("novahn_workspace", AutoRegister = true, Group = "core",
        Description = "Novahn Studio desktop chat workflow. Call connect with your own client ID: the AI that connects becomes the primary and starts the project Hub without opening any Editor window. Use request to record a user goal, delegate to assign workers, inbox/claim/complete/get/cancel for primary tasks, events for progress, add_agent/setup_client/install_skills for onboarding. Workers use their own authenticated Hub endpoint, not this coordinator gateway. status is read-only. Never claim workers are running just because they are configured.")]
    public static class NovahnWorkspace
    {
        public class Parameters
        {
            [ToolParameter("status, connect, request, delegate, inbox, claim, complete, get, cancel, events, messages, message, ack, add_agent, setup_client, install_skills", Required = false, DefaultValue = "status")]
            public string action { get; set; }
            [ToolParameter("Action arguments. connect: client (required; your own app's ID from status.availableClients). request/delegate: title, request, idempotency_key, assignee (delegate), parent_id and scheduled_at (optional ISO8601 with timezone). claim/get/complete/cancel: task_id, result (complete). add_agent: client (command-line AIs only, e.g. claudecode, codex, geminicli), optional name and responsibility; display names are automatic. setup_client/install_skills: agent_id. events: after cursor. message: recipient, body, idempotency_key, optional task_id. ack: message_id.", Required = false)]
            public object arguments { get; set; }
        }

        private static bool configuring;
        public static async Task<object> HandleCommand(JObject parameters)
        {
            var p = parameters ?? new JObject();
            string action = ((string)p["action"] ?? "status").ToLowerInvariant();
            var a = p["arguments"] as JObject ?? new JObject();
            try
            {
                if (action == "status")
                {
                    if (StudioHubRuntime.IsRunning) await StudioWorkspace.RefreshAsync();
                    return new SuccessResponse("Novahn workspace", Status());
                }
                if (action == "connect") return Connect(a);
                var leader = StudioWorkspace.Team.Leader;
                if (leader == null) return new ErrorResponse("connect_required", new { hint = "Call connect first. No Unity Editor input is needed." });
                if (action == "add_agent") return AddAgent(a);
                if (action == "install_skills" || action == "setup_client")
                {
                    var agent = FindAgent((string)a["agent_id"] ?? leader.id);
                    if (action == "setup_client") StudioClientSetup.Configure(agent);
                    int count = StudioSkillCatalog.InstallBundle(agent);
                    return new SuccessResponse("Agent ready for client onboarding", new {
                        agentId = agent.id, installedSkills = count, skillsPath = StudioSkillCatalog.InstallPath(agent),
                        joinPrompt = StudioClientSetup.JoinPrompt(agent),
                        note = "The client may need a new session or MCP reconnect after configuration. This does not launch an AI model."
                    });
                }
                if (action == "get" || action == "cancel")
                {
                    var local = StudioWorkspace.Data.tasks.FirstOrDefault(t => t.id == (string)a["task_id"]);
                    if (local != null && string.IsNullOrEmpty(local.remoteId))
                    {
                        if (action == "cancel") await StudioWorkspace.CancelAsync(local);
                        return new SuccessResponse("Local task", new { localId = local.id, state = local.state,
                            title = local.title, request = local.request, assignee = local.assignee, scheduledUtc = local.scheduledUtc });
                    }
                }
                if (!StudioHubRuntime.IsRunning)
                {
                    StudioHubRuntime.Start();
                    return Starting();
                }
                if (!StudioWorkspace.Online)
                {
                    await StudioWorkspace.RefreshAsync();
                    if (!StudioWorkspace.Online) return Starting();
                }
                switch (action)
                {
                    case "request": case "delegate":
                        return await Create(a, leader, action == "request");
                    case "inbox": return await Call(leader, "hub.task.inbox", new JObject());
                    case "messages": return await Call(leader, "hub.message.inbox", new JObject());
                    case "message": return await Call(leader, "hub.message.send", new JObject {
                        ["recipient"] = Required(a, "recipient"), ["body"] = Required(a, "body"),
                        ["idempotency_key"] = Required(a, "idempotency_key"), ["task_id"] = (string)a["task_id"] });
                    case "ack": return await Call(leader, "hub.message.ack", new JObject { ["message_id"] = Required(a, "message_id") });
                    case "claim": case "get": case "cancel":
                        return await Call(leader, "hub.task." + action, new JObject { ["task_id"] = TaskId(a) });
                    case "complete":
                        return await Call(leader, "hub.task.complete", new JObject { ["task_id"] = TaskId(a), ["result"] = Required(a, "result") });
                    case "events":
                        // Bounded observation keeps the Unity bridge free for other clients.
                        return await Call(leader, "hub.events.wait", new JObject { ["after"] = (long?)a["after"] ?? 0, ["timeout_seconds"] = 1 });
                    default: return new ErrorResponse("unknown_action", new { action });
                }
            }
            catch (Exception e) { return new ErrorResponse(e.Message); }
        }

        private static object Connect(JObject a)
        {
            if (configuring) return new ErrorResponse("setup_in_progress");
            configuring = true;
            try
            {
                string clientKey = (string)a["client"];
                if (string.IsNullOrWhiteSpace(clientKey)) return new ErrorResponse("client_selection_required", new {
                    hint = "Pass the ID of the AI app you are running in. The AI that connects becomes the primary.",
                    availableClients = StudioClientSetup.AvailableClients().Select(c => new { id = c.Id, name = c.DisplayName })
                });
                var previous = StudioWorkspace.Team.Leader?.clientKey;
                var leader = StudioClientSetup.ClaimPrimary(clientKey);
                if (previous != leader.clientKey)
                {
                    leader.responsibility = "Receive user requests in desktop chat, plan, delegate and combine results.";
                    StudioWorkspace.Save();
                    StudioSkillCatalog.InstallBundle(leader);
                }
                // No client configuration is rewritten: this tool already arrived over Unity MCP.
                if (!StudioHubRuntime.IsRunning) StudioHubRuntime.Start();
                return new SuccessResponse("Connected as primary coordinator", new {
                    agentId = leader.id, role = "primary", workspace = Status(),
                    instructions = "Use request to record the user's goal with a stable idempotency_key, then claim it. Work directly or delegate self-contained tasks to configured workers. Inspect recent activity before relying on a worker. Read real results with get; complete the primary goal only after all required work is done. Continue in this desktop chat; no Unity Editor request is needed.",
                    workerOnboarding = "Team members are command-line AIs (Claude Code, Codex, Gemini CLI, ...). Use add_agent then setup_client for one. Open its CLI session with the returned join prompt so it waits on its own endpoint. The Hub cannot launch a model."
                });
            }
            finally { configuring = false; }
        }

        private static object AddAgent(JObject a)
        {
            string client = StudioClientSetup.RequireClient(Required(a, "client")).Id;
            string name = (string)a["name"];
            var existing = StudioWorkspace.Team.agents.FirstOrDefault(agent => agent.displayName == name && agent.clientKey == client);
            if (existing != null) return new SuccessResponse("Agent already exists", new { agentId = existing.id });
            if (configuring) return new ErrorResponse("setup_in_progress");
            configuring = true;
            try
            {
                bool running = StudioHubRuntime.IsRunning;
                if (running) StudioHubRuntime.Stop();
                var agent = StudioClientSetup.AddWorker(client, name);
                agent.responsibility = (string)a["responsibility"] ?? "As assigned by the primary AI.";
                StudioWorkspace.Save();
                if (running) StudioHubRuntime.Start();
                return new SuccessResponse("Worker added", new { agentId = agent.id, role = "worker", nextAction = "setup_client", hubRestarted = running });
            }
            finally { configuring = false; }
        }

        private static async Task<object> Create(JObject a, HubAgentEntry leader, bool goal)
        {
            string title = Required(a, "title"), request = Required(a, "request"), key = Required(a, "idempotency_key");
            string assignee = goal ? leader.id : Required(a, "assignee");
            FindAgent(assignee);
            string localId = "chat-" + UnityEngine.Hash128.Compute(leader.id + ":" + key);
            var task = StudioWorkspace.Data.tasks.FirstOrDefault(t => t.id == localId);
            if (task != null && (task.title != title || task.request != request || task.assignee != assignee))
                return new ErrorResponse("idempotency_conflict", new { hint = "Reuse the key only for the same request." });
            if (task == null)
            {
                DateTime? scheduled = null;
                string date = (string)a["scheduled_at"];
                if (!string.IsNullOrEmpty(date))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(date, @"(Z|[+-]\d{2}:\d{2})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                        || !DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                        || parsed <= DateTimeOffset.UtcNow)
                        throw new ArgumentException("scheduled_at must be a future ISO8601 timestamp with an explicit timezone.");
                    scheduled = parsed.LocalDateTime;
                }
                // Persist the stable identity in the first write, before HTTP delivery.
                task = new StudioTask { id = localId, title = title, request = request, assignee = assignee,
                    creator = leader.id, parentId = (string)a["parent_id"], scheduledUtc = scheduled?.ToUniversalTime().ToString("o"),
                    state = scheduled.HasValue ? "scheduled" : "draft" };
                StudioWorkspace.Data.tasks.Add(task); StudioWorkspace.Save();
            }
            if (task.state == "draft" || task.state == "failed") await StudioWorkspace.DispatchAsync(task);
            if (task.state == "failed") return new ErrorResponse(task.error, new { localId = task.id, retryWithSameKey = true });
            return new SuccessResponse("Work recorded", new { localId = task.id, taskId = task.remoteId, state = task.state,
                assignee = task.assignee, scheduledUtc = task.scheduledUtc,
                nextAction = goal && !string.IsNullOrEmpty(task.remoteId) ? "claim" : "Observe the task; a worker must be connected to receive it." });
        }

        private static async Task<object> Call(HubAgentEntry leader, string tool, JObject arguments)
        {
            var result = await StudioHubClient.CallAsync(leader, tool, arguments);
            await StudioWorkspace.RefreshAsync();
            return new SuccessResponse("Hub response", result);
        }
        private static object Starting() => new SuccessResponse("Hub starting; repeat the same action shortly", new {
            state = "starting", retryAfterSeconds = 2, logPath = StudioHubRuntime.LogPath });
        private static HubAgentEntry FindAgent(string id) => StudioWorkspace.Team.agents.FirstOrDefault(a => a.id == id)
            ?? throw new ArgumentException("Unknown agent_id: " + id);
        private static string TaskId(JObject a)
        {
            string id = Required(a, "task_id");
            var local = StudioWorkspace.Data.tasks.FirstOrDefault(t => t.id == id);
            return local?.remoteId ?? id;
        }
        private static string Required(JObject a, string key)
        {
            string value = (string)a[key];
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Required argument: " + key);
            return value.Trim();
        }
        private static object Status() => new {
            projectRoot = StudioWorkspace.ProjectRoot, instanceId = StudioHubRuntime.InstanceId,
            online = StudioWorkspace.Online, processRunning = StudioHubRuntime.IsRunning,
            primary = StudioWorkspace.Team.Leader?.id,
            availableClients = StudioClientSetup.AvailableClients().Select(c => new { id = c.Id, name = c.DisplayName }),
            agents = StudioWorkspace.Team.agents.Select(a => new { agentId = a.id, name = a.displayName,
                role = a.isLeader ? "primary" : "worker", client = a.clientKey, responsibility = a.responsibility }),
            snapshot = StudioWorkspace.Snapshot,
            localTasks = StudioWorkspace.Data.tasks.Select(t => new { localId = t.id, taskId = t.remoteId, title = t.title, state = t.state }),
            note = "This is the trusted primary coordinator gateway on Unity MCP. Worker clients must use their own authenticated Hub endpoint. Configured agents are not necessarily active."
        };
    }
}

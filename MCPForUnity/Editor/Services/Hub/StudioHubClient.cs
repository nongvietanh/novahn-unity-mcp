using MCPForUnity.Editor.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.Networking;

namespace MCPForUnity.Editor.Services.Hub
{
    /// <summary>Small Streamable HTTP MCP client for the Editor's task controls.</summary>
    public static class StudioHubClient
    {
        private static readonly HashSet<UnityWebRequest> Requests = new HashSet<UnityWebRequest>();
        static StudioHubClient()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                foreach (var request in Requests.ToArray()) request.Abort();
            };
        }

        public static async Task<JObject> GetAsync(string url)
        {
            using (var request = UnityWebRequest.Get(url))
            { await SendAsync(request); return JObject.Parse(request.downloadHandler.text); }
        }

        public static async Task<JObject> CallAsync(HubAgentEntry agent, string tool, JObject arguments)
        {
            string session = null;
            string protocol = "2025-03-26";
            using (var init = Post(agent, new JObject {
                ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize", ["params"] = new JObject {
                    ["protocolVersion"] = protocol, ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = "Novahn Studio", ["version"] = "0.1.0" } } }, null, protocol))
            {
                await SendAsync(init);
                session = init.GetResponseHeader("Mcp-Session-Id");
                var response = ParseResponse(init.downloadHandler.text);
                protocol = (string)response["result"]?["protocolVersion"] ?? protocol;
            }
            using (var notification = Post(agent, new JObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }, session, protocol))
                await SendAsync(notification);
            using (var call = Post(agent, new JObject { ["jsonrpc"] = "2.0", ["id"] = 2,
                ["method"] = "tools/call", ["params"] = new JObject { ["name"] = tool, ["arguments"] = arguments } }, session, protocol))
            {
                await SendAsync(call);
                var rpc = ParseResponse(call.downloadHandler.text);
                if (rpc["error"] != null) throw new InvalidOperationException((string)rpc["error"]?["message"] ?? StudioText.T("MCP returned an error."));
                var result = rpc["result"] as JObject;
                var payload = result?["structuredContent"] as JObject;
                if (payload == null)
                {
                    var block = (result?["content"] as JArray)?.FirstOrDefault(c => (string)c["type"] == "text");
                    if (block != null) payload = JObject.Parse((string)block["text"]);
                }
                if (payload == null) throw new InvalidOperationException(StudioText.T("The Hub returned invalid data."));
                if ((bool?)result?["isError"] == true || (string)payload["status"] == "failed")
                    throw new InvalidOperationException((string)payload["error"]?["message"] ?? payload.ToString());
                return payload;
            }
        }

        private static UnityWebRequest Post(HubAgentEntry agent, JObject body, string session, string protocol)
        {
            var request = new UnityWebRequest("http://127.0.0.1:" + agent.port + "/mcp", "POST") {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString())),
                downloadHandler = new DownloadHandlerBuffer() };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json, text/event-stream");
            request.SetRequestHeader("Authorization", "Bearer " + agent.token);
            request.SetRequestHeader("MCP-Protocol-Version", protocol);
            if (!string.IsNullOrEmpty(session)) request.SetRequestHeader("Mcp-Session-Id", session);
            return request;
        }

        public static JObject ParseResponse(string response)
        {
            if (response.TrimStart().StartsWith("{")) return JObject.Parse(response);
            // Streamable HTTP may return SSE even for a single tools/call response.
            var data = new StringBuilder();
            foreach (string line in response.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.StartsWith("data:")) data.AppendLine(line.Substring(5).TrimStart());
                else if (line.Length == 0 && data.Length > 0)
                {
                    var item = JObject.Parse(data.ToString()); data.Clear();
                    if (item["result"] != null || item["error"] != null) return item;
                }
            }
            if (data.Length > 0) return JObject.Parse(data.ToString());
            throw new InvalidOperationException(StudioText.T("No MCP response received."));
        }

        private static Task SendAsync(UnityWebRequest request)
        {
            var completion = new TaskCompletionSource<bool>();
            request.timeout = 6;
            Requests.Add(request);
            request.SendWebRequest();
            EditorApplication.CallbackFunction poll = null;
            poll = () => {
                if (!request.isDone) return;
                EditorApplication.update -= poll; Requests.Remove(request);
                if (request.result == UnityWebRequest.Result.Success) completion.TrySetResult(true);
                else completion.TrySetException(new InvalidOperationException(StudioText.T("Cannot connect to the Hub: ") + request.error));
            };
            EditorApplication.update += poll;
            return completion.Task;
        }
    }
}

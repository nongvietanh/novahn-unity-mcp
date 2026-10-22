using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// One-click update of this package and the local MCP server it launches. A git install is
    /// compared by commit, not version, so new code is found even when package.json is unchanged.
    /// </summary>
    [InitializeOnLoad]
    public static class McpPackageUpdater
    {
        private const string RestartServerKey = "MCPForUnity.RestartServerAfterUpdate";

        static McpPackageUpdater()
        {
            // Package Manager reloads the domain after an update; finish the server restart here.
            if (!SessionState.GetBool(RestartServerKey, false)) return;
            SessionState.EraseBool(RestartServerKey);
            EditorApplication.delayCall += () => RestartServer(clearCache: true);
        }

        public static bool Busy { get; private set; }

        /// <summary>"url#ref" for a git install, otherwise null. packageId is "name@source".</summary>
        public static string GitSource(PackageInfo info) =>
            info?.source == PackageSource.Git ? info.packageId.Substring(info.packageId.IndexOf('@') + 1) : null;

        /// <summary>
        /// Where updates come from: the Novahn repository, keeping the installed branch when the package
        /// already tracks it. Any other install (fork, registry, Asset Store) moves to the Novahn main branch.
        /// </summary>
        public static string UpdateSource(string installedGitSource)
        {
            if (installedGitSource != null)
            {
                var (url, reference) = SplitGitSource(installedGitSource);
                if (SameRepository(url, ProductInfo.RepositoryUrl)) return ProductInfo.PackageGitUrl + "#" + (reference == "HEAD" ? "main" : reference);
            }
            return ProductInfo.PackageGitUrl + "#main";
        }

        private static bool SameRepository(string a, string b)
        {
            string Normalize(string url) => url.Trim().TrimEnd('/').Replace(".git", "").ToLowerInvariant()
                .Replace("http://", "https://").Replace("www.", "");
            string left = Normalize(a), right = Normalize(b);
            // Credentials such as https://user@github.com/... still name the same repository.
            int at = left.IndexOf('@');
            if (at >= 0 && left.StartsWith("https://")) left = "https://" + left.Substring(at + 1);
            return left == right;
        }

        /// <summary>Splits "https://host/repo.git?path=/X#main" into the repository URL and its ref (HEAD when absent).</summary>
        public static (string url, string reference) SplitGitSource(string source)
        {
            string url = source, reference = "HEAD";
            int hash = url.IndexOf('#');
            if (hash >= 0) { reference = url.Substring(hash + 1); url = url.Substring(0, hash); }
            int query = url.IndexOf('?');
            if (query >= 0) url = url.Substring(0, query);
            if (url.StartsWith("git+", StringComparison.OrdinalIgnoreCase)) url = url.Substring(4);
            return (url, string.IsNullOrWhiteSpace(reference) ? "HEAD" : reference);
        }

        /// <summary>Reads the commit hash from the first line of `git ls-remote` output.</summary>
        public static string ParseLsRemote(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return null;
            string first = output.Trim().Split('\n')[0];
            int tab = first.IndexOfAny(new[] { '\t', ' ' });
            string commit = tab > 0 ? first.Substring(0, tab) : first;
            return commit.Length >= 7 ? commit.Trim() : null;
        }

        /// <summary>Checks, then updates when newer code exists. Reports progress through <paramref name="report"/>.</summary>
        public static async Task UpdateAsync(Action<string> report)
        {
            if (Busy) return;
            Busy = true;
            try
            {
                var info = PackageInfo.FindForAssembly(typeof(McpPackageUpdater).Assembly);
                string source = GitSource(info);
                if (info?.source == PackageSource.Local || info?.source == PackageSource.Embedded)
                {
                    // Local code is already live: reimport it and restart the server on the same code.
                    AssetDatabase.Refresh();
                    RestartServer(clearCache: false);
                    report(StudioText.T("Local package: reloaded the code and restarted the MCP server."));
                    return;
                }
                string target = UpdateSource(source);
                report(StudioText.T("Checking for new code…"));
                var (url, reference) = SplitGitSource(target);
                string remote = await Task.Run(() =>
                    ExecPath.TryRun("git", "ls-remote \"" + url + "\" " + reference, null, out string stdout, out _, 20000)
                        ? ParseLsRemote(stdout) : null);
                if (remote == null)
                {
                    report(StudioText.T("Cannot reach the repository. Check Git and your network."));
                    return;
                }
                if (target == source && string.Equals(remote, info.git?.hash, StringComparison.OrdinalIgnoreCase))
                {
                    report(StudioText.T("Already up to date (") + remote.Substring(0, 7) + ").");
                    return;
                }

                report(StudioText.T("Updating to ") + remote.Substring(0, 7) + "…");
                SessionState.SetBool(RestartServerKey, MCPServiceLocator.Server.IsLocalHttpServerRunning());
                AddRequest request = Client.Add(target);
                while (!request.IsCompleted) await Task.Delay(200);
                if (request.Status == StatusCode.Success)
                    report(StudioText.T("Updated. Unity is reloading the package."));
                else
                {
                    SessionState.EraseBool(RestartServerKey);
                    report(StudioText.T("Update failed: ") + request.Error?.message);
                }
            }
            finally { Busy = false; }
        }

        private static void RestartServer(bool clearCache)
        {
            var server = MCPServiceLocator.Server;
            if (!server.IsLocalHttpServerRunning()) return;
            server.StopLocalHttpServer();
            if (clearCache) server.ClearUvxCache();
            server.StartLocalHttpServer(quiet: true);
        }
    }
}

using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Setup;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace MCPForUnity.Editor.Windows
{
    public partial class MCPForUnityEditorWindow : EditorWindow
    {
        private static void BuildDependenciesSection(VisualElement container)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var title = new Label("Optional Dependencies");
            title.AddToClassList("section-title");
            section.Add(title);

            var content = new VisualElement();
            content.AddToClassList("section-content");

            var desc = new Label("Some tool groups require optional packages. Install them to unlock additional capabilities.");
            desc.AddToClassList("validation-description");
            desc.style.marginBottom = 4;
            content.Add(desc);

            // Install All / Uninstall All buttons
            var bulkRow = new VisualElement();
            bulkRow.style.flexDirection = FlexDirection.Row;
            bulkRow.style.marginBottom = 8;

            var upmPackages = new[] { "com.unity.probuilder", "com.unity.cinemachine", "com.unity.visualeffectgraph", "com.unity.cloud.gltfast" };

            Button installAllButton = null;
            installAllButton = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Install All Dependencies",
                    "This will install Roslyn DLLs, ProBuilder, Cinemachine, VFX Graph, and glTFast. Continue?",
                    "Install All", "Cancel")) return;
                installAllButton.SetEnabled(false);
                installAllButton.text = "Installing...";
                if (!RoslynInstaller.IsInstalled()) RoslynInstaller.Install(interactive: false);
                BatchUpmAdd(upmPackages, () =>
                {
                    installAllButton.SetEnabled(true);
                    installAllButton.text = "Install All";
                });
            });
            installAllButton.text = "Install All";
            installAllButton.AddToClassList("action-button");
            installAllButton.style.marginRight = 4;
            bulkRow.Add(installAllButton);

            Button uninstallAllButton = null;
            uninstallAllButton = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Uninstall All Dependencies",
                    "This will remove Roslyn DLLs, ProBuilder, Cinemachine, VFX Graph, and glTFast. Continue?",
                    "Uninstall All", "Cancel")) return;
                uninstallAllButton.SetEnabled(false);
                uninstallAllButton.text = "Removing...";
                UninstallRoslyn();
                BatchUpmRemove(upmPackages, () =>
                {
                    uninstallAllButton.SetEnabled(true);
                    uninstallAllButton.text = "Uninstall All";
                });
            });
            uninstallAllButton.text = "Uninstall All";
            uninstallAllButton.AddToClassList("action-button");
            bulkRow.Add(uninstallAllButton);

            content.Add(bulkRow);

            // Roslyn — for execute_code modern C# support
            // Check if Roslyn types are actually loaded (covers NuGet, Plugins folder, etc.)
            bool roslynLoaded = Type.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation, Microsoft.CodeAnalysis.CSharp") != null;
            bool roslynInstalledLocally = RoslynInstaller.IsInstalled();
            AddDependencyRow(content,
                "Roslyn (C# 12+ Compiler)",
                "Enables modern C# syntax in execute_code tool (scripting_ext group).",
                roslynLoaded,
                roslynInstalledLocally
                    ? "Installed via Plugins/Roslyn \u2014 execute_code uses Roslyn"
                    : "Available (loaded from NuGet/external) \u2014 execute_code uses Roslyn",
                "Not installed \u2014 execute_code falls back to C# 6 (CodeDom)",
                done => { RoslynInstaller.Install(interactive: true); done?.Invoke(); },
                roslynInstalledLocally
                    ? (Action<Action>)(done => { UninstallRoslyn(); done?.Invoke(); })
                    : null);

            // ProBuilder
            bool hasProBuilder = Type.GetType("UnityEngine.ProBuilder.ProBuilderMesh, Unity.ProBuilder") != null;
            AddDependencyRow(content,
                "ProBuilder",
                "Required for the manage_probuilder tool (probuilder group).",
                hasProBuilder,
                "Installed",
                "Not installed",
                done => InstallUpmPackage("com.unity.probuilder", done),
                done => RemoveUpmPackage("com.unity.probuilder", done));

            // Cinemachine
            bool hasCinemachine = Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine") != null
                || Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine") != null;
            AddDependencyRow(content,
                "Cinemachine",
                "Enhances manage_camera with virtual camera support (core group).",
                hasCinemachine,
                "Installed",
                "Not installed \u2014 camera tool works without it",
                done => InstallUpmPackage("com.unity.cinemachine", done),
                done => RemoveUpmPackage("com.unity.cinemachine", done));

            // VFX Graph — uses preprocessor symbol, so check via UPM package list
            bool hasVfxGraph = IsUpmPackageInstalled("com.unity.visualeffectgraph");
            AddDependencyRow(content,
                "VFX Graph",
                "Enables VisualEffect support in manage_vfx tool (vfx group).",
                hasVfxGraph,
                "Installed",
                "Not installed \u2014 VFX tool falls back to ParticleSystem/LineRenderer",
                done => InstallUpmPackage("com.unity.visualeffectgraph", done),
                done => RemoveUpmPackage("com.unity.visualeffectgraph", done));

            // glTFast — uses an assembly type, but also check via UPM package list
            bool hasGltfast = IsUpmPackageInstalled("com.unity.cloud.gltfast") || Type.GetType("GLTFast.GltfImport, glTFast") != null;
            AddDependencyRow(content,
                "glTFast (glTF/GLB import)",
                "Enables .glb/.gltf model import for the AI Asset Generation tools (asset_gen group).",
                hasGltfast,
                "Installed — GLB generation/import works",
                "Not installed — GLB import is unavailable; FBX still works, or install to enable GLB",
                done => InstallUpmPackage("com.unity.cloud.gltfast", done),
                done => RemoveUpmPackage("com.unity.cloud.gltfast", done));

            section.Add(content);
            container.Add(section);
        }

        private static void AddDependencyRow(VisualElement parent, string name, string description,
            bool isInstalled, string installedText, string missingText,
            Action<Action> installAction, Action<Action> uninstallAction)
        {
            var row = new VisualElement();
            row.style.marginBottom = 8;
            row.style.paddingBottom = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 2;

            var nameLabel = new Label(name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.flexGrow = 1;
            header.Add(nameLabel);

            var statusIcon = new Label(isInstalled ? "\u2713" : "\u2717");
            statusIcon.style.color = isInstalled ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.4f, 0.4f);
            statusIcon.style.fontSize = 14;
            header.Add(statusIcon);

            row.Add(header);

            var descLabel = new Label(description);
            descLabel.AddToClassList("validation-description");
            descLabel.style.marginBottom = 2;
            row.Add(descLabel);

            var statusText = new Label(isInstalled ? installedText : missingText);
            statusText.style.fontSize = 11;
            statusText.style.color = isInstalled ? new Color(0.6f, 0.8f, 0.6f) : new Color(0.8f, 0.7f, 0.5f);
            row.Add(statusText);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 4;

            if (!isInstalled && installAction != null)
            {
                Button btn = null;
                btn = new Button(() =>
                {
                    btn.SetEnabled(false);
                    btn.text = "Installing...";
                    Action restore = () =>
                    {
                        btn.SetEnabled(true);
                        btn.text = "Install";
                    };
                    try { installAction(restore); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MCP] Install failed: {e.Message}");
                        restore();
                    }
                });
                btn.text = "Install";
                btn.AddToClassList("action-button");
                buttonRow.Add(btn);
            }

            if (isInstalled && uninstallAction != null)
            {
                Button btn = null;
                btn = new Button(() =>
                {
                    if (!EditorUtility.DisplayDialog("Remove " + name,
                        $"Are you sure you want to remove {name}?", "Remove", "Cancel")) return;
                    btn.SetEnabled(false);
                    btn.text = "Removing...";
                    Action restore = () =>
                    {
                        btn.SetEnabled(true);
                        btn.text = "Uninstall";
                    };
                    try { uninstallAction(restore); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MCP] Uninstall failed: {e.Message}");
                        restore();
                    }
                });
                btn.text = "Uninstall";
                btn.AddToClassList("action-button");
                buttonRow.Add(btn);
            }

            if (buttonRow.childCount > 0)
                row.Add(buttonRow);

            parent.Add(row);
        }

        private static void InstallUpmPackage(string packageId, Action onComplete = null)
        {
            BatchUpmAdd(new[] { packageId }, onComplete);
        }

        private static void RemoveUpmPackage(string packageId, Action onComplete = null)
        {
            BatchUpmRemove(new[] { packageId }, onComplete);
        }

        private static void BatchUpmAdd(string[] packageIds, Action onComplete = null)
        {
            var request = UnityEditor.PackageManager.Client.AddAndRemove(packageIds, null);
            EditorUtility.DisplayProgressBar("Installing Packages", $"Installing {packageIds.Length} package(s)...", 0.5f);
            PollUpmRequest(request, "install", onComplete);
        }

        private static void BatchUpmRemove(string[] packageIds, Action onComplete = null)
        {
            var request = UnityEditor.PackageManager.Client.AddAndRemove(null, packageIds);
            EditorUtility.DisplayProgressBar("Removing Packages", $"Removing {packageIds.Length} package(s)...", 0.5f);
            PollUpmRequest(request, "remove", onComplete);
        }

        private static void PollUpmRequest(UnityEditor.PackageManager.Requests.AddAndRemoveRequest request, string verb, Action onComplete)
        {
            EditorApplication.CallbackFunction pollCallback = null;
            pollCallback = () =>
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= pollCallback;
                EditorUtility.ClearProgressBar();
                if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                    Debug.Log($"[MCP] Package {verb} succeeded.");
                else
                    Debug.LogError($"[MCP] Package {verb} failed: {request.Error?.message}");
                onComplete?.Invoke();
            };
            EditorApplication.update += pollCallback;
        }

        private static void UninstallRoslyn()
        {
            string folder = System.IO.Path.Combine(Application.dataPath, "Plugins/Roslyn");
            if (System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.Delete(folder, true);
                string metaPath = folder + ".meta";
                if (System.IO.File.Exists(metaPath))
                    System.IO.File.Delete(metaPath);
                AssetDatabase.Refresh();
                Debug.Log("[MCP] Roslyn DLLs removed from Assets/Plugins/Roslyn/");
            }
        }

        private static bool IsUpmPackageInstalled(string packageId)
        {
            // Check manifest.json directly — faster than async UPM API
            string manifestPath = System.IO.Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!System.IO.File.Exists(manifestPath)) return false;
            string manifest = System.IO.File.ReadAllText(manifestPath);
            return manifest.Contains($"\"{packageId}\"");
        }
    }
}

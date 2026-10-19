using MCPForUnity.Editor.Constants;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Studio
{
    internal static class StudioUi
    {
        // Embedded packages can reload into an Editor panel before its font theme is attached.
        public static void ApplyFont(VisualElement root)
        {
            int major;
            int.TryParse(Application.unityVersion.Split('.')[0], out major);
            root.style.unityFont = UnityEngine.Resources.GetBuiltinResource<Font>(major >= 2022 ? "LegacyRuntime.ttf" : "Arial.ttf");
        }
        public static VisualElement Box(string css) { var box = new VisualElement(); box.AddToClassList(css); return box; }
        public static Label Text(string text, string css = "studio-muted") { var label = new Label(text); label.AddToClassList(css); return label; }
        public static VisualElement Card(string title = null)
        { var box = Box("studio-card"); if (title != null) box.Add(Text(title, "studio-card-title")); return box; }
        public static Label Note(string text) => Text(text, "studio-note");
        public static Button Button(string title, Action action, bool primary = false)
        {
            var button = new Button(action) { text = title };
            button.AddToClassList("studio-button"); if (primary) button.AddToClassList("primary"); return button;
        }
        public static void Safe(Action action, Label feedback)
        { try { action(); } catch (Exception e) { feedback.text = e.Message; } }
        public static async void Run(Func<Task> action, Label feedback)
        { try { await action(); } catch (Exception e) { feedback.text = e.Message; } }
        public static T Field<T>(T field) where T : VisualElement { field.AddToClassList("studio-form-field"); return field; }
        public static string State(string state)
        {
            switch (state)
            {
                case "draft": return StudioText.T("Draft"); case "scheduled": return StudioText.T("Scheduled");
                case "sending": return StudioText.T("Sending"); case "queued": return StudioText.T("Waiting for AI");
                case "running": return StudioText.T("AI working"); case "completed": return StudioText.T("Completed");
                case "cancelled": return StudioText.T("Cancelled"); case "failed": return StudioText.T("Delivery failed");
                default: return state;
            }
        }
    }
}

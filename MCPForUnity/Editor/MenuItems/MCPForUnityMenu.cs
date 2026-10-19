using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Setup;
using MCPForUnity.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.MenuItems
{
    public static class MCPForUnityMenu
    {
        [MenuItem(ProductInfo.MenuRoot + "/Studio %#m", priority = 1)]
        public static void ToggleMCPWindow()
        {
            MCPForUnityEditorWindow.ShowWindow();
        }

        [MenuItem(ProductInfo.MenuRoot + "/Unity Connection", priority = 20)]
        public static void ShowSetupWindow()
        {
            MCPForUnityEditorWindow.ShowPage(StudioPage.Connection);
        }


        [MenuItem(ProductInfo.MenuRoot + "/Advanced/Editor Preferences", priority = 100)]
        public static void ShowEditorPrefsWindow()
        {
            EditorPrefsWindow.ShowWindow();
        }

        [MenuItem(ProductInfo.MenuRoot + "/AI Team", priority = 2)]
        public static void ShowTeam() => MCPForUnityEditorWindow.ShowPage(StudioPage.Team);
        [MenuItem(ProductInfo.MenuRoot + "/Tasks", priority = 3)]
        public static void ShowTasks() => MCPForUnityEditorWindow.ShowPage(StudioPage.Tasks);
        [MenuItem(ProductInfo.MenuRoot + "/Skills", priority = 4)]
        public static void ShowSkills() => MCPForUnityEditorWindow.ShowPage(StudioPage.Skills);
    }
}

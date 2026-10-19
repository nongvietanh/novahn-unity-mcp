using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Studio
{
    // Keep unsent text and selections when the authored UI is rebuilt in another language.
    internal sealed class StudioInputSnapshot
    {
        private List<string> text;
        private List<int> choices;
        private List<bool> toggles;
        public static StudioInputSnapshot Capture(VisualElement root) => new StudioInputSnapshot {
            text = root.Query<TextField>().ToList().Select(f => f.value).ToList(),
            choices = root.Query<DropdownField>().ToList().Select(f => f.index).ToList(),
            toggles = root.Query<Toggle>().ToList().Select(f => f.value).ToList()
        };
        public void Restore(VisualElement root)
        {
            var fields = root.Query<TextField>().ToList();
            for (int i = 0; i < fields.Count && i < text.Count; i++) fields[i].SetValueWithoutNotify(text[i]);
            var dropdowns = root.Query<DropdownField>().ToList();
            for (int i = 0; i < dropdowns.Count && i < choices.Count; i++)
                if (choices[i] >= 0 && choices[i] < dropdowns[i].choices.Count)
                    dropdowns[i].SetValueWithoutNotify(dropdowns[i].choices[choices[i]]);
            var checks = root.Query<Toggle>().ToList();
            for (int i = 0; i < checks.Count && i < toggles.Count; i++) checks[i].SetValueWithoutNotify(toggles[i]);
        }
    }
}

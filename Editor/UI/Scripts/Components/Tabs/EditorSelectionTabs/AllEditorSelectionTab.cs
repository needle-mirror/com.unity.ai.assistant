using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Bridge.Editor;
using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AllEditorSelectionTab : EditorSelectionTab
    {
        public override int Order => 1;

        public override string Instruction1Message => "Nothing selected.";

        public override string Instruction2Message => "Select items from the hierarchy, or assets to add them as an attachment.";

        public AllEditorSelectionTab() : base("Selection")
        {
        }

        public override void RefreshExtraResults(List<LogData> gatheredConsoleLogList)
        {
            TabSearchResults.Clear();
            TabSearchResults.AddRange(Selection.objects.Where(IsSupportedAsset));

            NumberOfResults = TabSearchResults.Count;
        }
    }
}

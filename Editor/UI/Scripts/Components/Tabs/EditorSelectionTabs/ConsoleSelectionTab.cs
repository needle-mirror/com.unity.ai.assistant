using System.Collections.Generic;
using Unity.AI.Assistant.Bridge.Editor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ConsoleSelectionTab : EditorSelectionTab
    {
        public override int Order => 0;

        public override string Instruction1Message => "No console logs selected.";

        public override string Instruction2Message =>
            "Select any error(s), warning(s) or log(s) to add them as an attachment.";

        public override bool DisplayConsoleLogs => true;

        public ConsoleSelectionTab() : base("Console")
        {
        }

        public override void RefreshExtraResults(List<LogData> gatheredConsoleLogList)
        {
            var resultCount = TabSearchResults.Count;
            NumberOfResults = resultCount + (gatheredConsoleLogList?.Count ?? 0);
        }
    }
}

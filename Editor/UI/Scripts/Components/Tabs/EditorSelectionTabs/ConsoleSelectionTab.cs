using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ConsoleSelectionTab : EditorSelectionTab
    {
        public override string DefaultLabel => "Console";

        public override int Order => 0;
        public override SelectionType Type => SelectionType.Console;
        public override string Instruction1Message => "No console logs selected.";

        public override string Instruction2Message => "Select any error(s), warning(s) or log(s) to add them as an attachment.";

        public ConsoleSelectionTab() : base("Console")
        {
        }
    }
}

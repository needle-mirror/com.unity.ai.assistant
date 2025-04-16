using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class EditorSelectionTab: SelectionPopupTab
    {
        public override string DefaultLabel => "Selection";

        public EditorSelectionTab() : base("Selection")
        {
        }
    }
}

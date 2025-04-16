using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class HierarchyPopupTab: SearchableTab
    {
        public override string Filter => "h: ";
        public override string DefaultLabel => "Hierarchy";
        public override int Order => 2;

        public HierarchyPopupTab() : base("Hierarchy")
        {
        }
    }
}

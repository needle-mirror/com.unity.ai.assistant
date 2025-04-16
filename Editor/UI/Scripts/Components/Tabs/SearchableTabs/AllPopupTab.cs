using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AllPopupTab: SearchableTab
    {
        public override string Filter => string.Empty;

        public override string DefaultLabel => "All";
        public override int Order => 0;

        public AllPopupTab() : base("All")
        {
        }
    }
}

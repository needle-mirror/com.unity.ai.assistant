using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ProjectPopupTab : SearchableTab
    {
        public override string Filter => "p: ";
        public override string DefaultLabel => "Project";
        public override int Order => 1;

        public ProjectPopupTab() : base("Project")
        {
        }
    }
}

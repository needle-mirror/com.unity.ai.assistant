
namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class HierarchyPopupTab: SearchableTab
    {
        public override SearchContextWrapper[] SearchProviders => new[] { HierarchySearchContext };
        public override string DefaultLabel => "Hierarchy";
        public override int Order => 2;

        public HierarchyPopupTab() : base("Hierarchy")
        {
        }
    }
}

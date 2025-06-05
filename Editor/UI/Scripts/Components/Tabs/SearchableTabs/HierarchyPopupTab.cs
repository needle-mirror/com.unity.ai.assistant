namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class HierarchyPopupTab: SearchableTab
    {
        public override SearchContextWrapper[] SearchProviders => new[] { GetSearchContextWrapper("scene") };
        public override int Order => 2;

        public HierarchyPopupTab() : base("Hierarchy")
        {
        }
    }
}

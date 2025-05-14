
namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AllPopupTab: SearchableTab
    {
        public override SearchContextWrapper[] SearchProviders =>
            new[] { HierarchySearchContext, ProjectSearchContext };

        public override string DefaultLabel => "All";
        public override int Order => 0;

        public AllPopupTab() : base("All")
        {
        }

        internal override void NewResultsReceived()
        {
            // Do not clear, we have multiple SearchProviders.
        }
    }
}

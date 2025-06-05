namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ProjectPopupTab : SearchableTab
    {
        public override SearchContextWrapper[] SearchProviders => new[] { GetSearchContextWrapper("asset") };

        public override int Order => 1;

        public ProjectPopupTab() : base("Project")
        {
        }
    }
}

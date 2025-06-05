namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AllPopupTab: SearchableTab
    {
        public override SearchContextWrapper[] SearchProviders =>
            new[] { GetSearchContextWrapper("scene"), GetSearchContextWrapper("asset") };

        public override int Order => 0;

        public AllPopupTab() : base("All")
        {
        }
    }
}

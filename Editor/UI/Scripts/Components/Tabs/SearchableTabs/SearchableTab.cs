namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class SearchableTab: SelectionPopupTab
    {
        public abstract string Filter { get; }
        public abstract int Order { get; }
        protected SearchableTab(string label) : base(label)
        {
        }
    }
}

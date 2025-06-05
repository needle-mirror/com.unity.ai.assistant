namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class EditorSelectionTab: SelectionPopupTab
    {
        public override bool SearchEnabled => false;
        public override string SearchTooltip => "Searching and filtering are not available for Console and Selection.";

        public abstract int Order { get; }

        protected EditorSelectionTab(string label) : base(label)
        {
        }
    }
}

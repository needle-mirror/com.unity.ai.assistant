namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class SearchableTab: SelectionPopupTab
    {
        public abstract string Filter { get; }
        public abstract int Order { get; }

        public override string Instruction1Message => "Search sources inside of Unity and attach them your prompt for additional context.";

        public override string Instruction2Message => "Or drag and drop them directly below.";


        protected SearchableTab(string label) : base(label)
        {
        }
    }
}

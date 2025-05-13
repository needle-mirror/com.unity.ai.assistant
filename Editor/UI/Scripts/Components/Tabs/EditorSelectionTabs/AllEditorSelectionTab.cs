namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AllEditorSelectionTab : EditorSelectionTab
    {
        public override string DefaultLabel => "Selection";

        public override int Order => 1;
        public override SelectionType Type => SelectionType.UnityObject;
        public override string Instruction1Message => "Nothing selected.";

        public override string Instruction2Message => "Select items from the hierarchy, or assets to add them as an attachment.";

        public AllEditorSelectionTab() : base("Selection")
        {
        }
    }
}

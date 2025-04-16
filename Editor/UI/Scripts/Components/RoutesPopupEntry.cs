using Unity.AI.Assistant.Editor.Commands;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    internal class RoutesPopupEntry : ManagedTemplate
    {
        const string k_RouteChipHoveredClass = "mui-route-chip-hovered";

        Label m_LabelElement;
        Label m_DescriptionElement;

        public RoutesPopupEntry()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public ChatCommandHandler Command { get; private set; }

        protected override void InitializeView(TemplateContainer view)
        {
            m_LabelElement = view.Q<Label>("commandItemText");
            m_DescriptionElement = view.Q<Label>("commandItemDescription");
        }

        public void SetCommand(ChatCommandHandler command)
        {
            Command = command;
            RefreshUI();
        }

        public void SetHovered(bool hovered)
        {
            if (hovered)
            {
                AddToClassList(k_RouteChipHoveredClass);
            }
            else
            {
                RemoveFromClassList(k_RouteChipHoveredClass);
            }
        }

        void RefreshUI()
        {
            if (Command == null)
            {
                m_LabelElement.text = "#UNSET";
                m_DescriptionElement.text = string.Empty;
            }
            else
            {
                m_LabelElement.text = Command?.Label;
                m_DescriptionElement.text = Command?.PlaceHolderText;
            }
        }
    }
}

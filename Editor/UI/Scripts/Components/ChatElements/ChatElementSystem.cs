using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementSystem : ChatElementBase
    {
        readonly IList<VisualElement> m_TextFields = new List<VisualElement>();

        VisualElement m_TextFieldRoot;
        public override void SetData(MessageModel message)
        {
            base.SetData(message);

            RefreshText(m_TextFieldRoot, m_TextFields);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_TextFieldRoot = view.Q<VisualElement>("textFieldRoot");
        }
    }
}

using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ObjectPill: ManagedTemplate
    {
        AssistantImage m_Icon;
        Label m_Label;
        Object m_PillObject;
        string m_PillName;

        public ObjectPill()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Label = view.Q<Label>("objectPillLabel");
            m_Icon = view.SetupImage("objectPillIcon");
        }

        internal void SetData(Object pillObject)
        {
            m_Label.text = pillObject.name;
            m_Icon.SetTexture(pillObject.GetTextureForObjectType());
            RegisterCallback<ClickEvent>(evt => EditorGUIUtility.PingObject(pillObject));
        }
    }
}

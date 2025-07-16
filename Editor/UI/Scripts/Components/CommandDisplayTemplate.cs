using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class CommandDisplayTemplate : ManagedTemplate
    {
        public class ContentGroup
        {
            public string Content = "";
            public DisplayState State;
            public string Logs = "";

        }
        public enum DisplayState
        {
            Success = 0,
            Fail
        }

        List<ContentGroup> m_Content = new List<ContentGroup>();

        internal MessageModel m_ParentMessage;
        internal bool m_MessageReady = false;

        internal void SetMessage(MessageModel message)
        {
            m_ParentMessage = message;
            m_MessageReady = true;
            OnSetMessage();
        }
        public virtual void OnSetMessage() { }
        protected CommandDisplayTemplate(System.Type customElementType, string basePath = null) : base(customElementType, basePath) { }
        protected CommandDisplayTemplate(string basePath = null, string subPath = null) : base(basePath, subPath) { }
        public string Fence { get; set; }

        public void SetContent(string content)
        {
            m_Content.Clear();
            m_Content.Add(new ContentGroup { Content = content });
        }

        public void AddContent(string content)
        {
            m_Content.Add(new ContentGroup { Content = content });
        }

        public List<ContentGroup> ContentGroups => m_Content;

        public bool Validate(int index)
        {
            if (index < 0 || index >= m_Content.Count)
            {
                Debug.LogWarning("Invalid index supplied");
                return false;
            }

            var group = m_Content[index];
            var valid = ValidateInternal(index, out group.Logs);
            group.State = valid ? DisplayState.Success : DisplayState.Fail;
            return valid;
        }

        protected virtual bool ValidateInternal(int index, out string logs)
        {
            logs = null;
            return true;
        }

        public abstract void Display(bool isUpdate = false);

        public virtual void Sync()
        {

        }

        public virtual void SetCustomTitle(string title)
        {
        }

        public virtual void SetCodeReformatting(bool reformatCode)
        {
        }

        public virtual void SetCodeType(string codeType)
        {
        }
    }
}

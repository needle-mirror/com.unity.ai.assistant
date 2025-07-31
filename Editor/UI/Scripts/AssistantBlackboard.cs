using System.Collections.Generic;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    class AssistantBlackboard
    {
        public delegate void ActiveConversationChangedDelegate(AssistantConversationId previousConversationId, AssistantConversationId currentConversationId);

        ConversationModel m_ActiveConversation;

        readonly IDictionary<AssistantConversationId, bool> k_FavoriteCache = new Dictionary<AssistantConversationId, bool>();
        readonly IDictionary<AssistantConversationId, ConversationModel> k_ConversationCache = new Dictionary<AssistantConversationId, ConversationModel>();

        readonly List<Object> k_ObjectAttachments = new();
        readonly List<LogData> k_ConsoleAttachments = new();

        bool m_StateSaveSuspended;

        public readonly IList<InspirationModel> Inspirations = new List<InspirationModel>();

        public IReadOnlyCollection<Object> ObjectAttachments => k_ObjectAttachments.AsReadOnly();
        public IReadOnlyCollection<LogData> ConsoleAttachments => k_ConsoleAttachments.AsReadOnly();

        public ICollection<ConversationModel> Conversations => k_ConversationCache.Values;

        public AssistantConversationId ActiveConversationId { get; private set; }

        public event ActiveConversationChangedDelegate ActiveConversationChanged;

        public bool IsAPIWorking;
        public bool IsAPIRepairing;
        public bool IsAPIStreaming;
        public bool IsAPICanceling;
        public bool IsAPIReadyForPrompt = true;

        public void SetActiveConversation(AssistantConversationId newConversationId)
        {
            if (ActiveConversationId.IsValid && ActiveConversationId == newConversationId)
            {
                // Same valid conversation, ignore
                return;
            }

            var previousId = ActiveConversationId;
            ActiveConversationId = newConversationId;
            m_ActiveConversation = null;

            ActiveConversationChanged?.Invoke(previousId, newConversationId);

            SaveActiveConversationState();
        }

        public void SetFavorite(AssistantConversationId id, bool state)
        {
            k_FavoriteCache[id] = state;
        }

        public bool GetFavorite(AssistantConversationId id)
        {
            if (k_FavoriteCache.TryGetValue(id, out bool state))
            {
                return state;
            }

            return false;
        }

        public void UpdateConversation(AssistantConversationId id, ConversationModel conversation)
        {
            k_ConversationCache[id] = conversation;
        }

        public ConversationModel GetConversation(AssistantConversationId id)
        {
            if (k_ConversationCache.TryGetValue(id, out var conversation))
            {
                return conversation;
            }

            return null;
        }

        public bool RemoveConversation(AssistantConversationId id)
        {
            return k_ConversationCache.Remove(id);
        }

        public ConversationModel ActiveConversation
        {
            get
            {
                if (!ActiveConversationId.IsValid)
                {
                    return null;
                }

                if (m_ActiveConversation != null)
                {
                    return m_ActiveConversation;
                }

                m_ActiveConversation = GetConversation(ActiveConversationId);
                return m_ActiveConversation;
            }
        }

        public void ClearActiveConversation()
        {
            ActiveConversationId = AssistantConversationId.Invalid;
            m_ActiveConversation = null;

            SaveActiveConversationState();
        }

        public void ClearAttachments()
        {
            k_ObjectAttachments.Clear();
            k_ConsoleAttachments.Clear();

            SaveContextState();
        }

        public void AddConsoleAttachment(LogData data)
        {
            k_ConsoleAttachments.Add(data);

            SaveContextState();
        }

        public void AddObjectAttachment(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            k_ObjectAttachments.Add(obj);

            SaveContextState();
        }

        void SaveContextState()
        {
            if (m_StateSaveSuspended)
            {
                return;
            }

            AssistantUISessionState.instance.Context =
                JsonUtility.ToJson(ContextSerializationHelper.BuildPromptSelectionContext(k_ObjectAttachments, k_ConsoleAttachments));
        }

        void SaveActiveConversationState()
        {
            if (m_StateSaveSuspended)
            {
                return;
            }

            AssistantUISessionState.instance.LastActiveConversationId = ActiveConversationId.Value;
        }

        public void SuspendStateSave()
        {
            m_StateSaveSuspended = true;
        }

        public void ResumeStateSave()
        {
            m_StateSaveSuspended = false;
        }
    }
}

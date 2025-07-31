using System.Collections.Generic;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.Assistant.UI.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantView
    {
        readonly List<AssistantContextEntry> k_SelectedContext = new();

        bool m_DelayedUpdateContextElements;
        float m_LastReportTime;

        void RegisterContextCallbacks()
        {
            ContextMenuUtility.OnObjectsAttached += OnObjectsAttached;
        }

        void UnregisterContextCallbacks()
        {
            ContextMenuUtility.OnObjectsAttached -= OnObjectsAttached;
        }

        private void OnObjectsAttached(IEnumerable<Object> objects)
        {
            bool anyAdded = false;

            foreach (var obj in objects)
            {
                if (AddObjectToContext(obj))
                {
                    anyAdded = true;
                }
            }

            if (anyAdded)
            {
                UpdateContextSelectionElements(true);
            }
        }

        bool AddObjectToContext(object droppedObject)
        {
            if (droppedObject is not UnityEngine.Object unityObject)
            {
                return false;
            }

            if (unityObject == null)
            {
                return false;
            }

            if (!IsSupportedAsset(unityObject))
            {
                var currentTime = Time.time;
                if (currentTime - m_LastReportTime > AssistantUIConstants.UIAnalyticsDebounceInterval)
                {
                    AIAssistantAnalytics.ReportContextEvent(ContextSubType.DragDropAttachedContext, d =>
                    {
                        d.IsSuccessful = "false";
                        d.ContextType = unityObject.GetType().Name;
                        d.ContextContent = unityObject.name;
                    });
                    m_LastReportTime = currentTime;
                }
                return false;
            }

            var contextEntry = unityObject.GetContextEntry();
            if (k_SelectedContext.Contains(contextEntry))
            {
                return false;
            }

            AIAssistantAnalytics.ReportContextEvent(ContextSubType.DragDropAttachedContext, d =>
            {
                d.IsSuccessful = "true";
                d.ContextType = contextEntry.ValueType;
                d.ContextContent = contextEntry.DisplayValue;
            });
            k_SelectedContext.Add(contextEntry);
            return true;
        }

        void CheckContextForDeletedAssets(string[] paths)
        {
            if (m_DelayedUpdateContextElements)
            {
                return;
            }

            var pathHash = new HashSet<string>(paths);
            for (var i = 0; i < k_SelectedContext.Count; i++)
            {
                var entry = k_SelectedContext[i];
                switch (entry.EntryType)
                {
                    case AssistantContextType.HierarchyObject:
                    case AssistantContextType.SubAsset:
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(entry.Value);
                        if (pathHash.Contains(assetPath))
                        {
                            m_DelayedUpdateContextElements = true;
                            EditorApplication.delayCall += () => UpdateContextSelectionElements(true);
                            return;
                        }

                        break;
                    }
                }
            }
        }

        void RestoreContextSelection(List<AssistantContextEntry> contextEntries)
        {
            k_SelectedContext.AddRange(contextEntries);
        }

        void SyncContextSelection(List<UnityEngine.Object> objectList, List<LogData> consoleList)
        {
            k_SelectedContext.Clear();

            if (objectList != null)
            {
                for (var i = 0; i < objectList.Count; i++)
                {
                    var entry = objectList[i].GetContextEntry();
                    if (k_SelectedContext.Contains(entry))
                    {
                        continue;
                    }

                    k_SelectedContext.Add(entry);
                }
            }

            if (consoleList != null)
            {
                for (var i = 0; i < consoleList.Count; i++)
                {
                    var entry = consoleList[i].GetContextEntry();
                    if (k_SelectedContext.Contains(entry))
                    {
                        continue;
                    }

                    k_SelectedContext.Add(entry);
                }
            }
        }

        void RemoveInvalidContextEntries()
        {
            IList<AssistantContextEntry> deleteList = new List<AssistantContextEntry>();
            for (var i = 0; i < k_SelectedContext.Count; i++)
            {
                var entry = k_SelectedContext[i];
                switch (entry.EntryType)
                {
                    case AssistantContextType.HierarchyObject:
                    case AssistantContextType.SubAsset:
                    case AssistantContextType.SceneObject:
                    {
                        if (entry.GetTargetObject() == null)
                        {
                            deleteList.Add(entry);
                        }

                        break;
                    }

                    case AssistantContextType.Component:
                    {
                        if (entry.GetComponent() == null)
                        {
                            deleteList.Add(entry);
                        }

                        break;
                    }
                }
            }

            for (var i = 0; i < deleteList.Count; i++)
            {
                k_SelectedContext.Remove(deleteList[i]);
            }
        }

        void UpdateContextSelectionElements(bool updatePopup = false)
        {
            if (updatePopup && m_SelectionPopup.visible)
            {
                m_SelectionPopup.PopulateSearchListView();
            }

            RemoveInvalidContextEntries();

            m_ClearContextButton?.UnregisterCallback<PointerUpEvent>(ClearContext);
            m_SelectedContextRoot.Clear();
            m_SelectedContextDropdown.ClearData();
            m_ContextDropdownButton.SetDisplay(false);
            m_ClearContextButton.SetDisplay(false);

            if (k_SelectedContext.Count > 0)
            {
                m_SelectedContextRoot.SetDisplay(true);
                m_SelectedContextDropdown.AddChoicesToDropdown(k_SelectedContext, this);
                if (k_SelectedContext.Count >= AssistantConstants.AttachedContextDisplayLimit)
                {
                    m_ContextDropdownButton.SetDisplay(true);
                    AddItemsNumberToLabel(k_SelectedContext.Count);
                }
                else
                {
                    if (m_SelectedContextDropdown.IsShown)
                    {
                        ToggleContextDropdown();
                    }

                    var entries = m_SelectedContextDropdown.GetEntries();
                    for (var i = 0; i < k_SelectedContext.Count; i++)
                    {
                        var newElement = new ContextElement();
                        newElement.Initialize(Context);
                        newElement.SetData(i, entries[i]);
                        newElement.SetOwner(this);
                        newElement.RemoveListStyles(i);
                        m_SelectedContextRoot.Add(newElement);
                    }
                }

                m_ClearContextButton.SetDisplay(true);
                m_ClearContextButton?.RegisterCallback<PointerUpEvent>(ClearContext);

                m_SelectedContextRoot.MarkDirtyRepaint();

            }
            else
            {
                m_SelectedContextRoot.SetDisplay(false);
            }

            UpdateSelectedContextWarning();
            UpdateAssistantEditorDriverContext();
        }

        public void OnRemoveContextEntry(AssistantContextEntry entry)
        {
            k_SelectedContext.Remove(entry);
            UpdateContextSelectionElements(true);
        }

        void UpdateSelectedContextWarning(MouseEnterEvent evt = null)
        {
            m_ChatInput.ToggleContextLimitWarning(Context.API.GetAttachedContextLength() > AssistantMessageSizeConstraints.ContextLimit);
        }

        internal void ClearContext(PointerUpEvent evt)
        {
            k_SelectedContext.Clear();

            Context.Blackboard.ClearAttachments();

            UpdateContextSelectionElements();

            AIAssistantAnalytics.ReportContextEvent(ContextSubType.ClearAllAttachedContext);
        }

        void UpdateAssistantEditorDriverContext()
        {
            Context.Blackboard.ClearAttachments();

            for (var i = 0; i < k_SelectedContext.Count; i++)
            {
                var entry = k_SelectedContext[i];
                switch (entry.EntryType)
                {
                    case AssistantContextType.ConsoleMessage:
                    {
                        Context.Blackboard.AddConsoleAttachment(entry.GetLogData());
                        break;
                    }

                    case AssistantContextType.Component:
                    {
                        Context.Blackboard.AddObjectAttachment(entry.GetComponent());
                        break;
                    }

                    case AssistantContextType.HierarchyObject:
                    case AssistantContextType.SubAsset:
                    case AssistantContextType.SceneObject:
                    {
                        Context.Blackboard.AddObjectAttachment(entry.GetTargetObject());
                        break;
                    }
                }
            }
        }
    }
}

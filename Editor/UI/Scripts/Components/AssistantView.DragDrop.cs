using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantView
    {
        VisualElement m_DropZoneRoot;
        ChatDropZone m_DropZone;

        void OnDropped(IEnumerable<object> obj)
        {
            bool anyAdded = false;

            foreach (object droppedObject in obj)
            {
                if (AddObjectToContext(droppedObject))
                {
                    anyAdded = true;
                }
            }

            if (anyAdded)
            {
                UpdateContextSelectionElements(true);
            }

            m_DropZone.SetDropZoneActive(false);
        }

        bool IsSupportedAsset(Object unityObject)
        {
            if (unityObject is DefaultAsset)
                return false;

            return true;
        }
    }
}

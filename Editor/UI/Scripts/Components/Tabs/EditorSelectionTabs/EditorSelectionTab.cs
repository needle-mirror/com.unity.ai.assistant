using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class EditorSelectionTab: SelectionPopupTab
    {
        internal enum SelectionType
        {
            UnityObject,
            Console
        }

        public abstract int Order { get; }
        public abstract SelectionType Type { get; }

        protected EditorSelectionTab(string label) : base(label)
        {
        }
    }
}

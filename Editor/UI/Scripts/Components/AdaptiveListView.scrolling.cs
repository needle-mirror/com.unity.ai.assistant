using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AdaptiveListView<TD, TV>
    {
        const int k_DelayedScrollActions = 2;
        const int k_ScrollEndThreshold = 5;

        ScrollState m_ScrollState = ScrollState.None;
        bool m_CheckForScrollLock;
        bool m_EnforcementQueued;
        int m_DelayedScrollActions;

        enum ScrollState
        {
            None,
            ScrollToEnd,
            Locked,
            ScrollToStart
        }

        public bool CanScrollDown => m_VerticalScroller.value < m_VerticalScroller.highValue;

        public void ScrollToStartIfNotLocked()
        {
            if (m_ScrollState == ScrollState.Locked)
            {
                return;
            }

            ChangeScrollState(ScrollState.ScrollToStart, true);
        }

        public void ScrollToEndIfNotLocked()
        {
            if (m_ScrollState == ScrollState.Locked)
            {
                return;
            }

            ChangeScrollState(ScrollState.ScrollToEnd, true);
        }

        public void ScrollToEnd()
        {
            ChangeScrollState(ScrollState.ScrollToEnd, true);
        }

        void ChangeScrollState(ScrollState newState, bool force = false)
        {
            if (!EnableAutoScroll)
            {
                return;
            }

            if (!force && m_ScrollState == newState)
            {
                return;
            }

            m_DelayedScrollActions = k_DelayedScrollActions;
            m_ScrollState = newState;
            QueueEnforceScrollState();
        }

        void QueueEnforceScrollState()
        {
            if (m_EnforcementQueued)
            {
                return;
            }

            m_EnforcementQueued = true;
            EditorApplication.delayCall += EnforceScrollState;
        }

        void EnforceScrollState()
        {
            if (k_Data.Count == 0 || !EnableAutoScroll)
            {
                return;
            }

            m_EnforcementQueued = false;
            if (!EnableVirtualization && k_VisualElements.Count < k_Data.Count)
            {
                // Not all elements are made yet, come back later
                QueueEnforceScrollState();
                return;
            }

            RefreshIfRequired();

            m_CheckForScrollLock = false;

            switch (m_ScrollState)
            {
                case ScrollState.ScrollToEnd:
                {
                    if (EnableVirtualization)
                    {
                        m_InnerList.ScrollToItem(k_Data.Count - 1);
                    }
                    else
                    {
                        m_InnerScroll.ScrollTo(k_VisualElements[^1]);
                    }

                    if (CanScrollDown)
                    {
                        m_VerticalScroller.value = m_VerticalScroller.highValue;
                    }

                    break;
                }

                case ScrollState.ScrollToStart:
                {
                    if (EnableVirtualization)
                    {
                        m_InnerList.ScrollToItem(0);
                    }
                    else
                    {
                        m_InnerScroll.ScrollTo(k_VisualElements[0]);
                    }

                    if (m_VerticalScroller.value > 0)
                    {
                        m_VerticalScroller.value = 0;
                    }

                    break;
                }
            }

            m_CheckForScrollLock = true;

            if (m_DelayedScrollActions > 0)
            {
                m_DelayedScrollActions--;
                QueueEnforceScrollState();
            }
        }

        void OnVerticallyScrolled(float newValue)
        {
            UserScrolled?.Invoke();

            if (!EnableScrollLock || !m_CheckForScrollLock)
            {
                return;
            }

            if (!m_CheckForScrollLock)
            {
                return;
            }

            if (newValue >= m_VerticalScroller.highValue - k_ScrollEndThreshold)
            {
                ChangeScrollState(ScrollState.ScrollToEnd);
                return;
            }

            if (newValue < m_VerticalScroller.highValue)
            {
                ChangeScrollState(ScrollState.Locked);
            }
        }
    }
}

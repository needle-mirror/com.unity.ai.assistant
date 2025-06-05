using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantTextField
    {
        void DetectRouteInputOnKeyDown(KeyDownEvent evt)
        {
            bool inputMatch = StringUtils.StartsWithAnyLinq(RemoveCommandStyling(m_ChatInput.value), k_RouteLabels);

            // allow Enter key for selection of route
            if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && m_RoutesPopup.IsShown &&
                inputMatch)
            {
                var selectedEntry = m_RoutesPopup.VisibleEntries[m_SelectedRouteItemIndex];
                if (selectedEntry.Command != null)
                {
                    m_RouteSelection = true;
                    RouteSelectionChanged(selectedEntry.Command.Command);
                    return;
                }
            }

            // Allow arrow key navigation of popup
            if (m_RoutesPopup.VisibleEntries.Count != 0)
            {
                if (evt.keyCode == KeyCode.UpArrow && m_RoutesPopup.IsShown)
                {
                    m_RoutesPopup.VisibleEntries[m_SelectedRouteItemIndex].SetHovered(false);
                    if (m_SelectedRouteItemIndex == 0)
                    {
                        m_SelectedRouteItemIndex = m_RoutesPopup.VisibleEntries.Count - 1;
                    }
                    else
                    {
                        m_SelectedRouteItemIndex--;
                    }

                    m_RoutesPopup.VisibleEntries[m_SelectedRouteItemIndex].SetHovered(true);
                    m_RoutesPopup.DisplayRoutes();
                    evt.StopImmediatePropagation();
                }

                if (evt.keyCode == KeyCode.DownArrow && m_RoutesPopup.IsShown)
                {
                    m_RoutesPopup.VisibleEntries[m_SelectedRouteItemIndex].SetHovered(false);
                    if (m_SelectedRouteItemIndex == m_RoutesPopup.VisibleEntries.Count - 1)
                    {
                        m_SelectedRouteItemIndex = 0;
                    }
                    else
                    {
                        m_SelectedRouteItemIndex++;
                    }

                    m_RoutesPopup.VisibleEntries[m_SelectedRouteItemIndex].SetHovered(true);
                    m_RoutesPopup.DisplayRoutes();
                    evt.StopImmediatePropagation();
                }
            }
        }
    }
}

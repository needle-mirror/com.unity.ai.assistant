using Unity.AI.Toolkit.Accounts.Manipulators;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.ServerCompatibility
{
    /// <summary>
    /// Sets the target's enabled state based on server version status.
    /// </summary>
    class AssistantStatusTracker : Manipulator
    {
        readonly object k_Lock = new();
        ServerCompatibility.CompatibilityStatus m_LastKnownServerStatus = ServerCompatibility.CompatibilityStatus.Undetermined;

        protected override void RegisterCallbacksOnTarget()
        {
            Account.sessionStatus.OnChange += Refresh;
            Account.session.OnChange += Refresh;
            ServerCompatibility.Bind(RefreshStatus);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            Account.sessionStatus.OnChange -= Refresh;
            Account.session.OnChange -= Refresh;
            ServerCompatibility.Unbind(RefreshStatus);
        }

        void Refresh()
        {
            lock (k_Lock)
            {
                UpdateEnabledState();
            }
        }

        void RefreshStatus(ServerCompatibility.CompatibilityStatus status)
        {
            lock (k_Lock)
            {
                m_LastKnownServerStatus = status;
                UpdateEnabledState();
            }
        }

        void UpdateEnabledState()
        {
            bool enabledState = m_LastKnownServerStatus != ServerCompatibility.CompatibilityStatus.Unsupported;
            if (!Account.sessionStatus.IsUsable || !Account.settings.AiAssistantEnabled)
            {
                // Account status overrides server status
                enabledState = false;
            }

            target?.SetEnabled(enabledState);
        }
    }
}

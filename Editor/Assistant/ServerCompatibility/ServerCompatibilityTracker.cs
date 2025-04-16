using Unity.AI.Toolkit.Accounts.Services;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.ServerCompatibility
{
    /// <summary>
    /// Sets the target's enabled state based on server version status.
    /// </summary>
    class ServerCompatibilityTracker : Manipulator
    {
        protected override void RegisterCallbacksOnTarget() => ServerCompatibility.Bind(Refresh);
        protected override void UnregisterCallbacksFromTarget() => ServerCompatibility.Unbind(Refresh);

        void Refresh(ServerCompatibility.CompatibilityStatus status)
        {
            // This is so that in cases where the session state disables the UI, the server compatibility state does not enable it
            if(!Account.sessionStatus.IsUsable)
                return;

            var usable = status != ServerCompatibility.CompatibilityStatus.Unsupported;
            target?.SetEnabled(usable);
        }
    }
}

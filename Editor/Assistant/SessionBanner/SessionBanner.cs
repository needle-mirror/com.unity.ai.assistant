using Unity.AI.Assistant.Editor.ServerCompatibility;
using Unity.AI.Toolkit.Accounts.Components;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.SessionBanner
{
    /// <summary>
    /// Session top banners.
    ///
    /// Acts as a state machine where `CurrentView` return the view that should currently be showed.
    /// </summary>
    [UxmlElement]
    partial class SessionBanner : AssistantSessionStatusBanner
    {
        public SessionBanner()
        {
            NotificationsState.instance.hideCompatibility = false;
            this.AddManipulator(new ServerCompatibilityChanges(Refresh));
        }

        protected override VisualElement CurrentView()
        {
            var view = base.CurrentView();  // Prioritize base session status views.
            if (view == null)
            {
                if(ServerCompatibility.ServerCompatibility.Status == ServerCompatibility.ServerCompatibility.CompatibilityStatus.Unsupported)
                    view = new ServerCompatibilityNotSupportedBanner();
                else if(ServerCompatibility.ServerCompatibility.Status == ServerCompatibility.ServerCompatibility.CompatibilityStatus.Deprecated &&
                    !NotificationsState.instance.hideCompatibility)
                    view = new ServerCompatibilityDeprecatedNotificationView(Dismiss);
            }

            EnableInClassList("empty", view == null);
            return view;
        }

        void Dismiss()
        {
            Clear();
            Refresh();
        }
    }
}

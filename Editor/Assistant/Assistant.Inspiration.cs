using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.ErrorHandling;
using Unity.AI.Assistant.Editor.Data;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        /// <summary>
        /// Indicates that the inspiration entries have changed
        /// </summary>
        public event Action<IEnumerable<AssistantInspiration>> InspirationsRefreshed;

        public async Task RefreshInspirations(CancellationToken ct = default)
        {
            BackendResult<IEnumerable<Inspiration>> inspirations = await m_Backend.InspirationRefresh(await GetCredentialsContext(ct), ct);

            if (inspirations.Status != BackendResult.ResultStatus.Success)
            {
                // Inspiration can fail silently. It's better just not to display them than spam error messages
                ErrorHandlingUtility.InternalLogBackendResult(inspirations);
                InspirationsRefreshed?.Invoke(Array.Empty<AssistantInspiration>());
                return;
            }

            InspirationsRefreshed?.Invoke(inspirations.Value.Select(inspiration => inspiration.ToInternal()));
        }

        // TODO: For times sake, we are pushing fixing routes for internal tools until later. Inspiration create and update fall into this category
        // public Task InspirationUpdate(MuseChatInspiration inspiration)
        // {
        //     var externalData = inspiration.ToExternal();
        //     return !inspiration.Id.IsValid
        //         ? m_Backend.InspirationCreate(externalData)
        //         : m_Backend.InspirationUpdate(externalData);
        // }
        //
        // public void InspirationDelete(MuseChatInspiration inspiration)
        // {
        //     if(!inspiration.Id.IsValid)
        //         throw new ArgumentException("Cannot delete non-synchronized inspiration");
        //
        //     m_Backend.InspirationDelete(inspiration.Id.Value);
        // }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Backend.Socket.ErrorHandling;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.Data;

namespace Unity.AI.Assistant.Editor
{
    partial class Assistant
    {
        /// <summary>
        /// Cache for the point cost based on command
        /// NOTE: later we will replace this if the point cost become more intricate, for now only command matters
        /// </summary>
        static readonly IDictionary<string, int> k_CommandPointCostCache = new Dictionary<string, int>();

        /// <summary>
        /// Indicates that a point cost request has been completed
        /// </summary>
        public event Action<PointCostRequestId, int> PointCostReceived;

        public async Task PointCostRequest(PointCostRequestId requestId, PointCostRequestData data, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(data.Prompt))
            {
                PointCostReceived?.Invoke(requestId, 0);
                return;
            }

            string promptCommand = AskCommand.k_CommandName;
            if(ChatCommandParser.Parse(data.Prompt, out var commandHandler))
            {
                promptCommand = commandHandler.Command;
            }

            if (string.IsNullOrEmpty(promptCommand))
            {
                // No command, assume zero points
                PointCostReceived?.Invoke(requestId, 0);
                return;
            }

            if (k_CommandPointCostCache.TryGetValue(promptCommand, out int value))
            {
                PointCostReceived?.Invoke(requestId, value);
                return;
            }

            var pointsResult = await m_Backend.PointCostRequest(await GetCredentialsContext(ct), requestId.ConversationId.Value, data.ContextItemCount == 0 ? null : data.ContextItemCount, data.Prompt, ct);

            // TODO: Failing silently on points failure. This might need a QOL pass to express this correctly to user.
            if(pointsResult.Status != BackendResult.ResultStatus.Success)
                ErrorHandlingUtility.InternalLogBackendResult(pointsResult);

            int points = pointsResult.Status == BackendResult.ResultStatus.Success ? pointsResult.Value : 0;
            k_CommandPointCostCache[promptCommand] = points;
            PointCostReceived?.Invoke(requestId, points);
        }
    }
}

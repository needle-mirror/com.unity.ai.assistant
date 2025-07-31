using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.CodeAnalyze;
using Unity.AI.Assistant.Editor.Agent;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat;
using Unity.AI.Assistant.Editor.CodeBlock;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        /// <summary>
        /// Run C# command dynamically with Roslyn
        /// </summary>
        public RunCommandInterpreter RunCommandInterpreter { get; } = new();

        /// <summary>
        /// Validator for generated script files
        /// </summary>
        public CodeBlockValidator CodeBlockValidator { get; } = new();

        public bool ValidateCode(string code, out string localFixedCode, out CompilationErrors compilationErrors)
        {
            return CodeBlockValidator.ValidateCode(code, out localFixedCode, out compilationErrors);
        }

        public AgentRunCommand BuildAgentRunCommand(string script, IEnumerable<Object> contextAttachments)
        {
            return RunCommandInterpreter.BuildRunCommand(script, contextAttachments);
        }

        public void RunAgentCommand(AssistantConversationId conversationId, AgentRunCommand command, string fencedTag)
        {
            command.Execute(out var executionResult);
            RunCommandInterpreter.StoreExecution(executionResult);

            if (m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                AddInternalMessage(conversation, $"```{fencedTag}\n{executionResult.Id}\n```", k_SystemRole, false);
            }
        }

        public async Task SendEditRunCommand(AssistantMessageId messageId, string updatedCode)
        {
            // get the appropriate workflow
            if (messageId.ConversationId.IsValid)
            {
                var workflow = m_Backend.GetOrCreateWorkflow(await GetCredentialsContext(), FunctionCaller, messageId.ConversationId);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(() =>
                {
                    workflow?.SendEditRunCommandRequest(messageId.FragmentId, updatedCode).WithExceptionLogging();
                });
#pragma warning restore CS4014
            }
        }

        public ExecutionResult GetRunCommandExecution(int executionId)
        {
            return RunCommandInterpreter.RetrieveExecution(executionId);
        }
    }
}

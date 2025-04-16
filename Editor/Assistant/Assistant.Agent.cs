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
using Unity.Muse.Agent.Dynamic;
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

        public AgentRunCommand BuildAgentRunCommand(string script, List<Object> contextAttachments)
        {
            return RunCommandInterpreter.BuildRunCommand(script, contextAttachments);
        }

        public void RunAgentCommand(AssistantConversationId conversationId, AgentRunCommand command, string fencedTag)
        {
            command.Execute(out var executionResult);
            RunCommandInterpreter.StoreExecution(executionResult);

            if (m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                AddInternalMessage(conversation, $"```{fencedTag}\n{executionResult.Id}\n```", k_SystemRole, false, author: RunCommand.k_CommandName);
            }
        }

        public Task SendEditRunCommand(AssistantMessageId messageId, string updatedCode)
        {
            // get the appropriate workflow
            ChatWorkflow workflow = null;
            if (messageId.ConversationId.IsValid)
            {
                var castBackend = m_Backend as AssistantWebSocketBackend;
                workflow = castBackend?.GetOrCreateWorkflow(FunctionCaller, messageId.ConversationId.Value);
            }
            else
                return Task.CompletedTask;

            workflow?.SendEditRunCommandRequest(messageId.FragmentId, updatedCode).WithExceptionLogging();

            return Task.CompletedTask;
        }

        public ExecutionResult GetRunCommandExecution(int executionId)
        {
            return RunCommandInterpreter.RetrieveExecution(executionId);
        }
    }
}

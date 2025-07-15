using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Unity.AI.Assistant.CodeAnalyze;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.Agent
{
    class AgentRunCommand
    {
        static readonly string[] k_UnauthorizedNamespaces = { "System.Net", "System.Diagnostics", "System.Runtime.InteropServices", "System.Reflection" };

        readonly List<ClassCodeTextDefinition> k_RequiredMonoBehaviours = new();

        IRunCommand m_ActionInstance;

        string m_Description;

        public string Script { get; set; }
        public CompilationErrors CompilationErrors { get; set; }

        public bool PreviewIsDone { get; set; }

        public bool Unsafe { get; set; }

        public string Description => m_Description;

        public IRunCommand Instance => m_ActionInstance;

        public bool CompilationSuccess => m_ActionInstance != null;

        public IEnumerable<ClassCodeTextDefinition> RequiredMonoBehaviours => k_RequiredMonoBehaviours;

        public readonly List<Object> CommandAttachments;

        public AgentRunCommand(List<Object> contextAttachments)
        {
            CommandAttachments = new List<Object>(AssistantInstance.instance.Value.GetValidAttachment(contextAttachments));
        }

        public void SetInstance(IRunCommand instance, string description)
        {
            m_ActionInstance = instance;
            m_Description = description;
        }

        public bool Execute(out ExecutionResult executionResult)
        {
            executionResult = new ExecutionResult(m_Description);

            if (m_ActionInstance == null)
                return false;

            executionResult.Start();

            try
            {
                m_ActionInstance.Execute(executionResult);

                // Unsafe actions usually mean deleting things - so we need to update the project view afterwards
                if (Unsafe)
                {
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                executionResult.LogError(e.ToString());
            }

            executionResult.End();

            return true;
        }

        public void BuildPreview(out PreviewBuilder builder)
        {
            builder = new PreviewBuilder();

            if (m_ActionInstance == null)
                return;

            PreviewIsDone = true;
            m_ActionInstance.BuildPreview(builder);
        }

        public void SetRequiredMonoBehaviours(IEnumerable<ClassCodeTextDefinition> newBehaviors)
        {
            k_RequiredMonoBehaviours.Clear();
            k_RequiredMonoBehaviours.AddRange(newBehaviors);
        }

        public bool HasUnauthorizedNamespaceUsage()
        {
            var tree = SyntaxFactory.ParseSyntaxTree(Script);
            return tree.ContainsNamespaces(k_UnauthorizedNamespaces);
        }

        public IEnumerable<Object> GetAttachments(Type type)
        {
            var isComponentType = typeof(Component).IsAssignableFrom(type);

            foreach (var obj in CommandAttachments)
            {
                if (isComponentType && obj is GameObject go)
                {
                    var comp = go.GetComponent(type);
                    if (comp != null)
                        yield return comp;
                }
                else
                {
                    if (type.IsAssignableFrom(obj.GetType()))
                        yield return obj;
                }
            }
        }

        public Object GetAttachmentByNameOrFirstCompatible(string objectName, Type type)
        {
            var filtered = GetAttachments(type);
            if (string.IsNullOrEmpty(objectName))
                return filtered.FirstOrDefault();

            var objectByName = filtered.FirstOrDefault(a => a.name == objectName);
            return objectByName;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.Agent
{
    class RunCommandInterpreter
    {
        internal const string k_DynamicAssemblyName = "Unity.AI.Assistant.Agent.Dynamic.Extension.Editor";
        internal const string k_DynamicCommandNamespace = "Unity.AI.Assistant.Agent.Dynamic.Extension.Editor";
        internal const string k_DynamicCommandClassName = "CommandScript";

        internal static readonly Regex k_CsxMarkupRegex = new("```csx(.*?)```", RegexOptions.Compiled | RegexOptions.Singleline);

        const string k_DynamicCommandFullClassName = k_DynamicCommandNamespace + "." + k_DynamicCommandClassName;

        const string k_DummyCommandScript =
            "\nusing UnityEngine;\nusing UnityEditor;\n\ninternal class CommandScript : IRunCommand\n{\n    public void Execute(ExecutionResult result) {}\n    public void Preview(PreviewBuilder builder) {}\n}";


        readonly DynamicAssemblyBuilder m_Builder = new(k_DynamicAssemblyName, k_DynamicCommandNamespace);
        Dictionary<int, ExecutionResult> m_CommandExecutions = new();

        static string[] k_UnsafeMethods = new[]
        {
            "UnityEditor.AssetDatabase.DeleteAsset",
            "UnityEditor.FileUtil.DeleteFileOrDirectory",
            "System.IO.File.Delete",
            "System.IO.Directory.Delete",
            "System.IO.File.Move",
            "System.IO.Directory.Move"
        };

        public RunCommandInterpreter()
        {
            Task.Run(InitCacheWithDummyCompilation);
        }

        void InitCacheWithDummyCompilation()
        {
            // To enable the internal assembly cache we start a compilation of an empty command
            m_Builder.Compile(k_DummyCommandScript, out _);
        }

        public AgentRunCommand BuildRunCommand(string commandScript, List<Object> contextAttachments)
        {
            commandScript = ScriptPreProcessor(commandScript, out var embeddedMonoBehaviours);

            using var stream = new MemoryStream();
            var compilationSuccessful = m_Builder.TryCompileCode(commandScript, stream, out var compilationLogs, out var updatedScript);
            var runCommand = new AgentRunCommand(contextAttachments) { CompilationErrors = compilationLogs, Script = updatedScript };

            if (runCommand.HasUnauthorizedNamespaceUsage())
            {
                compilationSuccessful = false;
            }

            if (compilationSuccessful)
            {
                var agentAssembly = m_Builder.LoadAssembly(stream);
                var commandInstance = CreateRunCommandInstance(agentAssembly, out var commandDescription);
                runCommand.SetInstance(commandInstance, commandDescription);

                if (commandInstance != null)
                {
                    InitializeFieldWithLookup(commandInstance, runCommand);

                    CheckForUnsafeCalls(updatedScript, runCommand);

                    // Save embedded MonoBehaviours list
                    runCommand.SetRequiredMonoBehaviours(embeddedMonoBehaviours);
                }
                else
                {
                    InternalLog.LogWarning($"Unable to find a valid CommandScript in the assembly");
                }
            }
            else
            {
                InternalLog.LogWarning($"Unable to compile the command:\n{compilationLogs}");
            }

            return runCommand;
        }

        static string ScriptPreProcessor(string commandScript, out List<ClassCodeTextDefinition> additionalScripts)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(commandScript);
            // TODO remove any constructors from CommandScript

            // Remove embedded MonoBehaviours that already exist in the project
            additionalScripts = tree.ExtractTypesByInheritance<MonoBehaviour>(out var usingDirectives).ChangeModifiersToPublic().ToCodeTextDefinition(usingDirectives);
            for (var i = additionalScripts.Count - 1; i >= 0; i--)
            {
                var monoBehaviour = additionalScripts[i];
                if (UserAssemblyContainsType(monoBehaviour.ClassName))
                {
                    commandScript = tree.RemoveType(monoBehaviour.ClassName).GetText().ToString();
                    additionalScripts.RemoveAt(i);
                }
            }

            return commandScript;
        }

        void CheckForUnsafeCalls(string commandScript, AgentRunCommand runCommand)
        {
            var compilation = m_Builder.Compile(commandScript, out var tree);
            var model = compilation.GetSemanticModel(tree);

            var root = tree.GetCompilationUnitRoot();
            var walker = new PublicMethodCallWalker(model);
            walker.Visit(root);

            foreach (var methodCall in walker.PublicMethodCalls)
            {
                if (k_UnsafeMethods.Contains(methodCall))
                {
                    runCommand.Unsafe = true;
                    break;
                }
            }
        }

         void InitializeFieldWithLookup(object instance, AgentRunCommand runCommand)
        {
            var type = instance.GetType();

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var fieldInfo in fields)
            {
                var actionParameterAttribute = fieldInfo.GetCustomAttribute<CommandParameterAttribute>();
                if (actionParameterAttribute == null)
                    continue;

                var fieldType = fieldInfo.FieldType;
                if (typeof(Object).IsAssignableFrom(fieldType))
                {
                    Object defaultValue = null;
                    switch (actionParameterAttribute.LookupType)
                    {
                        case LookupType.Attachment:
                            defaultValue = runCommand.GetAttachmentByNameOrFirstCompatible(actionParameterAttribute.LookupName, fieldType);
                            break;
                        case LookupType.Scene:
                            defaultValue = GameObject.Find(actionParameterAttribute.LookupName);
                            break;
                        case LookupType.Asset:
                            defaultValue = AssetDatabase.LoadAssetAtPath(actionParameterAttribute.LookupName, fieldType);
                            break;
                    }

                    fieldInfo.SetValue(instance, defaultValue);
                }
                else if (fieldType.IsGenericType &&
                         fieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                         typeof(Object).IsAssignableFrom(fieldType.GetGenericArguments()[0]))
                {
                    var elementType = fieldType.GetGenericArguments()[0];
                    var listInstance = Activator.CreateInstance(fieldType) as IList;

                    switch (actionParameterAttribute.LookupType)
                    {
                        case LookupType.Attachment:
                            var attachments = runCommand.GetAttachments(elementType);
                            foreach (var attachment in attachments)
                                listInstance?.Add(attachment);
                            break;
                        case LookupType.Asset:
                            listInstance = AssetDatabase.LoadAllAssetsAtPath(actionParameterAttribute.LookupName);
                            break;
                        case LookupType.Scene:
                            Debug.LogWarning("List of GameObject in the scene is not supported.");
                            break;
                    }

                    fieldInfo.SetValue(instance, listInstance);
                }
                else if (fieldType.IsArray &&
                         typeof(Object).IsAssignableFrom(fieldType.GetElementType()))
                {
                    var elementType = fieldType.GetElementType();
                    Object[] arrayInstance = null;

                    switch (actionParameterAttribute.LookupType)
                    {
                        case LookupType.Attachment:
                            var attachments = runCommand.GetAttachments(elementType);
                            arrayInstance = attachments.ToArray();
                            break;
                        case LookupType.Asset:
                            var assets = AssetDatabase.LoadAllAssetsAtPath(actionParameterAttribute.LookupName);
                            arrayInstance = assets.Where(asset => elementType != null && elementType.IsInstanceOfType(asset)).ToArray();
                            break;
                        case LookupType.Scene:
                            Debug.LogWarning("Array of GameObject in the scene is not supported.");
                            break;
                    }

                    if (arrayInstance != null)
                    {
                        var typedArray = Array.CreateInstance(elementType, arrayInstance.Length);
                        for (int i = 0; i < arrayInstance.Length; i++)
                            typedArray.SetValue(arrayInstance[i], i);

                        fieldInfo.SetValue(instance, typedArray);
                    }
                }
            }
        }

        IRunCommand CreateRunCommandInstance(Assembly dynamicAssembly, out string commandDescription)
        {
            var type = dynamicAssembly.GetType(k_DynamicCommandFullClassName);
            if (type == null)
            {
                commandDescription = null;
                return null;
            }

            var attribute = type.GetCustomAttribute<CommandDescriptionAttribute>();
            commandDescription = attribute?.Description;

            return Activator.CreateInstance(type) as IRunCommand;
        }

        public void StoreExecution(ExecutionResult executionResult)
        {
            m_CommandExecutions.Add(executionResult.Id, executionResult);
        }

        public ExecutionResult RetrieveExecution(int id)
        {
            return m_CommandExecutions.GetValueOrDefault(id);
        }

        static bool UserAssemblyContainsType(string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblyCSharp = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (assemblyCSharp != null)
            {
                var type = assemblyCSharp.GetType(typeName);
                return type != null;
            }

            return false;
        }
    }
}

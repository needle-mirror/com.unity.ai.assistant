using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
    struct ExecutionLog
    {
        public string Log;
        public LogType LogType;
        public object[] LoggedObjects;
        public string[] LoggedObjectNames;

        public ExecutionLog(string formattedLog, LogType logType, object[] loggedObjects = null)
        {
            Log = formattedLog;
            LogType = logType;
            LoggedObjects = loggedObjects;
            LoggedObjectNames = loggedObjects != null ? new string[loggedObjects.Length] : null;

            if (LoggedObjectNames != null)
            {
                for (int i = 0; i < loggedObjects.Length; i++)
                {
                    LoggedObjectNames[i] = loggedObjects[i] is Object obj ? obj.name : loggedObjects[i]?.ToString();
                }
            }
        }
    }

#if CODE_LIBRARY_INSTALLED
    public
#else
    internal
#endif
    class ExecutionResult
    {
        internal static readonly string LinkTextColor = EditorGUIUtility.isProSkin ? "#8facef" : "#055b9f";
        internal static readonly string WarningTextColor = EditorGUIUtility.isProSkin ? "#DFB33D" : "#B76300";

        static int k_NextExecutionId = 1;

        public static readonly Regex PlaceholderRegex = new(@"%(\d+)", RegexOptions.Compiled);

        List<ExecutionLog> m_Logs = new();
        string m_ConsoleLogs;
        int UndoGroup;

        public readonly int Id;
        public readonly string CommandName;

        internal List<ExecutionLog> Logs => m_Logs;
        public string ConsoleLogs => m_ConsoleLogs;
        public bool SuccessfullyStarted { get; private set; }

        public ExecutionResult(string commandName)
        {
            Id = k_NextExecutionId++;
            CommandName = commandName;
        }

        public void RegisterObjectCreation(Object objectCreated)
        {
            if (objectCreated != null)
                Undo.RegisterCreatedObjectUndo(objectCreated, $"{objectCreated.name} was created");
        }

        public void RegisterObjectCreation(Component component)
        {
            if (component != null)
                Undo.RegisterCreatedObjectUndo(component, $"{component} was attached to {component.gameObject.name}");
        }

        public void RegisterObjectModification(Object objectToRegister, string operationDescription = "")
        {
            if (!string.IsNullOrEmpty(operationDescription))
                Undo.RecordObject(objectToRegister, operationDescription);
            else
                Undo.RegisterCompleteObjectUndo(objectToRegister, $"{objectToRegister.name} was modified");
        }

        public void DestroyObject(Object objectToDestroy)
        {
            if (EditorUtility.IsPersistent(objectToDestroy))
            {
                var path = AssetDatabase.GetAssetPath(objectToDestroy);
                AssetDatabase.DeleteAsset(path);
            }
            else
            {
                if (!EditorApplication.isPlaying)
                    Undo.DestroyObjectImmediate(objectToDestroy);
                else
                    Object.Destroy(objectToDestroy);
            }
        }

        public void Start()
        {
            SuccessfullyStarted = true;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(CommandName ?? "Run command execution");
            UndoGroup = Undo.GetCurrentGroup();

            Application.logMessageReceived += HandleConsoleLog;
        }

        public void End()
        {
            Application.logMessageReceived -= HandleConsoleLog;

            Undo.CollapseUndoOperations(UndoGroup);
        }

        public void Log(string log, params object[] references)
        {
            m_Logs.Add(new ExecutionLog(log, LogType.Log, references));
        }

        public void LogWarning(string log, params object[] references)
        {
            m_Logs.Add(new ExecutionLog(log, LogType.Warning, references));
        }

        public void LogError(string log, params object[] references)
        {
            m_Logs.Add(new ExecutionLog(log, LogType.Error, references));
        }

        void HandleConsoleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            {
                m_ConsoleLogs += $"{type}: {logString}\n";
            }
        }

        public List<string> GetFormattedLogs()
        {
            List<string> formattedLogs = new();

            if (m_Logs == null)
            {
                return formattedLogs;
            }

            foreach (var content in m_Logs)
            {
                if (string.IsNullOrEmpty(content.Log))
                {
                    continue;
                }

                string logTemplate = content.Log;
                var references = content.LoggedObjects;

                string formattedLog = PlaceholderRegex.Replace(logTemplate, match =>
                {
                    if (int.TryParse(match.Groups[1].Value, out int index))
                    {
                        if (references != null && index >= 0 && index < references.Length)
                        {
                            return references[index]?.ToString() ?? string.Empty;
                        }
                    }

                    return match.Value;
                });

                formattedLogs.Add(formattedLog);
            }
            return formattedLogs;
        }
    }
}

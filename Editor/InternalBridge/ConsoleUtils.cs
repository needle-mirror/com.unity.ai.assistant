using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Bridge.Editor
{
    internal static class ConsoleUtils
    {
        static FieldInfo s_ConsoleListViewField;
        static ConsoleWindow s_ConsoleWindow;
        static ListViewState s_ConsoleWindowListViewState;
        static readonly LogEntry s_Entry = new();

        /// <summary>
        /// Returns the console window's ListViewState field info.
        /// </summary>
        /// <remarks>this method is internal for testing purposes</remarks>
        internal static FieldInfo GetConsoleWindowSelectionState()
        {
            if ((s_ConsoleListViewField ??= typeof(ConsoleWindow)
                    .GetField("m_ListView", BindingFlags.Instance | BindingFlags.NonPublic)) == null)
                return null;
            return s_ConsoleListViewField;
        }

        internal static T GetOpenWindow<T>() where T : EditorWindow
        {
            T[] windows = Resources.FindObjectsOfTypeAll<T>();
            if (windows != null && windows.Length > 0)
            {
                return windows[0];
            }
            return null;
        }

        internal static int GetSelectedConsoleLogs(List<LogData> results)
        {
            results.Clear();
            var currentConsoleWindow = GetOpenWindow<ConsoleWindow>();
            // null if no console window can be found
            if (!ReferenceEquals(currentConsoleWindow, s_ConsoleWindow))
            {
                s_ConsoleWindow = currentConsoleWindow;
                if (s_ConsoleWindow == null)
                    return 0;
                var consoleWindowSelectionState = GetConsoleWindowSelectionState();
                s_ConsoleWindowListViewState = consoleWindowSelectionState.GetValue(s_ConsoleWindow) as ListViewState;
            }
            else
            {
                if (s_ConsoleWindow == null)
                    return 0;
            }

            // null if the m_ListView private field has been renamed or its type has changed</returns>
            if (s_ConsoleWindowListViewState == null)
                return 0;
            // no array allocation in any case.
            // true if the console window row with the same index is selected.
            bool[] selectedRows = s_ConsoleWindowListViewState.selectedItems;
            int resultCount = 0;
            if (selectedRows == null)
            {
                results.Clear();
                return 0;
            }

            for (int i = 0; i < selectedRows.Length; i++)
            {
                if (!selectedRows[i])
                    continue;
                if (LogEntries.GetEntryInternal(i, s_Entry))
                {
                    results.Add(LogEntryToInternal(s_Entry));
                    resultCount++;
                }
            }

            return resultCount;
        }

        internal static int GetConsoleLogs(List<LogData> results)
        {
            results.Clear();

            int entryCount = LogEntries.GetCount();

            for (int i = 0; i < entryCount; i++)
            {
                if (LogEntries.GetEntryInternal(i, s_Entry))
                {
                    var entryToAdd = LogEntryToInternal(s_Entry);
                    // Avoid duplicate entries:
                    if (FindLogEntry(results, entryToAdd) >= 0)
                    {
                        continue;
                    }

                    results.Add(entryToAdd);
                }
            }
            return results.Count;
        }

        internal static int FindLogEntry(List<LogData> entries, LogData entry)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var l = entries[i];
                if (l.Message.Equals(entry.Message) && l.Type == entry.Type)
                {
                    return i;
                }
            }

            return -1;
        }

        internal static bool HasEqualLogEntry(List<LogData> entries, LogData entry)
        {
            foreach (var l in entries)
            {
                if (l.Equals(entry))
                {
                    return true;
                }
            }

            return false;
        }

        static LogData LogEntryToInternal(LogEntry entry)
        {
            var internalEntry = new LogData
            {
                Message = entry.message,
                File = entry.file,
                Line = entry.line,
                Column = entry.column
            };

            var mode = (ConsoleWindow.Mode) entry.mode;
            if ((mode & (ConsoleWindow.Mode.Error | ConsoleWindow.Mode.Assert |
                                                   ConsoleWindow.Mode.Fatal | ConsoleWindow.Mode.AssetImportError |
                                                   ConsoleWindow.Mode.ScriptingError |
                                                   ConsoleWindow.Mode.ScriptCompileError |
                                                   ConsoleWindow.Mode.ScriptingException |
                                                   ConsoleWindow.Mode.GraphCompileError |
                                                   ConsoleWindow.Mode.ScriptingAssertion |
                                                   ConsoleWindow.Mode.StickyError | ConsoleWindow.Mode.ReportBug |
                                                   ConsoleWindow.Mode.DisplayPreviousErrorInStatusBar |
                                                   ConsoleWindow.Mode.VisualScriptingError
                                                   )) != 0)
            {
                internalEntry.Type = LogDataType.Error;
            }
            else if ((mode & (ConsoleWindow.Mode.AssetImportWarning |
                              ConsoleWindow.Mode.ScriptingWarning |
                              ConsoleWindow.Mode.ScriptCompileWarning)) != 0)
            {
                internalEntry.Type = LogDataType.Warning;
            }
            else
            {
                internalEntry.Type = LogDataType.Info;
            }

            return internalEntry;
        }
    }
}

// ============================================================
//  TerminalEditorTools.cs
//  Unity Editor window for:
//   - Creating/editing files and folders in the filesystem
//   - Assigning events to files/directories
//   - Previewing terminal output
//   - Testing commands
//   - Debugging parser behaviour
// ============================================================
// Place this file in: Assets/Scripts/TerminalHorror/Editor/
// ============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TerminalHorror.Core;
using TerminalHorror.Commands;
using TerminalHorror.Filesystem;

namespace TerminalHorror.Editor
{
    public class TerminalEditorWindow : EditorWindow
    {
        private enum Tab { Commands, Filesystem, Preview, Debug }
        private Tab _tab = Tab.Commands;

        // Command tester
        private string _testInput = "";
        private string _testOutput = "";

        // Filesystem editor
        private string _newFileName      = "file.txt";
        private string _newFileContent   = "Enter content here.";
        private FileType _newFileType    = FileType.Text;
        private string _newDirName       = "new_folder";
        private string _newEventId       = "";
        private bool _showHiddenFiles    = true;

        // Preview
        private string _previewInput     = "ls";

        // Debug
        private string _parserInput      = "";
        private string _parserOutput     = "";

        private Vector2 _outputScroll;
        private Vector2 _fsScroll;

        // ─── Open window ─────────────────────────────────────

        [MenuItem("Terminal Horror/Editor Tools")]
        public static void ShowWindow()
        {
            GetWindow<TerminalEditorWindow>("Terminal Horror Dev Tools");
        }

        // ─── GUI ─────────────────────────────────────────────

        private void OnGUI()
        {
            DrawHeader();

            _tab = (Tab)GUILayout.Toolbar((int)_tab,
                new[] { "Commands", "Filesystem", "Preview", "Debug" });

            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case Tab.Commands:   DrawCommandsTab();   break;
                case Tab.Filesystem: DrawFilesystemTab(); break;
                case Tab.Preview:    DrawPreviewTab();    break;
                case Tab.Debug:      DrawDebugTab();      break;
            }
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 14,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("⬛ TERMINAL HORROR — DEV TOOLS", style);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // ─── Commands tab ─────────────────────────────────────

        private void DrawCommandsTab()
        {
            EditorGUILayout.LabelField("Command Tester", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Test a command as if typed by the player. " +
                "Only works in Play Mode (requires TerminalManager).",
                MessageType.Info);

            _testInput = EditorGUILayout.TextField("Command", _testInput);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Execute Command"))
            {
                var tm = FindFirstObjectByType<TerminalManager>();
                if (tm != null)
                {
                    tm.InjectCommand(_testInput);
                    _testOutput = $"Injected: '{_testInput}'";
                }
                else
                {
                    _testOutput = "ERROR: TerminalManager not found in scene.";
                }
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_testOutput))
            {
                EditorGUILayout.HelpBox(_testOutput, MessageType.None);
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Registered Commands (Play Mode)", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("List All Commands"))
            {
                var tm = FindFirstObjectByType<TerminalManager>();
                if (tm != null)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var cmd in tm.Registry.AllCommands())
                        sb.AppendLine($"  {cmd.Verb,-14} — {cmd.Description}");
                    _testOutput = sb.ToString();
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        // ─── Filesystem tab ──────────────────────────────────

        private void DrawFilesystemTab()
        {
            EditorGUILayout.LabelField("Runtime Filesystem Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adds/removes nodes in the live filesystem. Play Mode only.",
                MessageType.Info);

            _showHiddenFiles = EditorGUILayout.Toggle("Show Hidden", _showHiddenFiles);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Add File to Current Directory", EditorStyles.boldLabel);

            _newFileName    = EditorGUILayout.TextField("File Name",    _newFileName);
            _newFileType    = (FileType)EditorGUILayout.EnumPopup("Type", _newFileType);
            _newEventId     = EditorGUILayout.TextField("onReadEventId", _newEventId);
            EditorGUILayout.LabelField("Content:");
            _newFileContent = EditorGUILayout.TextArea(_newFileContent, GUILayout.Height(80));

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Add File to Current Dir"))
            {
                var tm = FindFirstObjectByType<TerminalManager>();
                if (tm != null)
                {
                    var file = new VirtualFile(_newFileName, _newFileType);
                    file.SetContent(_newFileContent);
                    file.onReadEventId = _newEventId;
                    tm.FileSystem.CurrentDirectory.AddFile(file);
                    Debug.Log($"[EditorTools] Added file '{_newFileName}'");
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Add Subdirectory to Current Directory", EditorStyles.boldLabel);
            _newDirName = EditorGUILayout.TextField("Dir Name", _newDirName);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Add Directory"))
            {
                var tm = FindFirstObjectByType<TerminalManager>();
                if (tm != null)
                {
                    var dir = new VirtualDirectory(_newDirName);
                    tm.FileSystem.CurrentDirectory.AddSubDir(dir);
                    Debug.Log($"[EditorTools] Added dir '{_newDirName}'");
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Current Directory Listing (Play Mode)", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Refresh Listing"))
            {
                var tm = FindFirstObjectByType<TerminalManager>();
                if (tm != null)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Path: {tm.FileSystem.GetCurrentPath()}");
                    foreach (var node in tm.FileSystem.CurrentDirectory.ListAll(_showHiddenFiles))
                    {
                        string type = node is VirtualDirectory ? "[DIR] " : "[FILE]";
                        string hidden = node.IsHidden ? " (hidden)" : "";
                        sb.AppendLine($"  {type} {node.name}{hidden}");
                    }
                    _testOutput = sb.ToString();
                }
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_testOutput))
            {
                _fsScroll = EditorGUILayout.BeginScrollView(_fsScroll, GUILayout.Height(150));
                EditorGUILayout.TextArea(_testOutput, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        // ─── Preview tab ──────────────────────────────────────

        private void DrawPreviewTab()
        {
            EditorGUILayout.LabelField("Terminal Output Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Inject a line directly into the terminal display without parsing it as a command.",
                MessageType.Info);

            _previewInput = EditorGUILayout.TextField("Text to Print", _previewInput);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Print to Terminal"))
            {
                var tm = FindFirstObjectByType<TerminalManager>();
                tm?.PrintDirect(_previewInput);
            }
            if (GUILayout.Button("Print as Typewriter"))
            {
                var tm = FindFirstObjectByType<TerminalManager>();
                tm?.terminalUI?.PrintTypewriter(new List<string> { _previewInput });
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Trigger Named Event (Play Mode)", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Fire Event: evt_hint_request"))
            {
                var es = FindFirstObjectByType<Events.TerminalEventSystem>();
                es?.TriggerEvent("evt_hint_request");
            }
            if (GUILayout.Button("Trigger Fake Crash"))
            {
                var c = FindFirstObjectByType<Corruption.CorruptionSystem>();
                c?.TriggerFakeReboot();
            }
            EditorGUI.EndDisabledGroup();
        }

        // ─── Debug tab ────────────────────────────────────────

        private void DrawDebugTab()
        {
            EditorGUILayout.LabelField("Parser Debug", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Parse a command string and inspect tokens. Works outside Play Mode.",
                MessageType.Info);

            _parserInput = EditorGUILayout.TextField("Input", _parserInput);

            if (GUILayout.Button("Parse"))
            {
                var parsed = CommandParser.Parse(_parserInput);
                var sb     = new System.Text.StringBuilder();
                sb.AppendLine($"Verb:  {parsed.verb}");
                sb.AppendLine($"Args:  [{string.Join(", ", parsed.args)}]");
                sb.AppendLine($"Flags: [{string.Join(", ", parsed.flags)}]");
                _parserOutput = sb.ToString();
            }

            if (!string.IsNullOrEmpty(_parserOutput))
            {
                EditorGUILayout.HelpBox(_parserOutput, MessageType.None);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Corruption Controls (Play Mode)", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            if (GUILayout.Button("Set Intensity: 0%"))
                SetCorruption(0f);
            if (GUILayout.Button("Set Intensity: 50%"))
                SetCorruption(0.5f);
            if (GUILayout.Button("Set Intensity: 100%"))
                SetCorruption(1f);

            EditorGUI.EndDisabledGroup();
        }

        private void SetCorruption(float value)
        {
            var c = FindFirstObjectByType<Corruption.CorruptionSystem>();
            c?.SetIntensity(value);
        }
    }
}
#endif
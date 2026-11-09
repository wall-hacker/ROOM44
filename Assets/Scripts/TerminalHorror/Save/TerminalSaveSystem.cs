// ============================================================
//  SaveSystem.cs
//  JSON-based save/load for terminal game state.
//  Saves: filesystem mutations, discovered files,
//  triggered events (fire counts), command history,
//  corruption state, narrative flags, player progress.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TerminalHorror.Filesystem;

namespace TerminalHorror.Save
{
    // ─────────────────────────────────────────────────────────
    //  SAVE DATA  (serialisable DTO)
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class FileSaveData
    {
        public string path;
        public string name;
        public FileType fileType;
        public List<string> contentLines = new();
        public int revealedLineCount;
        public string permissions;
        public bool wasAdded; // true = was added by event (not in original def)
    }

    [Serializable]
    public class TerminalSaveData
    {
        public string version = "1.0";
        public string saveTimestamp;

        // Filesystem
        public List<FileSaveData> addedFiles    = new();
        public List<string>       removedFiles  = new();   // full paths
        public List<string>       modifiedFiles = new();   // full paths of files w/ changed content

        // All content changes (keyed by full path)
        public List<FileSaveData> fileContentSnapshots = new();

        // Player progress
        public List<string> discoveredFilePaths = new();
        public List<string> setFlags            = new();

        // Event fire counts
        public List<string> eventIds       = new();
        public List<int>    eventFireCounts = new();

        // Command history
        public List<string> commandHistory = new();

        // Corruption
        public float corruptionIntensity;

        // Navigation
        public string currentPath;
    }

    // ─────────────────────────────────────────────────────────
    //  SAVE SYSTEM  (MonoBehaviour)
    // ─────────────────────────────────────────────────────────

    public class TerminalSaveSystem : MonoBehaviour
    {
        [Header("Settings")]
        public string saveFileName = "terminal_save.json";

        [Tooltip("If true, auto-saves every AutoSaveInterval seconds.")]
        public bool autoSave = true;
        public float autoSaveInterval = 60f;

        private Core.TerminalManager     _terminal;
        private Events.TerminalEventSystem _events;
        private Corruption.CorruptionSystem _corruption;

        private float _autoSaveTimer;

        private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        // ─── Lifecycle ──────────────────────────────────────

        private void Awake()
        {
            _terminal   = GetComponent<Core.TerminalManager>()
                          ?? FindFirstObjectByType<Core.TerminalManager>();
            _events     = GetComponent<Events.TerminalEventSystem>()
                          ?? FindFirstObjectByType<Events.TerminalEventSystem>();
            _corruption = GetComponent<Corruption.CorruptionSystem>()
                          ?? FindFirstObjectByType<Corruption.CorruptionSystem>();
        }

        private void Update()
        {
            if (!autoSave) return;
            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                Save();
            }
        }

        // ─── Public API ──────────────────────────────────────

        public void Save()
        {
            if (_terminal == null)
            {
                Debug.LogWarning("[TerminalSaveSystem] TerminalManager not found.");
                return;
            }

            var data = BuildSaveData();
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[TerminalSaveSystem] Saved to {SavePath}");
        }

        public bool Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[TerminalSaveSystem] No save file found.");
                return false;
            }

            string json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<TerminalSaveData>(json);

            if (data == null)
            {
                Debug.LogWarning("[TerminalSaveSystem] Failed to parse save data.");
                return false;
            }

            ApplySaveData(data);
            Debug.Log($"[TerminalSaveSystem] Loaded from {SavePath}");
            return true;
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }

        public bool SaveExists() => File.Exists(SavePath);

        // ─── Build / Apply ────────────────────────────────────

        private TerminalSaveData BuildSaveData()
        {
            var data = new TerminalSaveData
            {
                saveTimestamp = DateTime.Now.ToString("o"),
                currentPath   = _terminal.FileSystem.GetCurrentPath(),
                corruptionIntensity = _corruption?.currentIntensity ?? 0f,
            };

            // Command history
            data.commandHistory.AddRange(_terminal.CommandHistory);

            // Event fire counts (via reflection on the private dict)
            // We expose them via a helper on TerminalEventSystem.
            var counts = _events?.GetFireCounts();
            if (counts != null)
            {
                foreach (var kv in counts)
                {
                    data.eventIds.Add(kv.Key);
                    data.eventFireCounts.Add(kv.Value);
                }
            }

            // Flags
            var flags = _events?.GetFlags();
            if (flags != null) data.setFlags.AddRange(flags);

            // Snapshot all file contents that differ from defaults
            SnapshotFilesystem(_terminal.FileSystem.Root, data);

            return data;
        }

        private void SnapshotFilesystem(VirtualDirectory dir, TerminalSaveData data)
        {
            foreach (var file in dir.files)
            {
                data.fileContentSnapshots.Add(new FileSaveData
                {
                    path             = file.FullPath,
                    name             = file.name,
                    fileType         = file.fileType,
                    contentLines     = new List<string>(file.contentLines),
                    revealedLineCount = file.revealedLineCount,
                    permissions      = file.permissions.ToString()
                });
            }

            foreach (var sub in dir.subDirectories)
                SnapshotFilesystem(sub, data);
        }

        private void ApplySaveData(TerminalSaveData data)
        {
            // Corruption
            if (_corruption != null)
                _corruption.SetIntensity(data.corruptionIntensity);

            // Flags
            if (_events != null)
            {
                foreach (var f in data.setFlags)
                    _events.SetFlag(f);
            }

            // Navigate to saved path
            _terminal.FileSystem.ChangeDirectory(data.currentPath);

            // Restore file content snapshots
            foreach (var snap in data.fileContentSnapshots)
            {
                var file = _terminal.FileSystem.ResolveFile(snap.path);
                if (file != null)
                {
                    file.contentLines     = new List<string>(snap.contentLines);
                    file.revealedLineCount = snap.revealedLineCount;
                    file.fileType         = snap.fileType;
                }
            }

            // Command history — inject back into terminal
            // (CommandHistory is read-only; TerminalManager exposes InjectHistoryLines)
            _terminal.InjectHistoryLines(data.commandHistory);
        }
    }
}
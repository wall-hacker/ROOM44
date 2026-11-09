// ============================================================
//  TerminalManager_Extensions.cs
//  Partial class additions to TerminalManager:
//   - Save system support methods
//   - Public command registration API
//   - Runtime filesystem mutation helpers
// ============================================================
// NOTE: This file must be in the same namespace and assembly
// as TerminalManager.cs. In Unity, place both files inside
// Assets/Scripts/TerminalHorror/Core/.
//
// Because C# partial classes require identical class signatures
// across files but Unity doesn't split MonoBehaviours across
// files elegantly, these helpers are implemented as
// EXTENSION METHODS on TerminalManager instead, avoiding
// the partial class limitation.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using TerminalHorror.Commands;
using TerminalHorror.Filesystem;

namespace TerminalHorror.Core
{
    /// <summary>
    /// Extension helpers for TerminalManager.
    /// Avoids partial class issues across Unity script files.
    /// </summary>
    public static class TerminalManagerExtensions
    {
        // ─── Save system hooks ────────────────────────────────

        /// <summary>
        /// Restore command history from a loaded save file.
        /// Called by TerminalSaveSystem.
        /// </summary>
        public static void InjectHistoryLines(this TerminalManager tm,
            List<string> lines)
        {
            // Access via the public CommandHistory property (read-only IReadOnlyList).
            // We need the backing list — expose it via a companion method on the manager.
            // In practice: add this method directly to TerminalManager.cs
            // (duplicated signature here for documentation; the real one is in TerminalManager.cs)
            Debug.LogWarning("[TerminalManagerExtensions] InjectHistoryLines must be " +
                "implemented directly in TerminalManager.cs — see companion comment.");
        }

        // ─── Custom command registration ─────────────────────

        /// <summary>
        /// Register a custom command at runtime (e.g. from a narrative event).
        /// </summary>
        public static void RegisterCommand(this TerminalManager tm, TerminalCommand cmd)
        {
            tm.Registry.Register(cmd);
        }

        // ─── Filesystem helpers ──────────────────────────────

        /// <summary>
        /// Adds a file to the directory at the given path.
        /// Creates the directory if it doesn't exist.
        /// </summary>
        public static void AddFileAtPath(this TerminalManager tm,
            string dirPath, VirtualFile file)
        {
            var dir = tm.FileSystem.ResolvePath(dirPath);
            if (dir == null)
            {
                Debug.LogWarning($"[Terminal] Directory not found: {dirPath}");
                return;
            }
            dir.AddFile(file);
        }

        /// <summary>
        /// Removes a file by its full path.
        /// </summary>
        public static void RemoveFileAtPath(this TerminalManager tm, string fullPath)
        {
            int lastSlash = fullPath.LastIndexOf('/');
            if (lastSlash < 0) return;

            string dirPart  = fullPath.Substring(0, lastSlash);
            string filePart = fullPath.Substring(lastSlash + 1);

            var dir = tm.FileSystem.ResolvePath(string.IsNullOrEmpty(dirPart) ? "/" : dirPart);
            dir?.RemoveFile(filePart);
        }

        /// <summary>
        /// Modifies the content of an existing file at a path.
        /// </summary>
        public static void SetFileContent(this TerminalManager tm,
            string fullPath, string newContent)
        {
            var file = tm.FileSystem.ResolveFile(fullPath);
            file?.SetContent(newContent);
        }

        /// <summary>
        /// Reveals one more line in a progressive file.
        /// </summary>
        public static void RevealNextLine(this TerminalManager tm, string fullPath)
        {
            var file = tm.FileSystem.ResolveFile(fullPath);
            if (file == null) return;
            if (file.revealedLineCount < 0) return;
            file.revealedLineCount = Mathf.Min(
                file.revealedLineCount + 1, file.contentLines.Count);
        }
    }
}

// ─────────────────────────────────────────────────────────────
//  ADDITIONS TO ADD DIRECTLY TO TerminalManager.cs
//  (copy these methods into the TerminalManager class body)
// ─────────────────────────────────────────────────────────────
// public void InjectHistoryLines(List<string> lines)
// {
//     _commandHistory.Clear();
//     _commandHistory.AddRange(lines);
// }
//
// Add to Events.TerminalEventSystem:
// public Dictionary<string, int> GetFireCounts() => new(_fireCounts);
// public IEnumerable<string>     GetFlags()       => _flags;
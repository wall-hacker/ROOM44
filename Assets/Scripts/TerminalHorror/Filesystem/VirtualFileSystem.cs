// ============================================================
//  VirtualFileSystem.cs
//  Core data layer for the terminal's virtual filesystem.
//  Supports: directories, files, hidden/locked nodes,
//  metadata, timestamps, permissions, and runtime mutation.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerminalHorror.ScriptableObjects;

namespace TerminalHorror.Filesystem
{
    // ─────────────────────────────────────────────────────────
    //  ENUMS
    // ─────────────────────────────────────────────────────────

    public enum FileType
    {
        Text,
        CorruptedText,
        EncodedText,
        Image,
        Audio,
        Video,
        ZipArchive,
        EventTrigger,
        Directory
    }

    [Flags]
    public enum FilePermission
    {
        None      = 0,
        Read      = 1,
        Write     = 2,
        Execute   = 4,
        Hidden    = 8,
        Locked    = 16,
        System    = 32
    }

    // ─────────────────────────────────────────────────────────
    //  VIRTUAL NODE  (base for files and directories)
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class VirtualNode
    {
        public string name;
        public FilePermission permissions = FilePermission.Read;
        public string createdAt;
        public string modifiedAt;
        public string owner = "root";
        public string group = "root";

        // Runtime parent reference (not serialised)
        [NonSerialized] public VirtualDirectory parent;

        public bool IsHidden   => (permissions & FilePermission.Hidden)  != 0;
        public bool IsLocked   => (permissions & FilePermission.Locked)  != 0;
        public bool IsReadable => (permissions & FilePermission.Read)    != 0;
        public bool IsSystem   => (permissions & FilePermission.System)  != 0;

        public string FullPath
        {
            get
            {
                if (parent == null) return "/" + name;
                string parentPath = parent.FullPath;
                return parentPath == "/" ? "/" + name : parentPath + "/" + name;
            }
        }

        public void Touch()
        {
            modifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        protected VirtualNode(string nodeName)
        {
            name       = nodeName;
            createdAt  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            modifiedAt = createdAt;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  VIRTUAL FILE
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class VirtualFile : VirtualNode
    {
        public FileType fileType = FileType.Text;

        /// <summary>
        /// Raw content lines. Use \n to separate paragraphs.
        /// Corrupted / encoded files may store base-64 or glitch strings here.
        /// </summary>
        public List<string> contentLines = new();

        /// <summary>
        /// Optional narrative event ID to fire when this file is read.
        /// </summary>
        public string onReadEventId;

        /// <summary>
        /// If true, content is revealed progressively (typewriter).
        /// </summary>
        public bool progressiveReveal = false;

        /// <summary>
        /// How many lines have been unlocked via narrative progression.
        /// -1 = all lines available.
        /// </summary>
        public int revealedLineCount = -1;

        public VirtualFile(string fileName, FileType type = FileType.Text) : base(fileName)
        {
            fileType = type;
            permissions = FilePermission.Read;
        }

        public string GetContent()
        {
            if (!IsReadable) return "[PERMISSION DENIED]";

            IEnumerable<string> lines = revealedLineCount >= 0
                ? contentLines.Take(revealedLineCount)
                : contentLines;

            return string.Join("\n", lines);
        }

        public void AppendLine(string line)
        {
            contentLines.Add(line);
            Touch();
        }

        public void SetContent(string fullText)
        {
            contentLines = fullText.Split('\n').ToList();
            Touch();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  VIRTUAL DIRECTORY
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class VirtualDirectory : VirtualNode
    {
        public List<VirtualDirectory> subDirectories = new();
        public List<VirtualFile>      files          = new();

        /// <summary>
        /// Optional narrative event ID to fire when this directory is entered.
        /// </summary>
        public string onEnterEventId;

        public VirtualDirectory(string dirName) : base(dirName)
        {
            permissions = FilePermission.Read | FilePermission.Execute;
        }

        // ── Child access ─────────────────────────────────────

        public VirtualDirectory GetSubDir(string dirName, bool includeHidden = false)
        {
            return subDirectories.FirstOrDefault(d =>
                string.Equals(d.name, dirName, StringComparison.OrdinalIgnoreCase)
                && (includeHidden || !d.IsHidden));
        }

        public VirtualFile GetFile(string fileName, bool includeHidden = false)
        {
            return files.FirstOrDefault(f =>
                string.Equals(f.name, fileName, StringComparison.OrdinalIgnoreCase)
                && (includeHidden || !f.IsHidden));
        }

        /// <summary>
        /// Returns files whose name contains the query (case-insensitive).
        /// </summary>
        public List<VirtualFile> FindFiles(string query, bool includeHidden = false)
        {
            return files.Where(f =>
                f.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                && (includeHidden || !f.IsHidden)).ToList();
        }

        // ── Mutation ─────────────────────────────────────────

        public void AddFile(VirtualFile file)
        {
            file.parent = this;
            files.Add(file);
            Touch();
        }

        public void AddSubDir(VirtualDirectory dir)
        {
            dir.parent = this;
            subDirectories.Add(dir);
            Touch();
        }

        public bool RemoveFile(string fileName)
        {
            var f = GetFile(fileName, includeHidden: true);
            if (f == null) return false;
            files.Remove(f);
            Touch();
            return true;
        }

        public bool RemoveSubDir(string dirName)
        {
            var d = GetSubDir(dirName, includeHidden: true);
            if (d == null) return false;
            subDirectories.Remove(d);
            Touch();
            return true;
        }

        // ── Listing ──────────────────────────────────────────

        public IEnumerable<VirtualNode> ListAll(bool includeHidden = false)
        {
            foreach (var d in subDirectories)
                if (includeHidden || !d.IsHidden) yield return d;
            foreach (var f in files)
                if (includeHidden || !f.IsHidden) yield return f;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  VIRTUAL FILESYSTEM  (runtime singleton-like manager)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Manages the entire virtual filesystem tree at runtime.
    /// Initialised from a <see cref="FilesystemSO"/> ScriptableObject.
    /// </summary>
    public class VirtualFileSystem
    {
        public VirtualDirectory Root { get; private set; }
        public VirtualDirectory CurrentDirectory { get; private set; }

        // ── Bootstrap ────────────────────────────────────────

        public void InitialiseFromDefinition(FilesystemDefinition def)
        {
            Root = BuildDirectory(def.rootDirectory, null);
            CurrentDirectory = Root;
        }

        private VirtualDirectory BuildDirectory(DirectoryDefinition def, VirtualDirectory parentDir)
        {
            var dir = new VirtualDirectory(def.name)
            {
                permissions   = def.permissions,
                onEnterEventId = def.onEnterEventId,
                owner         = def.owner,
                group         = def.group
            };

            foreach (var fileDef in def.files)
            {
                var file = new VirtualFile(fileDef.name, fileDef.fileType)
                {
                    permissions       = fileDef.permissions,
                    contentLines      = new List<string>(fileDef.contentLines),
                    onReadEventId     = fileDef.onReadEventId,
                    progressiveReveal = fileDef.progressiveReveal,
                    revealedLineCount = fileDef.revealedLineCount,
                    owner             = fileDef.owner,
                    group             = fileDef.group
                };
                dir.AddFile(file);
            }

            foreach (var subDef in def.subDirectories)
            {
                var sub = BuildDirectory(subDef, dir);
                dir.AddSubDir(sub);
            }

            dir.parent = parentDir;
            return dir;
        }

        // ── Navigation ───────────────────────────────────────

        /// <summary>
        /// Attempts to change the current directory.
        /// Returns null on success, or an error string.
        /// </summary>
        public string ChangeDirectory(string path)
        {
            var target = ResolvePath(path);
            if (target == null)
                return $"cd: no such file or directory: {path}";
            if (target.IsLocked)
                return $"cd: permission denied: {path}";

            CurrentDirectory = target;
            return null;
        }

        public VirtualDirectory ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "~") return Root;

            VirtualDirectory context;
            string[] parts;

            if (path.StartsWith("/"))
            {
                context = Root;
                parts   = path.TrimStart('/').Split('/');
            }
            else
            {
                context = CurrentDirectory;
                parts   = path.Split('/');
            }

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part) || part == ".") continue;
                if (part == "..")
                {
                    if (context.parent != null) context = context.parent;
                    continue;
                }

                var next = context.GetSubDir(part, includeHidden: true);
                if (next == null) return null;
                context = next;
            }

            return context;
        }

        /// <summary>
        /// Finds a file relative to the current directory or by absolute path.
        /// </summary>
        public VirtualFile ResolveFile(string path)
        {
            // Absolute path with directory prefix
            if (path.Contains("/"))
            {
                int lastSlash = path.LastIndexOf('/');
                string dirPart  = path.Substring(0, lastSlash);
                string filePart = path.Substring(lastSlash + 1);
                var dir = ResolvePath(string.IsNullOrEmpty(dirPart) ? "/" : dirPart);
                return dir?.GetFile(filePart, includeHidden: true);
            }

            return CurrentDirectory.GetFile(path, includeHidden: true);
        }

        // ── Global Search ────────────────────────────────────

        public List<VirtualFile> FindGlobal(string query)
        {
            var results = new List<VirtualFile>();
            SearchDir(Root, query, results);
            return results;
        }

        private void SearchDir(VirtualDirectory dir, string query, List<VirtualFile> results)
        {
            results.AddRange(dir.FindFiles(query, includeHidden: false));
            foreach (var sub in dir.subDirectories.Where(d => !d.IsHidden))
                SearchDir(sub, query, results);
        }

        public string GetCurrentPath()
        {
            if (CurrentDirectory == Root) return "/";
            return CurrentDirectory.FullPath;
        }
    }
}
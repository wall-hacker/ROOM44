// ============================================================
//  FilesystemSO.cs
//  ScriptableObject-based filesystem definition.
//  Edit the entire filesystem from the Unity Inspector.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TerminalHorror.Filesystem;

namespace TerminalHorror.ScriptableObjects
{
    // ─────────────────────────────────────────────────────────
    //  FILE DEFINITION  (inspector-serialisable)
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class FileDefinition
    {
        [Tooltip("File name, include extension, e.g. diary.txt")]
        public string name = "file.txt";

        public FileType fileType = FileType.Text;

        [TextArea(3, 20)]
        [Tooltip("Each element is one line. Use \\n within a string for internal breaks.")]
        public List<string> contentLines = new();

        public FilePermission permissions = FilePermission.Read;

        [Tooltip("Event ID to fire when this file is read via cat/open.")]
        public string onReadEventId;

        [Tooltip("If true, content is revealed progressively via typewriter effect.")]
        public bool progressiveReveal = false;

        [Tooltip("-1 = all lines visible. Otherwise how many lines are currently unlocked.")]
        public int revealedLineCount = -1;

        public string owner = "root";
        public string group = "root";
    }

    // ─────────────────────────────────────────────────────────
    //  DIRECTORY DEFINITION
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class DirectoryDefinition
    {
        public string name = "folder";
        public FilePermission permissions = FilePermission.Read | FilePermission.Execute;

        [Tooltip("Event ID to fire when cd enters this directory.")]
        public string onEnterEventId;

        public string owner = "root";
        public string group = "root";

        public List<FileDefinition> files = new();
        public List<DirectoryDefinition> subDirectories = new();
    }

    // ─────────────────────────────────────────────────────────
    //  FILESYSTEM DEFINITION  (top-level SO)
    // ─────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "FilesystemDefinition", menuName = "Terminal Horror/Filesystem Definition")]
    public class FilesystemDefinition : ScriptableObject
    {
        [Tooltip("The root of the filesystem. Name this '/'.")]
        public DirectoryDefinition rootDirectory = new()
        {
            name = "/",
            files = new List<FileDefinition>
            {
                new() { name = "welcome.txt", fileType = FileType.CorruptedText,
                    contentLines = new List<string>
                    {
                        "Y2FjYXQgcGUgdmFyemE="
                    }
                }
            },
            subDirectories = new List<DirectoryDefinition>
            {
                new() { name = "home",
                    subDirectories = new List<DirectoryDefinition>
                    {
                        new() { name = "user",
                            files = new List<FileDefinition>
                            {
                                new() { name = "README.txt", fileType = FileType.Text,
                                    contentLines = new List<string>
                                    {
                                        "Welcome.",
                                        "Type 'help' to see available commands.",
                                        "Type 'ls' to list files.",
                                        "Type 'cat <filename>' to read a file.",
                                        "",
                                        "You should not be here."
                                    }
                                }
                            }
                        }
                    }
                },
                new() { name = "var",
                    subDirectories = new List<DirectoryDefinition>
                    {
                        new() { name = "log",
                            files = new List<FileDefinition>
                            {
                                new() { name = "system.log", fileType = FileType.Text,
                                    contentLines = new List<string>
                                    {
                                        "[2003-08-12 03:14:07] System boot.",
                                        "[2003-08-12 03:14:08] Filesystem mounted.",
                                        "[2003-08-12 03:14:09] User session started.",
                                        "[2003-08-12 03:14:??] ?????????"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
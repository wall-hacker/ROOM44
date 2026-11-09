// ============================================================
//  BuiltInCommands.cs
//  All built-in terminal commands:
//   Basic:     help, clear, echo, pwd, ls, cd, cat, open,
//              find, unzip
//   Narrative: listen, decode, restore, repair, remember
//   Utility:   history, hint, exit
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerminalHorror.Filesystem;

namespace TerminalHorror.Commands.Builtin
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  HELP
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class HelpCommand : TerminalCommand
    {
        public override string Verb        => "help";
        public override string Description => "List all commands, or 'help <command>' for details.";
        public override string[] Aliases   => new[] { "?" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count > 0)
            {
                var targetCmd = ctx.Terminal.Registry.Resolve(cmd.FirstArg);
                if (targetCmd == null)
                    return CommandResult.Error($"help: unknown command '{cmd.FirstArg}'");

                return CommandResult.Success(new[]
                {
                    $"<color=#66ffcc>{targetCmd.Verb}</color>",
                    targetCmd.DetailedHelp
                });
            }

            var lines = new List<string>
            {
                "<color=#66ffcc>── Available Commands ──────────────────────────</color>",
                ""
            };

            foreach (var c in ctx.Terminal.Registry.AllCommands().OrderBy(x => x.Verb))
            {
                lines.Add($"  <color=#aaffdd>{c.Verb,-12}</color> {c.Description}");
            }

            lines.Add("");
            lines.Add("Type <color=#ffdd88>help <command></color> for detailed usage.");
            lines.Add("You can also try typing naturally — the system will try to understand you.");
            return CommandResult.Success(lines);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  CLEAR
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class ClearCommand : TerminalCommand
    {
        public override string Verb        => "clear";
        public override string Description => "Clear the terminal screen.";
        public override string[] Aliases   => new[] { "cls", "clr" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            ctx.Terminal.ClearScreen();
            return CommandResult.Success();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  ECHO
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class EchoCommand : TerminalCommand
    {
        public override string Verb        => "echo";
        public override string Description => "Print text to the terminal.";
        public override string DetailedHelp => "Usage: echo <text>\nPrints the provided text.";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            return CommandResult.Success(cmd.AllArgs);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  PWD
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class PwdCommand : TerminalCommand
    {
        public override string Verb        => "pwd";
        public override string Description => "Print the current directory path.";
        public override string[] Aliases   => new[] { "where" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            return CommandResult.Success(ctx.FileSystem.GetCurrentPath());
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  LS
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class LsCommand : TerminalCommand
    {
        public override string Verb        => "ls";
        public override string Description => "List files and folders in the current directory.";
        public override string DetailedHelp => "Usage: ls [-a]\n  -a  Show hidden files.";
        public override string[] Aliases   => new[] { "list", "dir" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            bool showHidden = cmd.HasFlag("a");
            var  dir        = ctx.FileSystem.CurrentDirectory;
            var  nodes      = dir.ListAll(showHidden).ToList();

            if (!nodes.Any())
                return CommandResult.Success("(empty directory)");

            var lines = new List<string>();
            foreach (var node in nodes)
            {
                if (node is VirtualDirectory d)
                    lines.Add($"<color=#88ccff>{d.name}/</color>");
                else if (node is VirtualFile f)
                {
                    string color = f.fileType switch
                    {
                        FileType.CorruptedText => "#ff6666",
                        FileType.EncodedText   => "#ffaa44",
                        FileType.EventTrigger  => "#ff88ff",
                        _                      => "#dddddd"
                    };
                    string lockIcon = f.IsLocked ? " <color=#ff4444>[LOCKED]</color>" : "";
                    lines.Add($"<color={color}>{f.name}</color>{lockIcon}");
                }
            }
            return CommandResult.Success(lines);
        }

        public override List<string> GetCompletions(string partial, TerminalContext ctx)
        {
            return ctx.FileSystem.CurrentDirectory
                .ListAll(false)
                .Select(n => n.name)
                .Where(n => n.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  CD
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class CdCommand : TerminalCommand
    {
        public override string Verb        => "cd";
        public override string Description => "Change current directory.";
        public override string DetailedHelp => "Usage: cd <path>\nExamples: cd home   cd ..   cd /var/log";
        public override string[] Aliases   => new[] { "go", "enter" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count == 0)
            {
                ctx.FileSystem.ChangeDirectory("~");
                return CommandResult.Success();
            }

            string error = ctx.FileSystem.ChangeDirectory(cmd.FirstArg);
            if (error != null)
                return CommandResult.Error(error);

            // Fire directory enter event
            var current = ctx.FileSystem.CurrentDirectory;
            if (!string.IsNullOrEmpty(current.onEnterEventId))
                ctx.EventSystem?.TriggerEvent(current.onEnterEventId);

            return CommandResult.Success();
        }

        public override List<string> GetCompletions(string partial, TerminalContext ctx)
        {
            return ctx.FileSystem.CurrentDirectory.subDirectories
                .Where(d => !d.IsHidden && d.name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.name)
                .ToList();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  CAT
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class CatCommand : TerminalCommand
    {
        public override string Verb        => "cat";
        public override string Description => "Read and display the contents of a file.";
        public override string DetailedHelp => "Usage: cat <filename>\nExample: cat diary.txt";
        public override string[] Aliases   => new[] { "read", "show", "view" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count == 0)
                return CommandResult.Error("cat: missing file operand. Usage: cat <filename>");

            var file = ctx.FileSystem.ResolveFile(cmd.FirstArg);
            if (file == null)
                return TryFuzzyMatch(cmd.FirstArg, ctx);

            if (file.IsLocked)
                return CommandResult.Error($"cat: {cmd.FirstArg}: permission denied");

            var lines = new List<string>();

            // Apply corruption rendering
            Debug.Log($"File: {file.name}");
            Debug.Log($"Readable: {file.IsReadable}");
            Debug.Log($"Line count: {file.contentLines?.Count}");
            Debug.Log($"Revealed count: {file.revealedLineCount}");

            string content = file.GetContent();

            Debug.Log($"Content length: {content?.Length}");
            Debug.Log($"Content: [{content}]");
            if (file.fileType == FileType.CorruptedText)
            {
                content = CorruptText(content);
                content = $"<color=#ffaa44>[CORRUPTED DATA]\n{content}</color>";
                return CommandResult.Success(content);
            }
            else if (file.fileType == FileType.EncodedText)
            {
                content = $"<color=#ffaa44>[ENCODED DATA]\n{content}</color>";
                return CommandResult.Success(content);
            }
            else if (file.fileType == FileType.EventTrigger)
            {
                // Fire event and return minimal output
                if (!string.IsNullOrEmpty(file.onReadEventId))
                    ctx.EventSystem?.TriggerEvent(file.onReadEventId);
                return CommandResult.Success("[ACCESS LOG ENTRY RECORDED]");
            }

            lines.AddRange(content.Split('\n'));

            // Fire read event
            if (!string.IsNullOrEmpty(file.onReadEventId))
                ctx.EventSystem?.TriggerEvent(file.onReadEventId);

            return CommandResult.Success(lines);
        }

        private CommandResult TryFuzzyMatch(string hint, TerminalContext ctx)
        {
            var all = ctx.FileSystem.CurrentDirectory.ListAll(false).OfType<VirtualFile>().ToList();
            var fuzzy = all.Where(f =>
                f.name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (fuzzy.Count == 1)
            {
                var suggestion = fuzzy[0].name;
                return CommandResult.Suggestion(
                    $"cat: {hint}: not found. Did you mean '<color=#ffdd88>cat {suggestion}</color>'?");
            }

            if (fuzzy.Count > 1)
                return CommandResult.Suggestion(
                    $"cat: {hint}: not found. Similar files: {string.Join(", ", fuzzy.Select(f => f.name))}");

            return CommandResult.Error($"cat: {hint}: no such file");
        }

        private string CorruptText(string input)
        {
            var sb = new StringBuilder();
            var rng = new System.Random();
            char[] glitch = new[] { '█', '▓', '▒', '░', '■', '▪', '●', '◌', '◊' };
            // char[] glitch = new[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i' };
            foreach (char c in input)
            {
                if (c != '\n' && rng.NextDouble() < 0.15)
                    sb.Append($"<color=#ff4444>{glitch[rng.Next(glitch.Length)]}</color>");
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public override List<string> GetCompletions(string partial, TerminalContext ctx) =>
            ctx.FileSystem.CurrentDirectory.files
                .Where(f => !f.IsHidden && f.name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.name).ToList();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  OPEN
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class OpenCommand : TerminalCommand
    {
        public override string Verb        => "open";
        public override string Description => "Open a file or folder (triggers associated event).";
        public override string DetailedHelp =>
            "Usage: open <name>\nOpens a file or directory and fires any associated events.";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count == 0)
                return CommandResult.Error("open: missing operand");

            string name = cmd.AllArgs;

            // Try directory first
            var dir = ctx.FileSystem.CurrentDirectory.GetSubDir(name, includeHidden: false);
            if (dir != null)
            {
                string err = ctx.FileSystem.ChangeDirectory(name);
                if (err != null) return CommandResult.Error(err);
                if (!string.IsNullOrEmpty(dir.onEnterEventId))
                    ctx.EventSystem?.TriggerEvent(dir.onEnterEventId);
                return CommandResult.Success($"Opened directory: {dir.name}");
            }

            // Try file
            var file = ctx.FileSystem.ResolveFile(name);
            if (file == null)
                return CommandResult.Error($"open: '{name}': no such file or directory");

            if (file.IsLocked)
                return CommandResult.Error($"open: '{name}': permission denied");

            if (!string.IsNullOrEmpty(file.onReadEventId))
                ctx.EventSystem?.TriggerEvent(file.onReadEventId);

            return file.fileType switch
            {
                FileType.Image  => CommandResult.Success($"[IMAGE: {file.name}]\n(No graphical display available in terminal mode)"),
                FileType.Audio  => CommandResult.WithEvent($"[AUDIO: {file.name}] Playing...", file.onReadEventId),
                FileType.Video  => CommandResult.Success($"[VIDEO: {file.name}]\n(No video playback available in terminal mode)"),
                _               => CommandResult.Success(file.GetContent().Split('\n'))
            };
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  FIND
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class FindCommand : TerminalCommand
    {
        public override string Verb        => "find";
        public override string Description => "Search for files by name across the filesystem.";
        public override string DetailedHelp => "Usage: find <query>\nExample: find diary";
        public override string[] Aliases   => new[] { "search" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count == 0)
                return CommandResult.Error("find: missing search term");

            var results = ctx.FileSystem.FindGlobal(cmd.AllArgs);
            if (!results.Any())
                return CommandResult.Success($"find: no results for '{cmd.AllArgs}'");

            var lines = new List<string>
                { $"<color=#66ffcc>Search results for '{cmd.AllArgs}':</color>" };
            foreach (var f in results)
                lines.Add($"  {f.FullPath}");
            return CommandResult.Success(lines);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  UNZIP
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class UnzipCommand : TerminalCommand
    {
        public override string Verb        => "unzip";
        public override string Description => "Extract a zip archive in the current directory.";
        public override string DetailedHelp => "Usage: unzip <archive.zip>";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count == 0)
                return CommandResult.Error("unzip: missing archive name");

            var file = ctx.FileSystem.ResolveFile(cmd.FirstArg);
            if (file == null) return CommandResult.Error($"unzip: {cmd.FirstArg}: file not found");
            if (file.fileType != FileType.ZipArchive)
                return CommandResult.Error($"unzip: {cmd.FirstArg}: not a zip archive");
            if (file.IsLocked)
                return CommandResult.Error($"unzip: {cmd.FirstArg}: permission denied");

            // Fire the event encoded in the zip (which adds extracted files at runtime)
            if (!string.IsNullOrEmpty(file.onReadEventId))
            {
                ctx.EventSystem?.TriggerEvent(file.onReadEventId);
                return CommandResult.Success(new[]
                {
                    $"Archive: {file.name}",
                    "  inflating: [content extracted]",
                    "<color=#66ffcc>Done.</color>"
                });
            }

            return CommandResult.Success($"Archive {file.name} is empty.");
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  LISTEN  (narrative)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class ListenCommand : TerminalCommand
    {
        public override string Verb        => "listen";
        public override string Description => "Listen carefully to the environment.";
        public override string DetailedHelp =>
            "Usage: listen\nPauses and listens. Sometimes reveals new information.";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            ctx.EventSystem?.TriggerEvent("evt_listen");
            return CommandResult.Success(new[]
            {
                "...",
                "You hold your breath.",
                "...",
                "<color=#aaaaaa>Something shifts in the silence.</color>"
            });
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  DECODE  (narrative)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class DecodeCommand : TerminalCommand
    {
        public override string Verb        => "decode";
        public override string Description => "Attempt to decode an encoded or corrupted file.";
        public override string DetailedHelp => "Usage: decode <filename>";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count == 0)
                return CommandResult.Error("decode: specify a file to decode");

            var file = ctx.FileSystem.ResolveFile(cmd.FirstArg);
            if (file == null)
                return CommandResult.Error($"decode: {cmd.FirstArg}: file not found");

            if (file.fileType == FileType.EncodedText)
            {
                // Attempt to base-64 decode the content
                string raw = string.Join("", file.contentLines);
                try
                {
                    byte[] data    = Convert.FromBase64String(raw);
                    string decoded = System.Text.Encoding.UTF8.GetString(data);
                    file.fileType  = FileType.Text;
                    file.SetContent(decoded);
                    file.revealedLineCount = -1;

                    if (!string.IsNullOrEmpty(file.onReadEventId))
                        ctx.EventSystem?.TriggerEvent(file.onReadEventId);

                    return CommandResult.Success(new[]
                    {
                        $"<color=#66ffcc>Decoded {file.name}:</color>",
                        decoded
                    });
                }
                catch
                {
                    return CommandResult.Error("decode: failed — data too corrupted to recover");
                }
            }

            if (file.fileType == FileType.CorruptedText)
                return CommandResult.Error("decode: data is too corrupted — try 'repair' first");

            return CommandResult.Error($"decode: {cmd.FirstArg}: file does not appear to be encoded");
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  RESTORE  (narrative)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class RestoreCommand : TerminalCommand
    {
        public override string Verb        => "restore";
        public override string Description => "Restore deleted or hidden files from a directory.";
        public override string DetailedHelp => "Usage: restore\nAttempts to recover deleted files in the current directory.";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            ctx.EventSystem?.TriggerEvent("evt_restore");
            return CommandResult.Success(new[]
            {
                "Scanning for recoverable data...",
                "<color=#aaaaaa>... fragments found ...</color>",
                "<color=#ffdd88>Attempting to restore. This may take a moment.</color>"
            });
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  REPAIR  (narrative)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class RepairCommand : TerminalCommand
    {
        public override string Verb        => "repair";
        public override string Description => "Attempt to repair a corrupted file.";
        public override string DetailedHelp => "Usage: repair <filename>";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            if (cmd.args.Count == 0)
                return CommandResult.Error("repair: specify a file to repair");

            var file = ctx.FileSystem.ResolveFile(cmd.FirstArg);
            if (file == null)
                return CommandResult.Error($"repair: {cmd.FirstArg}: file not found");

            if (file.fileType != FileType.CorruptedText)
                return CommandResult.Error($"repair: {cmd.FirstArg}: file does not appear to be corrupted");

            // Trigger a narrative event that will handle the actual repair logic
            if (!string.IsNullOrEmpty(file.onReadEventId))
                ctx.EventSystem?.TriggerEvent($"evt_repair_{file.name}");
            else
                ctx.EventSystem?.TriggerEvent("evt_repair_generic");

            return CommandResult.Success(new[]
            {
                $"Attempting to repair {file.name}...",
                "<color=#ffaa44>Partial recovery in progress.</color>",
                "<color=#aaaaaa>Some data may be unrecoverable.</color>"
            });
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  REMEMBER  (narrative)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class RememberCommand : TerminalCommand
    {
        public override string Verb        => "remember";
        public override string Description => "Recall something you have seen before.";
        public override string DetailedHelp => "Usage: remember <keyword>\nExample: remember name";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            string keyword = cmd.AllArgs.Trim().ToLower();
            ctx.EventSystem?.TriggerEvent($"evt_remember_{keyword}");
            return CommandResult.Success(new[]
            {
                $"<color=#aaaaaa>You try to remember '{keyword}'...</color>",
                "<color=#888888>The memory is fragmented.</color>"
            });
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  HISTORY  (utility)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class HistoryCommand : TerminalCommand
    {
        public override string Verb        => "history";
        public override string Description => "Display command history.";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            var hist = ctx.Terminal.CommandHistory;
            if (!hist.Any())
                return CommandResult.Success("(no history)");

            var lines = hist.Select((h, i) => $"  {i + 1,3}  {h}").ToList();
            return CommandResult.Success(lines);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  HINT  (utility)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class HintCommand : TerminalCommand
    {
        public override string Verb        => "hint";
        public override string Description => "Request a contextual hint.";

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            ctx.EventSystem?.TriggerEvent("evt_hint_request");
            return CommandResult.Success(new[]
            {
                "<color=#aaffdd>Hint requested.</color>",
                "<color=#888888>Look around. Read what you find. Something is always watching.</color>"
            });
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  EXIT  (utility)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class ExitCommand : TerminalCommand
    {
        public override string Verb        => "exit";
        public override string Description => "Exit the terminal session.";
        public override string[] Aliases   => new[] { "quit", "q" };

        public override CommandResult Execute(ParsedCommand cmd, TerminalContext ctx)
        {
            ctx.EventSystem?.TriggerEvent("evt_exit_attempt");
            return CommandResult.Success();
            // return CommandResult.Success(new[]
            // {
            //     "<color=#ff6666>logout</color>",
            //     "<color=#888888>Connection closed.</color>",
            //     "",
            //     "<color=#ff4444>You cannot leave.</color>"
            // });
        }
    }
}
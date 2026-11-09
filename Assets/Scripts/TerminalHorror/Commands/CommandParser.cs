// ============================================================
//  CommandParser.cs
//  Modular, extensible command parser.
//  Supports: arguments, flags, aliases, quoted strings,
//  fuzzy intent matching, and player-friendly suggestions.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TerminalHorror.Commands
{
    // ─────────────────────────────────────────────────────────
    //  PARSED COMMAND  (result of tokenising user input)
    // ─────────────────────────────────────────────────────────

    public class ParsedCommand
    {
        public string          verb;
        public List<string>    args;
        public HashSet<string> flags;
        public string          rawInput;

        public bool HasFlag(string flag) => flags.Contains(flag);

        public string ArgString(int index) =>
            index < args.Count ? args[index] : string.Empty;

        public string FirstArg => args.Count > 0 ? args[0] : string.Empty;
        public string AllArgs  => string.Join(" ", args);
    }

    // ─────────────────────────────────────────────────────────
    //  COMMAND RESULT
    // ─────────────────────────────────────────────────────────

    public enum CommandResultType { Success, Error, Suggestion, Event }

    public class CommandResult
    {
        public CommandResultType type;
        public List<string>      lines         = new();
        public string            eventId;
        public bool              isAsync;

        public static CommandResult Success(string text = "")
        {
            var r = new CommandResult { type = CommandResultType.Success };
            if (!string.IsNullOrEmpty(text)) r.lines.Add(text);
            return r;
        }

        public static CommandResult Success(IEnumerable<string> lines)
        {
            var r = new CommandResult { type = CommandResultType.Success };
            r.lines.AddRange(lines);
            return r;
        }

        public static CommandResult Error(string message)
            => new() { type = CommandResultType.Error,   lines = { message } };

        public static CommandResult Suggestion(string message)
            => new() { type = CommandResultType.Suggestion, lines = { message } };

        public static CommandResult WithEvent(string text, string eventId)
            => new() { type = CommandResultType.Event, lines = { text }, eventId = eventId };
    }

    // ─────────────────────────────────────────────────────────
    //  ABSTRACT BASE COMMAND
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// All terminal commands extend this class.
    /// Register them with <see cref="CommandRegistry"/>.
    /// </summary>
    public abstract class TerminalCommand
    {
        /// <summary>Primary verb, e.g. "ls".</summary>
        public abstract string Verb { get; }

        /// <summary>Short one-line description shown by help.</summary>
        public abstract string Description { get; }

        /// <summary>Detailed usage shown by 'help {verb}'.</summary>
        public virtual string DetailedHelp =>
            $"Usage: {Verb}\n{Description}";

        /// <summary>Alternative names the player can use.</summary>
        public virtual string[] Aliases => Array.Empty<string>();

        /// <summary>
        /// Execute the command.
        /// Receives the current terminal context for filesystem and event access.
        /// </summary>
        public abstract CommandResult Execute(ParsedCommand cmd, TerminalContext ctx);

        /// <summary>
        /// Returns completions for partial input. Used by the autocomplete system.
        /// Override to provide context-sensitive completions.
        /// </summary>
        public virtual List<string> GetCompletions(string partial, TerminalContext ctx) =>
            new();
    }

    // ─────────────────────────────────────────────────────────
    //  TERMINAL CONTEXT  (passed to every command)
    // ─────────────────────────────────────────────────────────

    public class TerminalContext
    {
        public Filesystem.VirtualFileSystem FileSystem;
        public Events.TerminalEventSystem   EventSystem;
        public Core.TerminalManager         Terminal;
    }

    // ─────────────────────────────────────────────────────────
    //  COMMAND REGISTRY
    // ─────────────────────────────────────────────────────────

    public class CommandRegistry
    {
        private readonly Dictionary<string, TerminalCommand> _commandsByVerb  = new();
        private readonly Dictionary<string, TerminalCommand> _commandByAlias  = new();

        // Player-friendly intent mappings  e.g. "read" → "cat", "look" → "ls"
        private readonly Dictionary<string, string> _intentMap = new()
        {
            { "read",   "cat"   },
            { "look",   "ls"    },
            { "list",   "ls"    },
            { "dir",    "ls"    },
            { "show",   "cat"   },
            { "view",   "cat"   },
            { "go",     "cd"    },
            { "enter",  "cd"    },
            { "where",  "pwd"   },
            { "search", "find"  },
            { "quit",   "exit"  },
            { "q",      "exit"  },
            { "?",      "help"  },
            { "h",      "help"  },
        };

        public void Register(TerminalCommand cmd)
        {
            if (cmd == null) throw new ArgumentNullException(nameof(cmd));
            _commandsByVerb[cmd.Verb.ToLower()] = cmd;
            foreach (var alias in cmd.Aliases)
                _commandByAlias[alias.ToLower()] = cmd;
        }

        public TerminalCommand Resolve(string verb)
        {
            verb = verb.ToLower();

            if (_commandsByVerb.TryGetValue(verb, out var cmd)) return cmd;
            if (_commandByAlias.TryGetValue(verb, out cmd))     return cmd;

            // Intent remapping
            if (_intentMap.TryGetValue(verb, out var remapped))
                if (_commandsByVerb.TryGetValue(remapped, out cmd)) return cmd;

            return null;
        }

        public IEnumerable<TerminalCommand> AllCommands() => _commandsByVerb.Values;

        public string GetRemappedVerb(string verb)
        {
            verb = verb.ToLower();
            if (_intentMap.TryGetValue(verb, out var remapped)) return remapped;
            return null;
        }

        public List<string> GetSuggestions(string partial)
        {
            partial = partial.ToLower();
            var all = _commandsByVerb.Keys
                .Concat(_commandByAlias.Keys)
                .Concat(_intentMap.Keys);
            return all.Where(k => k.StartsWith(partial)).Distinct().OrderBy(k => k).ToList();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  COMMAND PARSER
    // ─────────────────────────────────────────────────────────

    public static class CommandParser
    {
        // Regex: splits on spaces but respects quoted strings
        private static readonly Regex TokenRegex =
            new(@"(?:""([^""]*)"")|(?:'([^']*)')|(\S+)", RegexOptions.Compiled);

        public static ParsedCommand Parse(string input)
        {
            var result = new ParsedCommand
            {
                rawInput = input,
                args     = new List<string>(),
                flags    = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            if (string.IsNullOrWhiteSpace(input)) return result;

            var tokens = new List<string>();
            foreach (Match m in TokenRegex.Matches(input))
            {
                // Group 1: double-quoted, Group 2: single-quoted, Group 3: bare
                string token = m.Groups[1].Success ? m.Groups[1].Value
                             : m.Groups[2].Success ? m.Groups[2].Value
                             : m.Groups[3].Value;
                tokens.Add(token);
            }

            if (tokens.Count == 0) return result;

            result.verb = tokens[0].ToLower();

            for (int i = 1; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.StartsWith("--"))
                    result.flags.Add(t.Substring(2));
                else if (t.StartsWith("-") && t.Length > 1)
                    foreach (char c in t.Substring(1)) result.flags.Add(c.ToString());
                else
                    result.args.Add(t);
            }

            return result;
        }

        /// <summary>
        /// Intelligent intent parsing for player-friendly remapping.
        /// e.g. "read diary" → "cat diary.txt" if diary.txt exists.
        /// </summary>
        public static ParsedCommand ParseWithIntentRemap(string input, CommandRegistry registry,
            Filesystem.VirtualFileSystem fs)
        {
            var cmd = Parse(input);
            if (string.IsNullOrEmpty(cmd.verb)) return cmd;

            // Already a known command?
            if (registry.Resolve(cmd.verb) != null) return cmd;

            // Try remapping
            var remapped = registry.GetRemappedVerb(cmd.verb);
            if (remapped != null)
            {
                cmd.verb = remapped;
                return cmd;
            }

            // Fuzzy: "read diary" → try to find "diary.txt" and map to cat
            if (cmd.args.Count > 0)
            {
                var guessedFile = TryMatchFile(cmd.args[0], fs);
                if (guessedFile != null)
                {
                    cmd.verb    = "cat";
                    cmd.args[0] = guessedFile;
                }
            }

            return cmd;
        }

        private static string TryMatchFile(string hint, Filesystem.VirtualFileSystem fs)
        {
            // Try exact match first
            var f = fs.CurrentDirectory.GetFile(hint, includeHidden: false);
            if (f != null) return f.name;

            // Try adding extensions
            foreach (var ext in new[] { ".txt", ".log", ".dat", ".zip", ".enc" })
            {
                f = fs.CurrentDirectory.GetFile(hint + ext, includeHidden: false);
                if (f != null) return f.name;
            }

            // Try partial match
            var allFiles = fs.CurrentDirectory.ListAll(false)
                .OfType<Filesystem.VirtualFile>().ToList();
            var partial  = allFiles.FirstOrDefault(x =>
                x.name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);

            return partial?.name;
        }
    }
}
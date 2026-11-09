// ============================================================
//  TerminalManager.cs
//  Central orchestrator for the terminal simulation.
//  Handles:
//   - Input System keyboard reading (Unity 6 Input System)
//   - Command registration and dispatch
//   - Filesystem management
//   - Event system wiring
//   - Corruption system integration
//   - Command history (arrow keys)
//   - Tab autocomplete
//   - Audio hooks
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TerminalHorror.Commands;
using TerminalHorror.Commands.Builtin;
using TerminalHorror.Filesystem;
using TerminalHorror.Events;
using TerminalHorror.Corruption;
using TerminalHorror.UI;
using TerminalHorror.Audio;
using TerminalHorror.ScriptableObjects;

namespace TerminalHorror.Core
{
    [RequireComponent(typeof(TerminalEventSystem))]
    [RequireComponent(typeof(CorruptionSystem))]
    public class TerminalManager : MonoBehaviour
    {
        // ── Inspector wiring ─────────────────────────────────

        [Header("Core References")]
        public TerminalUI                terminalUI;
        public FilesystemDefinition      filesystemDefinition;
        public CorruptionSettingsSO      corruptionSettings;

        [Header("Audio")]
        public TerminalAudioController   audioController;

        [Header("Settings")]
        [Tooltip("Max commands kept in history.")]
        public int maxHistoryEntries = 100;

        [Tooltip("If true, unknown input prints a helpful suggestion.")]
        public bool playerFriendlyMode = true;

        [Tooltip("Seconds of simulated command processing delay (base).")]
        [Range(0f, 0.5f)] public float baseProcessingDelay = 0.05f;

        // ── Public properties ────────────────────────────────

        public CommandRegistry  Registry    { get; private set; }
        public VirtualFileSystem FileSystem { get; private set; }

        public IReadOnlyList<string> CommandHistory => _commandHistory;

        // ── Private state ─────────────────────────────────────

        private TerminalEventSystem _eventSystem;
        private CorruptionSystem    _corruption;
        private TerminalContext     _ctx;

        private string        _inputBuffer    = "";
        private List<string>  _commandHistory = new();
        private int           _historyIndex   = -1;
        private string        _historyTemp    = "";

        private bool _processingCommand = false;
        private bool _terminalActive    = true;

        // ── Backspace internal logic ─────────────────────────
        private float _backspaceHoldTime;
        private float _nextBackspaceTime;
        private bool _backspaceHeld;
        [SerializeField] private float backspaceInitialDelay = 0.35f;
        [SerializeField] private float backspaceStartInterval = 0.12f;
        [SerializeField] private float backspaceMinInterval = 0.03f;
        [SerializeField] private float backspaceAcceleration = 0.92f;

        // ─── Unity Lifecycle ─────────────────────────────────

        private void Awake()
        {
            // Get sibling components
            _eventSystem = GetComponent<TerminalEventSystem>();
            _corruption  = GetComponent<CorruptionSystem>();

            if (corruptionSettings != null)
                _corruption.settings = corruptionSettings;

            // Build filesystem
            FileSystem = new VirtualFileSystem();
            if (filesystemDefinition != null)
                FileSystem.InitialiseFromDefinition(filesystemDefinition);

            // Build command registry and register all built-in commands
            Registry = new CommandRegistry();
            RegisterBuiltInCommands();

            // Build context
            _ctx = new TerminalContext
            {
                FileSystem  = FileSystem,
                EventSystem = _eventSystem,
                Terminal    = this
            };

            // Wire event system callbacks
            _eventSystem.OnTerminalOutputRequested  += HandleEventOutput;
            _eventSystem.OnCorruptionIntensityChanged += _corruption.AdjustIntensity;

            // Wire corruption callbacks
            _corruption.OnGhostMessageReady        += lines => terminalUI?.PrintLines(lines);
            _corruption.OnInputOverwritten         += HandleInputOverwrite;
            _corruption.OnFakeCrashSequence        += lines => terminalUI?.PrintLines(lines);
            _corruption.OnDelayedResponseRequested += _ => { /* handled inside ProcessCommand */ };
        }

        private void Start()
        {
            PrintWelcomeBanner();
            RefreshInputDisplay();
        }

        private void OnEnable()
        {
            // Subscribe to Unity Input System keyboard events
            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnTextInput;
        }

        private void OnDisable()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnTextInput;
        }

        private void Update()
        {
            if (!_terminalActive) return;
            if (InputBlockedByDialogue) return;

            HandleSpecialKeys();
        }

        // ─── Input Handling ──────────────────────────────────

        // While a dialogue is on screen (e.g. the lights-out lines) the terminal must not take
        // keystrokes: typing `exit` there would fire evt_exit_attempt on top of the dialogue's own
        // stand-up sequence and play the get-up / turn twice.
        private static bool InputBlockedByDialogue =>
            DialogueSystem.Core.DialogueManager.Instance != null &&
            DialogueSystem.Core.DialogueManager.Instance.IsDialogueActive;

        private void OnTextInput(char c)
        {
            if (!_terminalActive || _processingCommand) return;
            if (InputBlockedByDialogue) return;

            // Filter control characters (handled in HandleSpecialKeys)
            if (c < 32 || c == 127) return;

            audioController?.PlayKeySound();
            _eventSystem.RegisterPlayerInput();

            _inputBuffer += c;
            RefreshInputDisplay();
        }

        private void HandleSpecialKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Submit
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                SubmitInput();
                return;
            }

            // // Backspace
            // if (kb.backspaceKey.wasPressedThisFrame || kb.backspaceKey.isPressed)
            // {
            //     // Held backspace: handled via isPressed with a repeat timer
            //     if (_inputBuffer.Length > 0)
            //     {
            //         _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
            //         audioController?.PlayKeySound();
            //         RefreshInputDisplay();
            //     }
            //     return;
            // }

            // Backspace
            // Pressed this frame
            if (kb.backspaceKey.wasPressedThisFrame)
            {
                if (_inputBuffer.Length == 0) return;
                _inputBuffer = _inputBuffer[..^1];
                _backspaceHeld = true;
                _backspaceHoldTime = 0f;
                _nextBackspaceTime = Time.time + backspaceInitialDelay;

                audioController?.PlayKeySound();
                RefreshInputDisplay();
                return;
            }

            // Held
            if (kb.backspaceKey.isPressed)
            {
                _backspaceHoldTime += Time.deltaTime;

                if (Time.time >= _nextBackspaceTime)
                {
                    if (_inputBuffer.Length == 0) return;
                    _inputBuffer = _inputBuffer[..^1];

                    audioController?.PlayKeySound();
                    RefreshInputDisplay();

                    // accelerate: reduce interval over time
                    float interval = Mathf.Lerp(
                        backspaceStartInterval,
                        backspaceMinInterval,
                        1f - Mathf.Exp(-_backspaceHoldTime * backspaceAcceleration)
                    );

                    _nextBackspaceTime = Time.time + interval;
                }

                return;
            }

            // Released
            _backspaceHeld = false;

            // Tab — autocomplete
            if (kb.tabKey.wasPressedThisFrame)
            {
                TryAutocomplete();
                return;
            }

            // Arrow up — history back
            if (kb.upArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(-1);
                return;
            }

            // Arrow down — history forward
            if (kb.downArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(1);
                return;
            }

            // Ctrl+L — clear
            if (kb.leftCtrlKey.isPressed && kb.lKey.wasPressedThisFrame)
            {
                ClearScreen();
                return;
            }

            // Ctrl+C — cancel current input
            if (kb.leftCtrlKey.isPressed && kb.cKey.wasPressedThisFrame)
            {
                terminalUI?.EchoCommand(_inputBuffer + "^C");
                _inputBuffer = "";
                RefreshInputDisplay();
            }
        }

        private void SubmitInput()
        {
            if (_processingCommand) return;

            string raw = _inputBuffer.Trim();
            _inputBuffer = "";
            _historyIndex = -1;
            _historyTemp  = "";

            terminalUI?.EchoCommand(raw);
            RefreshInputDisplay();

            if (string.IsNullOrWhiteSpace(raw))
            {
                audioController?.PlayKeySound();
                return;
            }

            // Add to history
            if (_commandHistory.Count == 0 || _commandHistory[^1] != raw)
                _commandHistory.Add(raw);
            while (_commandHistory.Count > maxHistoryEntries)
                _commandHistory.RemoveAt(0);

            _eventSystem.RegisterPlayerInput();
            StartCoroutine(ProcessCommand(raw));
        }

        // ─── Command Processing ──────────────────────────────

        private IEnumerator ProcessCommand(string raw)
        {
            _processingCommand = true;

            // Corruption: may hallucinate the input
            string actual = raw;
            string hallucination = _corruption.TryHallucinateInput(raw);
            if (hallucination != null)
            {
                terminalUI?.PrintLine(
                    $"<color=#330000>> {hallucination}</color>");
                actual = hallucination;
            }

            // Roll other corruption effects
            _corruption.RollEffects(raw);

            // Simulated processing delay (+ corruption random delay)
            float delay = baseProcessingDelay;
            yield return new WaitForSeconds(delay);

            // Parse with intent remapping
            var parsed = CommandParser.ParseWithIntentRemap(actual, Registry, FileSystem);

            // Dispatch
            CommandResult result;
            var cmd = Registry.Resolve(parsed.verb);

            if (cmd != null)
            {
                result = cmd.Execute(parsed, _ctx);
            }
            else
            {
                result = HandleUnknownCommand(parsed);
            }

            // Apply corruption to output text
            var outLines = new List<string>(result.lines.Count);
            foreach (var line in result.lines)
                outLines.Add(_corruption.GlitchText(line));

            // Print result
            bool useTypewriter = result.type == CommandResultType.Success
                                 && outLines.Count > 1;

            if (useTypewriter)
                terminalUI?.PrintTypewriter(outLines);
            else
                terminalUI?.PrintLines(outLines);

            // Fire associated event
            if (!string.IsNullOrEmpty(result.eventId))
                _eventSystem.TriggerEvent(result.eventId);

            audioController?.PlayCommandSound(result.type == CommandResultType.Error);

            _processingCommand = false;
            RefreshInputDisplay();
        }

        private CommandResult HandleUnknownCommand(ParsedCommand parsed)
        {
            if (!playerFriendlyMode)
                return CommandResult.Error($"{parsed.verb}: command not found");

            var suggestions = Registry.GetSuggestions(parsed.verb);
            if (suggestions.Count > 0)
            {
                string best = suggestions[0];
                return CommandResult.Suggestion(
                    $"<color=#ffaa44>'{parsed.verb}': command not found.</color> " +
                    $"Did you mean: <color=#ffdd88>{best}</color>? " +
                    $"(Type '<color=#ffdd88>help</color>' for all commands)");
            }

            return CommandResult.Error(
                $"<color=#ff6666>'{parsed.verb}': command not found.</color> " +
                $"Type '<color=#ffdd88>help</color>' for available commands.");
        }

        // ─── Autocomplete ────────────────────────────────────

        private void TryAutocomplete()
        {
            if (string.IsNullOrEmpty(_inputBuffer)) return;

            var tokens = _inputBuffer.Split(' ');

            if (tokens.Length == 1)
            {
                // Complete the verb
                var matches = Registry.GetSuggestions(tokens[0]);
                if (matches.Count == 1)
                {
                    _inputBuffer = matches[0] + " ";
                }
                else if (matches.Count > 1)
                {
                    terminalUI?.PrintLine(string.Join("  ", matches));
                }
            }
            else
            {
                // Complete argument using the active command's completions
                var cmd = Registry.Resolve(tokens[0]);
                if (cmd == null) return;

                string partial = tokens[^1];
                var completions = cmd.GetCompletions(partial, _ctx);

                if (completions.Count == 1)
                {
                    tokens[^1] = completions[0];
                    _inputBuffer = string.Join(" ", tokens);
                }
                else if (completions.Count > 1)
                {
                    terminalUI?.PrintLine(string.Join("  ", completions));
                }
            }

            RefreshInputDisplay();
        }

        // ─── History Navigation ──────────────────────────────

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            if (_historyIndex == -1)
            {
                _historyTemp  = _inputBuffer;
                _historyIndex = _commandHistory.Count;
            }

            _historyIndex = Mathf.Clamp(_historyIndex + direction, 0, _commandHistory.Count);

            _inputBuffer = _historyIndex == _commandHistory.Count
                ? _historyTemp
                : _commandHistory[_historyIndex];

            RefreshInputDisplay();
        }

        // ─── Public API ──────────────────────────────────────

        public void ClearScreen()
        {
            terminalUI?.ClearOutput();
        }

        /// <summary>
        /// Inject a command programmatically (e.g. from events or editor tools).
        /// </summary>
        public void InjectCommand(string commandText)
        {
            _inputBuffer = commandText;
            SubmitInput();
        }

        /// <summary>
        /// Print directly to the terminal output (bypasses command system).
        /// </summary>
        public void PrintDirect(string line)
        {
            terminalUI?.PrintLine(line);
        }

        public void PrintDirectLines(List<string> lines)
        {
            terminalUI?.PrintLines(lines);
        }

        // ─── Private helpers ─────────────────────────────────

        private void RefreshInputDisplay()
        {
            terminalUI?.UpdateInputDisplay(_inputBuffer, FileSystem.GetCurrentPath());
        }

        private void PrintWelcomeBanner()
        {
            // Banner is committed to history immediately so the prompt line
            // always sits below it on first render.
            var banner = new List<string>
            {
                "<color=#226644>╔══════════════════════════════════════════╗</color>",
                "<color=#226644>║</color>   <color=#66ffcc>SYSTEM TERMINAL v2.3.1</color>                                   <color=#226644>║</color>",
                "<color=#226644>║</color>   <color=#aaaaaa>Last login: Unknown</color>                                      <color=#226644>║</color>",
                "<color=#226644>╚══════════════════════════════════════════╝</color>",
                "",
                "<color=#aaaaaa>Type <color=#ffdd88>help</color> to see available commands.</color>",
                ""
            };
            terminalUI?.PrintLines(banner);
        }

        private void RegisterBuiltInCommands()
        {
            Registry.Register(new HelpCommand());
            Registry.Register(new ClearCommand());
            Registry.Register(new EchoCommand());
            Registry.Register(new PwdCommand());
            Registry.Register(new LsCommand());
            Registry.Register(new CdCommand());
            Registry.Register(new CatCommand());
            Registry.Register(new OpenCommand());
            Registry.Register(new FindCommand());
            Registry.Register(new UnzipCommand());
            Registry.Register(new ListenCommand());
            Registry.Register(new DecodeCommand());
            Registry.Register(new RestoreCommand());
            Registry.Register(new RepairCommand());
            Registry.Register(new RememberCommand());
            Registry.Register(new HistoryCommand());
            Registry.Register(new HintCommand());
            Registry.Register(new ExitCommand());
        }

        private void HandleEventOutput(List<string> lines, float lineDelay)
        {
            terminalUI?.PrintTypewriter(lines, lineDelay);
        }

        private void HandleInputOverwrite(string original, string replacement)
        {
            terminalUI?.PrintLine(
                $"<color=#330000>// {original} → {replacement}</color>");
        }

        public void InjectHistoryLines(List<string> lines)
        {
            _commandHistory.Clear();
            _commandHistory.AddRange(lines);
        }
    }
}
// ============================================================
//  TerminalUI.cs
//  Single-buffer terminal — one TMP_Text, one ScrollRect.
//
//  The terminal is one continuous block of text.
//  The last line is always the live prompt + typed input
//  + a ▌ cursor character baked into the string itself.
//
//  Layout (one GameObject):
//    Canvas
//    └── TerminalRoot
//        ├── Background      Image
//        └── Scroll View     ScrollRect
//            └── Viewport
//                └── Content
//                    └── TerminalText    TMP_Text   ← the ONLY text field
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TerminalHorror.UI
{
    // ─────────────────────────────────────────────────────────
    //  THEME  (ScriptableObject — unchanged public API)
    // ─────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "TerminalTheme", menuName = "Terminal Horror/Terminal Theme")]
    public class TerminalThemeSO : ScriptableObject
    {
        [Header("Colors")]
        public Color backgroundColor = new(0.04f, 0.04f, 0.04f);
        public Color foregroundColor = new(0.72f, 1f, 0.85f);
        public Color promptColor     = new(0.4f,  1f, 0.6f);
        public Color errorColor      = new(1f, 0.35f, 0.35f);
        public Color dimColor        = new(0.5f, 0.5f, 0.5f);

        [Header("Typography")]
        public TMP_FontAsset terminalFont;
        [Range(0.005f, 0.1f)] public float fontSize    = 0.025f;
        public float               lineSpacing  = 4f;

        [Header("Cursor")]
        [Tooltip("Block cursor character inserted into the text.")]
        public string cursorChar     = "▌";
        public float  cursorBlinkRate = 0.53f;

        [Header("Scroll")]
        public float scrollSensitivity = 3f;
        public int   maxStoredLines    = 500;

        [Header("Typewriter")]
        public float typewriterCharDelay = 0.025f;
        public float typewriterLineDelay = 0.08f;
    }

    // ─────────────────────────────────────────────────────────
    //  TERMINAL UI
    // ─────────────────────────────────────────────────────────

    public class TerminalUI : MonoBehaviour
    {
        [Header("References  (one text field only)")]
        [Tooltip("The single TMP_Text that is the entire terminal surface.")]
        public TMP_Text terminalText;

        [Tooltip("The ScrollRect that wraps terminalText.")]
        public ScrollRect scrollRect;

        [Tooltip("Optional background Image (themed colour).")]
        public Image backgroundImage;

        [Header("Theme")]
        public TerminalThemeSO theme;

        [Header("Prompt")]
        public string promptPrefix = "user@terminal:";
        public bool   showFullPath = true;

        // ── Private state ─────────────────────────────────────

        // Committed history lines (everything above the input line).
        private readonly List<string> _history = new();

        // The live input the player is currently typing.
        private string _currentInput = "";
        private string _currentPath  = "/";

        // Cursor blink
        private bool  _cursorVisible = true;
        private float _cursorTimer;

        // True while a typewriter coroutine holds the output lock.
        // Input display is still updated; we just don't re-render the
        // prompt line until the typewriter finishes its current line.
        // private bool _typewriterActive;

        // ─── Unity Lifecycle ──────────────────────────────────

        private void Awake()
        {
            ApplyTheme();
        }

        private void Update()
        {
            TickCursor();
        }

        // ─── Theme ────────────────────────────────────────────

        public void ApplyTheme()
        {
            if (theme == null) return;

            if (backgroundImage != null)
                backgroundImage.color = theme.backgroundColor;

            if (terminalText != null)
            {
                terminalText.color    = theme.foregroundColor;
                terminalText.fontSize = theme.fontSize;

                if (theme.terminalFont != null)
                    terminalText.font = theme.terminalFont;
            }
        }

        // ─── Committed output ─────────────────────────────────

        /// <summary>Commit a single line to history and redraw.</summary>
        public void PrintLine(string line)
        {
            _history.Add(line);
            TrimHistory();
            Redraw();
        }

        /// <summary>Commit multiple lines to history and redraw once.</summary>
        public void PrintLines(IEnumerable<string> lines)
        {
            foreach (var l in lines)
                _history.Add(l);
            TrimHistory();
            Redraw();
        }

        /// <summary>
        /// Typewriter effect — reveals each line character-by-character,
        /// committing it to history when the line is complete.
        /// The live input line stays visible below the output at all times.
        /// </summary>
        public void PrintTypewriter(List<string> lines, float lineDelay = -1f,
            float charDelay = -1f, Action onComplete = null)
        {
            StartCoroutine(TypewriterCoroutine(lines,
                lineDelay >= 0 ? lineDelay : (theme?.typewriterLineDelay ?? 0.08f),
                charDelay >= 0 ? charDelay : (theme?.typewriterCharDelay ?? 0.025f),
                onComplete));
        }

        private IEnumerator TypewriterCoroutine(List<string> lines,
            float lineDelay, float charDelay, Action onComplete)
        {
            // _typewriterActive = true;

            foreach (var line in lines)
            {
                var staged = new System.Text.StringBuilder();
                bool inTag = false;

                foreach (char c in line)
                {
                    if (c == '<') inTag = true;
                    if (c == '>') { inTag = false; staged.Append(c); continue; }
                    staged.Append(c);

                    if (!inTag && charDelay > 0f)
                    {
                        // Show partial line as the last history entry while typing
                        SetOrAppendLastHistory(staged.ToString(), isPartial: true);
                        Redraw();
                        yield return new WaitForSeconds(charDelay);
                    }
                }

                // Commit the completed line
                SetOrAppendLastHistory(staged.ToString(), isPartial: false);
                TrimHistory();
                Redraw();

                if (lineDelay > 0f)
                    yield return new WaitForSeconds(lineDelay);
            }

            // _typewriterActive = false;
            onComplete?.Invoke();
        }

        // Typewriter helper: while building a line character by character we
        // keep modifying the last history entry rather than appending new ones.
        private bool _partialLineActive;

        private void SetOrAppendLastHistory(string content, bool isPartial)
        {
            if (_partialLineActive)
                _history[^1] = content;          // overwrite in-progress line
            else
                _history.Add(content);            // start a new in-progress line

            _partialLineActive = isPartial;
        }

        /// <summary>Erase all history. Prompt line remains.</summary>
        public void ClearOutput()
        {
            _history.Clear();
            _partialLineActive = false;
            Redraw();
        }

        // ─── Input line ───────────────────────────────────────

        /// <summary>
        /// Called by TerminalManager every time the input buffer or path changes.
        /// Updates the live bottom line without committing anything to history.
        /// </summary>
        public void UpdateInputDisplay(string input, string path = null)
        {
            _currentInput = input;
            if (path != null) _currentPath = path;
            Redraw();
        }

        /// <summary>
        /// Commits the current prompt+input as a permanent history line
        /// (the "you pressed Enter" moment), then clears the live input.
        /// Call this BEFORE printing the command result.
        /// </summary>
        public void EchoCommand(string input)
        {
            // Build the same line that was showing as the live prompt
            _history.Add(BuildPromptLine(input, cursor: false));
            TrimHistory();
            _currentInput = "";
            Redraw();
        }

        // ─── Cursor blink ─────────────────────────────────────

        private void TickCursor()
        {
            if (theme == null) return;
            _cursorTimer += Time.deltaTime;
            if (_cursorTimer < theme.cursorBlinkRate) return;

            _cursorTimer   = 0f;
            _cursorVisible = !_cursorVisible;
            Redraw(forceScroll: false); // just re-join the string — cheap for TMP
        }

        // ─── Core render ──────────────────────────────────────

        /// <summary>
        /// Rebuilds the full TMP string: history lines + live input line.
        /// Called whenever anything changes.
        /// </summary>
        private void Redraw(bool forceScroll = true)
        {
            if (terminalText == null) return;

            // History block
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _history.Count; i++)
            {
                sb.Append(_history[i]);
                sb.Append('\n');
            }

            // Live input line (always last)
            sb.Append(BuildPromptLine(_currentInput, cursor: _cursorVisible));

            terminalText.text = sb.ToString();
            terminalText.ForceMeshUpdate();
            // ScrollToBottom();
            // StartCoroutine(ScrollNextFrame());
            if (forceScroll)
            {
                StopCoroutine(nameof(ScrollToBottomNextFrame)); // Prevent overlapping scrolls
                StartCoroutine(ScrollToBottomNextFrame());
            }
        }

        private string BuildPromptLine(string input, bool cursor)
        {
            string promptHex = ColorToHex(theme?.promptColor ?? Color.green);
            string pathPart  = showFullPath
                ? _currentPath
                : System.IO.Path.GetFileName(_currentPath);
            if (string.IsNullOrEmpty(pathPart)) pathPart = "/";

            string prompt    = $"{promptPrefix}{pathPart}$ ";
            string cursorStr = cursor ? (theme?.cursorChar ?? "▌") : "";

            return $"<color=#{promptHex}>{prompt}</color>{input}{cursorStr}";
        }

        // ─── Scroll ───────────────────────────────────────────

        // public void ScrollToBottom()
        // {
        //     if (scrollRect == null) return;
        //     StartCoroutine(ScrollNextFrame());
        // }

        // private IEnumerator ScrollNextFrame()
        // {
        //     yield return new WaitForEndOfFrame();
        //     scrollRect.normalizedPosition = new Vector2(0f, 0f);
        // }

        // private IEnumerator ScrollNextFrame()
        // {
        //     yield return null; // wait 1 frame for TMP to update

        //     Canvas.ForceUpdateCanvases();
        //     LayoutRebuilder.ForceRebuildLayoutImmediate(
        //         terminalText.rectTransform
        //     );

        //     Canvas.ForceUpdateCanvases();

        //     scrollRect.verticalNormalizedPosition = 0f;
        // }
        // private IEnumerator ScrollToBottomNextFrame()
        // {
        // // 1. Force TMP to calculate the new height immediately
        //     terminalText.ForceMeshUpdate();

        //     Canvas.ForceUpdateCanvases();
        //             LayoutRebuilder.ForceRebuildLayoutImmediate(
        //                 terminalText.rectTransform
        //             );

        //     // 2. Wait for the end of the frame so the layout system 
        //     // acknowledges the new height of the content object
        //     yield return new WaitForEndOfFrame();
        //     yield return new WaitForEndOfFrame();

        //     // 3. Force the canvas and layout to rebuild based on the new mesh size
        //     Canvas.ForceUpdateCanvases();
        //     if (scrollRect.content != null)
        //     {
        //         LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        //     }

        //     // 4. Finally, set position to absolute bottom
        //     scrollRect.verticalNormalizedPosition = 0f;
        // }

        private IEnumerator ScrollToBottomNextFrame()
        {
            // Wait until end of UI rebuild phase
            yield return null;

            Canvas.ForceUpdateCanvases();

            // Force layout rebuild of the ACTUAL scroll content
            if (scrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            }

            Canvas.ForceUpdateCanvases();

            scrollRect.verticalNormalizedPosition = -0.1f;
        }

        // ─── Helpers ──────────────────────────────────────────

        private void TrimHistory()
        {
            int max = theme?.maxStoredLines ?? 500;
            while (_history.Count > max)
                _history.RemoveAt(0);
        }

        private static string ColorToHex(Color c) =>
            $"{ToByte(c.r):X2}{ToByte(c.g):X2}{ToByte(c.b):X2}";

        private static int ToByte(float f) => Mathf.RoundToInt(f * 255f);
    }
}
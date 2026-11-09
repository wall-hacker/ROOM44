using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DialogueSystem.Data;

namespace DialogueSystem.UI
{
    /// <summary>
    /// Controls the Normal Dialogue panel.
    ///
    /// Special characters recognised inside dialogue text:
    ///   '|'  — pause character: waits <see cref="pauseDuration"/> seconds without
    ///           appending anything to the visible text (dramatic timing).
    ///   '`'  — instant-space character: appends a regular space with zero delay
    ///           (use to add spacing without the normal per-character wait).
    ///
    /// Expected hierarchy (all fields assignable in Inspector):
    ///   DialoguePanel (Canvas Group)
    ///     ├── NameplateGroup
    ///     │     ├── NameText (TMP)
    ///     │     └── PortraitImage (Image)
    ///     └── DialogueText (TMP)
    /// </summary>
    public class NormalDialogueUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup panelGroup;

        [Header("Nameplate")]
        [SerializeField] private GameObject   nameplateGroup1;
        [SerializeField] private GameObject   nameplateGroup2;
        [SerializeField] private TMP_Text     nameText;
        [SerializeField] private Image        portraitImage;
        [SerializeField] private Image        portraitBackgroundImage;

        [Header("Dialogue")]
        [SerializeField] private TMP_Text dialogueText;

        [Header("Continue Indicator")]
        [Tooltip("Small icon or arrow shown when a batch is fully revealed and waiting for input.")]
        [SerializeField] private GameObject continueIndicator;

        [Header("Settings")]
        [SerializeField] private KeyCode advanceKey = KeyCode.E;
        [Tooltip("Fade duration for panel show/hide.")]
        [SerializeField] private float fadeDuration = 0.2f;
        [Tooltip("How long '|' pauses the typewriter without appending any character.")]
        [SerializeField] private float pauseDuration = 0.4f;

        // Special characters
        private const char PauseChar       = '|';   // wait pauseDuration, append nothing
        private const char InstantSpaceChar = '`';  // append ' ', zero delay

        // ── State ──────────────────────────────────────────────────────────────
        private bool _skipRequested;
        private bool _advanceRequested;
        private bool _isTyping;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Runs all lines in the NormalDialogueSO, then hides the panel.
        /// Yield this coroutine from DialogueManager.
        /// </summary>
        public IEnumerator RunDialogue(NormalDialogueSO data)
        {
            ResetPortraits();
            yield return ShowPanel();

            foreach (var line in data.lines)
            {
                line.onLineStart?.ExecuteAll();
                yield return DisplayLine(line);
                line.onLineEnd?.ExecuteAll();
            }

            yield return HidePanel();
            dialogueText.text = string.Empty;
        }

        // ── Internal ───────────────────────────────────────────────────────────

        private IEnumerator DisplayLine(DialogueLine line)
        {
            // Nameplate
            bool hasName = !string.IsNullOrEmpty(line.characterName);
            nameplateGroup1?.SetActive(hasName);
            nameplateGroup2?.SetActive(hasName);
            if (hasName && nameText) nameText.text = line.characterName;

            // Portrait
            if (portraitImage != null)
            {
                portraitImage.gameObject.SetActive(line.characterPortrait != null);
                if (line.characterPortrait != null)
                    portraitImage.sprite = line.characterPortrait;
            }

            // Portrait background
            if (portraitBackgroundImage != null)
            {
                portraitBackgroundImage.gameObject.SetActive(line.characterPortraitBackground != null);
                if (line.characterPortraitBackground != null)
                    portraitBackgroundImage.sprite = line.characterPortraitBackground;
            }

            continueIndicator?.SetActive(false);
            dialogueText.text = string.Empty;

            _skipRequested = false;

            // Type out
            _isTyping = true;
            dialogueText.text = string.Empty;
            yield return TypeText(line.text, line.typeSpeed);
            _isTyping = false;

            // Fully revealed – show continue indicator and wait for E
            continueIndicator?.SetActive(true);
            _skipRequested = false;
            _advanceRequested = false;

            yield return WaitForAdvance();

            continueIndicator?.SetActive(false);
        }

        /// <summary>
        /// Types out <paramref name="fullText"/> character by character, honouring
        /// special characters:
        /// <list type="bullet">
        ///   <item><term>|</term><description>Pause for <see cref="pauseDuration"/> s; nothing appended.</description></item>
        ///   <item><term>`</term><description>Append a space instantly (no delay).</description></item>
        /// </list>
        /// When skip is requested the entire visible string (special chars stripped /
        /// substituted) is shown immediately.
        /// </summary>
        private IEnumerator TypeText(string fullText, float speed)
        {
            // Pre-build the "fully revealed" string so skip can show it instantly.
            // '|' is removed (it's invisible timing), '`' becomes a regular space.
            string skipTarget = BuildSkipTarget(fullText);

            // We accumulate the visible text ourselves so we never show '|' or '`'.
            System.Text.StringBuilder visible = new System.Text.StringBuilder(fullText.Length);

            for (int i = 0; i < fullText.Length; i++)
            {
                if (_skipRequested)
                {
                    dialogueText.text = skipTarget;
                    yield break;
                }

                char c = fullText[i];

                if (c == PauseChar)
                {
                    // Don't append anything; just wait.
                    yield return new WaitForSeconds(pauseDuration);
                }
                else if (c == InstantSpaceChar)
                {
                    // Append a space with no delay at all.
                    visible.Append(' ');
                    dialogueText.text = visible.ToString();
                    // yield return null here so the frame still advances
                    // but there is no WaitForSeconds — effectively instant.
                    yield return null;
                }
                else
                {
                    visible.Append(c);
                    dialogueText.text = visible.ToString();
                    yield return new WaitForSeconds(speed);
                }
            }
        }

        /// <summary>
        /// Converts the raw authored string into the final fully-visible form:
        /// <c>|</c> is removed, <c>`</c> becomes a space.
        /// </summary>
        private static string BuildSkipTarget(string raw)
        {
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c == PauseChar)       continue;         // silent — drop it
                if (c == InstantSpaceChar) sb.Append(' ');  // substitute
                else                       sb.Append(c);
            }
            return sb.ToString();
        }

        private IEnumerator WaitForAdvance()
        {
            while (!_advanceRequested)
                yield return null;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(advanceKey)) return;

            if (_isTyping)
            {
                _skipRequested = true;
            }
            else
            {
                _advanceRequested = true;
            }
        }

        // ── Panel Fade ─────────────────────────────────────────────────────────

        private IEnumerator ShowPanel()
        {
            gameObject.SetActive(true);
            yield return Fade(0f, 1f);
        }

        private IEnumerator HidePanel()
        {
            yield return Fade(1f, 0f);
            gameObject.SetActive(false);
        }

        private IEnumerator Fade(float from, float to)
        {
            if (panelGroup == null) yield break;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                panelGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            panelGroup.alpha = to;
        }

        // ── Reset Portraits ────────────────────────────────────────────────────
        private void ResetPortraits()
        {
            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.gameObject.SetActive(false);
            }
            if (portraitBackgroundImage != null)
            {
                portraitBackgroundImage.sprite = null;
                portraitBackgroundImage.gameObject.SetActive(false);
            }

            if (nameText != null)
                nameText.text = string.Empty;

            nameplateGroup1?.SetActive(false);
            nameplateGroup2?.SetActive(false);
        }
    }
}
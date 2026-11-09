using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DialogueSystem.Data;
using System.Linq;

namespace DialogueSystem.UI
{
    /// <summary>
    /// Controls the Meta Dialogue panel (question + branching answers).
    ///
    /// ── Required UI Hierarchy ──────────────────────────────────────────────────
    ///
    ///  MetaDialoguePanel  [Canvas Group]
    ///    ├─ NameplateGroup  [GameObject]
    ///    │    ├─ NameText          [TMP_Text]
    ///    │    └─ PortraitImage     [Image]
    ///    ├─ QuestionText           [TMP_Text]
    ///    └─ AnswersContainer       [RectTransform + Horizontal Layout Group
    ///                               + Content Size Fitter (Horizontal: Preferred)]
    ///
    /// ── Answer Button Prefab ────────────────────────────────────────────────────
    ///
    ///  AnswerButton  [Button + Image (background) + Layout Element]
    ///    ├─ SelectionIndicator  [any Image / graphic — enabled when selected]
    ///    └─ AnswerText          [TMP_Text]
    ///
    /// Set "Flexible Width = 1" on the Layout Element so all buttons share space equally.
    /// </summary>
    public class MetaDialogueUI : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Panel")]
        [SerializeField] private CanvasGroup panelGroup;

        [Header("Nameplate")]
        [SerializeField] private GameObject nameplateGroup;
        [SerializeField] private TMP_Text   nameText;
        [SerializeField] private Image      portraitImage;

        [Header("Question")]
        [SerializeField] private TMP_Text questionText;

        [Header("Answers")]
        [SerializeField] private RectTransform answersContainer;
        [SerializeField] private GameObject    answerButtonPrefab;

        [Header("Input")]
        [SerializeField] private KeyCode confirmKey = KeyCode.E;
        [SerializeField] private List<KeyCode> leftKeys = new List<KeyCode> { KeyCode.LeftArrow, KeyCode.A };
        [SerializeField] private List<KeyCode> rightKeys = new List<KeyCode> { KeyCode.RightArrow, KeyCode.D };

        [Header("Appearance")]
        [SerializeField] private float fadeDuration   = 0.2f;
        [SerializeField] private Color selectedColor   = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color unselectedColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);

        // ── Runtime State ──────────────────────────────────────────────────────
        private readonly List<MetaAnswer>     _answers      = new List<MetaAnswer>();
        private readonly List<AnswerButtonRef> _buttonRefs  = new List<AnswerButtonRef>();
        private int  _selectedIndex;
        private bool _confirmed;

        // ── Per-Button Data ────────────────────────────────────────────────────
        private struct AnswerButtonRef
        {
            public Image      background;
            public GameObject indicator;
        }

        // ── Public Entry Point ─────────────────────────────────────────────────

        /// <summary>
        /// Displays the meta dialogue and calls <paramref name="onAnswerChosen"/> with
        /// the MetaAnswer the player confirmed.  Yield from DialogueManager.
        /// </summary>
        public IEnumerator RunMetaDialogue(MetaDialogueSO data, Action<MetaAnswer> onAnswerChosen)
        {
            yield return FadePanel(0f, 1f, show: true);

            ApplyNameplate(data.characterName, data.characterPortrait);

            // Question
            questionText.text = string.Empty;
            if (data.typeOutQuestion)
                yield return TypeText(questionText, data.questionText, data.typeSpeed);
            else
                questionText.text = data.questionText;

            // Answers
            SpawnAnswerButtons(data.answers);
            _selectedIndex = 0;
            _confirmed     = false;
            RefreshSelection();

            yield return WaitForConfirmation();

            var chosen = _answers[_selectedIndex];

            CleanupButtons();
            yield return FadePanel(1f, 0f, show: false);
            questionText.text = string.Empty;

            onAnswerChosen?.Invoke(chosen);
        }

        // ── Input ──────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_buttonRefs.Count == 0 || _confirmed) return;

            if (leftKeys.Any(k => Input.GetKeyDown(k)))
            {
                _selectedIndex = (_selectedIndex - 1 + _buttonRefs.Count) % _buttonRefs.Count;
                RefreshSelection();
            }
            else if (rightKeys.Any(k => Input.GetKeyDown(k)))
            {
                _selectedIndex = (_selectedIndex + 1) % _buttonRefs.Count;
                RefreshSelection();
            }
            else if (Input.GetKeyDown(confirmKey))
            {
                _confirmed = true;
            }
        }

        private IEnumerator WaitForConfirmation()
        {
            while (!_confirmed) yield return null;
        }

        // ── Button Lifecycle ───────────────────────────────────────────────────

        private void SpawnAnswerButtons(List<MetaAnswer> answers)
        {
            _answers.Clear();
            _buttonRefs.Clear();

            foreach (var answer in answers)
            {
                _answers.Add(answer);

                var go  = Instantiate(answerButtonPrefab, answersContainer);
                var lbl = go.GetComponentInChildren<TMP_Text>();
                var bg = go.transform.Find("ButtonBackground")?.GetComponent<Image>();
                var btn = go.GetComponent<Button>();

                if (lbl != null) lbl.text = answer.answerText;

                // Selection indicator: a child named "SelectionIndicator" (optional)
                Transform indicatorT = go.transform.Find("SelectionIndicator");
                var indicator = indicatorT != null ? indicatorT.gameObject : null;
                indicator?.SetActive(false);

                // Clicking a button also confirms that choice
                int capturedIndex = _answers.Count - 1;
                btn?.onClick.AddListener(() =>
                {
                    _selectedIndex = capturedIndex;
                    _confirmed     = true;
                });

                _buttonRefs.Add(new AnswerButtonRef { background = bg, indicator = indicator });
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(answersContainer);
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < _buttonRefs.Count; i++)
            {
                bool sel = i == _selectedIndex;
                _buttonRefs[i].indicator?.SetActive(sel);
                if (_buttonRefs[i].background != null)
                    _buttonRefs[i].background.color = sel ? selectedColor : unselectedColor;
            }
        }

        private void CleanupButtons()
        {
            foreach (Transform t in answersContainer)
                Destroy(t.gameObject);
            _buttonRefs.Clear();
            _answers.Clear();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ApplyNameplate(string charName, Sprite portrait)
        {
            bool hasName = !string.IsNullOrEmpty(charName);
            nameplateGroup?.SetActive(hasName);
            if (hasName && nameText != null) nameText.text = charName;

            if (portraitImage != null)
            {
                portraitImage.gameObject.SetActive(portrait != null);
                if (portrait != null) portraitImage.sprite = portrait;
            }
        }

        private IEnumerator TypeText(TMP_Text target, string full, float speed)
        {
            for (int i = 0; i <= full.Length; i++)
            {
                target.text = full.Substring(0, i);
                yield return new WaitForSeconds(speed);
            }
        }

        private IEnumerator FadePanel(float from, float to, bool show)
        {
            if (show) gameObject.SetActive(true);
            if (panelGroup == null) { if (!show) gameObject.SetActive(false); yield break; }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                panelGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            panelGroup.alpha = to;
            if (!show) gameObject.SetActive(false);
        }
    }
}
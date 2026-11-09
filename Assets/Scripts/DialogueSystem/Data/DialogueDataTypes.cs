using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Events;

namespace DialogueSystem.Data
{
    // ─────────────────────────────────────────────────────────────
    //  DIALOGUE LINE  (one "batch" of text spoken by one character)
    // ─────────────────────────────────────────────────────────────

    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("Name shown in the dialogue box. Leave empty to hide the nameplate.")]
        public string characterName;

        [Tooltip("Portrait sprite to display. Optional.")]
        public Sprite characterPortrait;
        public Sprite characterPortraitBackground;

        [TextArea(2, 6)]
        [Tooltip("The text batch to type out.")]
        public string text;

        [Tooltip("Seconds per character. Lower = faster typing.")]
        [Range(0.01f, 0.2f)]
        public float typeSpeed = 0.04f;

        [Tooltip("Events fired when this specific line starts.")]
        public DialogueEventGroup onLineStart;

        [Tooltip("Events fired when this specific line finishes (fully revealed).")]
        public DialogueEventGroup onLineEnd;
    }

    // ─────────────────────────────────────────────────────────────
    //  META DIALOGUE ANSWER
    // ─────────────────────────────────────────────────────────────

    [System.Serializable]
    public class MetaAnswer
    {
        [Tooltip("Text shown on the answer button.")]
        public string answerText;

        [Tooltip("Events triggered when this answer is selected.")]
        public DialogueEventGroup onSelected;

        [Tooltip("If set, a follow-up Normal Dialogue plays after this answer's events.")]
        public NormalDialogueSO followUpDialogue;
    }

    // ─────────────────────────────────────────────────────────────
    //  INTERACTION EVENT ENTRY  (one phase of an interaction point)
    // ─────────────────────────────────────────────────────────────

    public enum InteractionEventType
    {
        NormalDialogue,
        MetaDialogue,
        CustomEvent     // calls external code – no dialogue panel opened
    }

    [System.Serializable]
    public class InteractionEventEntry
    {
        public InteractionEventType eventType = InteractionEventType.NormalDialogue;

        [Tooltip("Used when eventType == NormalDialogue.")]
        public NormalDialogueSO normalDialogue;

        [Tooltip("Used when eventType == MetaDialogue.")]
        public MetaDialogueSO metaDialogue;

        [Tooltip("Used when eventType == CustomEvent. Fires immediately on interaction.")]
        public DialogueEventGroup customEvents;

        [Tooltip("Camera anchor Transform. The camera will be locked to look from here during this interaction.")]
        public Transform cameraAnchor;

        [Tooltip("Camera blend/lock duration in seconds.")]
        public float cameraLockDuration = 0.5f;
    }
}
using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Events;

namespace DialogueSystem.Data
{
    // ─────────────────────────────────────────────────────────────
    //  META DIALOGUE  (question + branching answers)
    // ─────────────────────────────────────────────────────────────

    [CreateAssetMenu(menuName = "Dialogue System/Meta Dialogue", fileName = "NewMetaDialogue")]
    public class MetaDialogueSO : ScriptableObject
    {
        [Tooltip("Optional character posing the question.")]
        public string characterName;

        [Tooltip("Optional portrait for the character posing the question.")]
        public Sprite characterPortrait;

        [TextArea(2, 5)]
        [Tooltip("The question text.")]
        public string questionText;

        [Tooltip("If true the question types out letter-by-letter; if false it appears instantly.")]
        public bool typeOutQuestion = true;

        [Range(0.01f, 0.2f)]
        public float typeSpeed = 0.04f;

        [Tooltip("At least 2 answers. Each can trigger independent events.")]
        public List<MetaAnswer> answers = new List<MetaAnswer>();

        [Tooltip("Events fired when this meta dialogue first opens.")]
        public DialogueEventGroup onDialogueOpen;
    }
}

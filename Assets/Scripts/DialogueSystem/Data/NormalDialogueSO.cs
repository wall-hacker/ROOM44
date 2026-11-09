using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Events;

namespace DialogueSystem.Data
{
    // ─────────────────────────────────────────────────────────────
    //  NORMAL DIALOGUE  (sequential lines)
    // ─────────────────────────────────────────────────────────────

    [CreateAssetMenu(menuName = "Dialogue System/Normal Dialogue", fileName = "NewNormalDialogue")]
    public class NormalDialogueSO : ScriptableObject
    {
        [Tooltip("All lines in order.")]
        public List<DialogueLine> lines = new List<DialogueLine>();

        [Tooltip("Events fired once when this entire dialogue finishes.")]
        public DialogueEventGroup onDialogueComplete;
    }
}

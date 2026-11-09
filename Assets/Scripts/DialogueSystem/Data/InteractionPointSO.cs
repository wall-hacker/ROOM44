using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Events;

namespace DialogueSystem.Data
{
    // ─────────────────────────────────────────────────────────────
    //  INTERACTION POINT DATA  (drives InteractionPoint component)
    // ─────────────────────────────────────────────────────────────

    [CreateAssetMenu(menuName = "Dialogue System/Interaction Point Data", fileName = "NewInteractionPoint")]
    public class InteractionPointSO : ScriptableObject
    {
        [Tooltip("Text displayed in the hint popup when the player is close.")]
        public string hintText = "Press E to interact";

        [Tooltip(
            "Ordered list of events. The interaction point walks through this list. " +
            "Set 'Loop Last Entry' to replay the final entry on every subsequent interaction.")]
        public List<InteractionEventEntry> eventSequence = new List<InteractionEventEntry>();

        [Tooltip("If true, the last entry in eventSequence repeats on every interaction after the list is exhausted.")]
        public bool loopLastEntry = true;
        
        [Tooltip("If true, a random entry from eventSequence is picked on each interaction instead of running in order.")]
        public bool randomizeEntry = false;

        [Tooltip("Optional events fired every time this interaction point is activated (before the main event).")]
        public DialogueEventGroup onActivated;

        [Tooltip("Optional events fired every time this interaction point finishes.")]
        public DialogueEventGroup onCompleted;
    }
}

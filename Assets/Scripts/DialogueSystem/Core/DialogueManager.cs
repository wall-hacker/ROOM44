using System;
using System.Collections;
using UnityEngine;
using DialogueSystem.Data;
using DialogueSystem.UI;

namespace DialogueSystem.Core
{
    /// <summary>
    /// Central singleton that orchestrates all dialogue flows.
    /// Attach to a persistent GameObject in your scene.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static DialogueManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Inspector References ───────────────────────────────────────────────
        [Header("UI Panels")]
        [SerializeField] private NormalDialogueUI normalDialogueUI;
        [SerializeField] private MetaDialogueUI   metaDialogueUI;

        [Header("Camera")]
        [SerializeField] public CameraController cameraController;

        [Header("Player")]
        [Tooltip("Assign the player controller so movement can be locked during dialogue.")]
        [SerializeField] public FirstPersonController playerController;

        // ── State ──────────────────────────────────────────────────────────────
        public bool IsDialogueActive { get; private set; }

        // Events the rest of the game can subscribe to
        public event Action OnAnyDialogueStart;
        public event Action OnAnyDialogueEnd;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Start a Normal Dialogue sequence.</summary>
        public void StartNormalDialogue(NormalDialogueSO data, Transform cameraAnchor = null, float camDuration = 0.5f)
        {
            if (IsDialogueActive) return;
            StartCoroutine(RunNormalDialogue(data, cameraAnchor, camDuration));
        }

        /// <summary>Start a Meta Dialogue (question + answers).</summary>
        public void StartMetaDialogue(MetaDialogueSO data, Transform cameraAnchor = null, float camDuration = 0.5f)
        {
            if (IsDialogueActive) return;
            StartCoroutine(RunMetaDialogue(data, cameraAnchor, camDuration));
        }

        /// <summary>Used by InteractionPoint – runs a full InteractionEventEntry.</summary>
        public void ExecuteInteractionEntry(InteractionEventEntry entry)
        {
            if (IsDialogueActive) return;

            switch (entry.eventType)
            {
                case InteractionEventType.NormalDialogue:
                    StartNormalDialogue(entry.normalDialogue, entry.cameraAnchor, entry.cameraLockDuration);
                    break;
                case InteractionEventType.MetaDialogue:
                    StartMetaDialogue(entry.metaDialogue, entry.cameraAnchor, entry.cameraLockDuration);
                    break;
                case InteractionEventType.CustomEvent:
                    StartCoroutine(RunCustomEvent(entry));
                    break;
            }
        }

        // ── Internal Flows ─────────────────────────────────────────────────────

        private IEnumerator RunNormalDialogue(NormalDialogueSO data, Transform cameraAnchor, float camDuration)
        {
            if (data == null) yield break;

            BeginDialogue(cameraAnchor, camDuration);

            yield return normalDialogueUI.RunDialogue(data);

            data.onDialogueComplete?.ExecuteAll();
            EndDialogue();
        }

        private IEnumerator RunMetaDialogue(MetaDialogueSO data, Transform cameraAnchor, float camDuration)
        {
            if (data == null) yield break;

            BeginDialogue(cameraAnchor, camDuration);
            data.onDialogueOpen?.ExecuteAll();

            MetaAnswer chosen = null;
            yield return metaDialogueUI.RunMetaDialogue(data, result => chosen = result);

            EndDialogue();

            if (chosen == null) yield break;

            // Fire the answer's events
            chosen.onSelected?.ExecuteAll();

            // Optional follow-up normal dialogue
            if (chosen.followUpDialogue != null)
                yield return RunNormalDialogue(chosen.followUpDialogue, cameraAnchor, camDuration);
        }

        private IEnumerator RunCustomEvent(InteractionEventEntry entry)
        {
            BeginDialogue(entry.cameraAnchor, entry.cameraLockDuration);
            entry.customEvents?.ExecuteAll();
            // Give events one frame to start, then release
            yield return null;
            EndDialogue();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void BeginDialogue(Transform cameraAnchor, float camDuration)
        {
            IsDialogueActive = true;
            SetPlayerMovement(false);
            if (cameraAnchor != null)
                cameraController?.LockToAnchor(cameraAnchor, camDuration);
            else
                cameraController?.LockToAnchor(cameraController.targetCamera.transform, 0.01f);
            OnAnyDialogueStart?.Invoke();
        }

        private void EndDialogue()
        {
            IsDialogueActive = false;
            // SetPlayerMovement(true);
            OnAnyDialogueEnd?.Invoke();
        }

        public void SetPlayerMovement(bool enabled)
        {
            if (playerController != null)
                playerController.enabled = enabled;
        }
    }
}
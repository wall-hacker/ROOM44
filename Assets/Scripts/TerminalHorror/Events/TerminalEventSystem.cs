// ============================================================
//  TerminalEventSystem.cs
//  Data-driven narrative event system.
//  Events fire on: command usage, file reads, dir entry,
//  elapsed time, player hesitation, failed commands.
//  Integrates with DialogueSystem.Events.DialogueEventGroup.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DialogueSystem.Events;

namespace TerminalHorror.Events
{
    // ─────────────────────────────────────────────────────────
    //  TERMINAL EVENT DEFINITION  (ScriptableObject)
    // ─────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "TerminalEvent", menuName = "Terminal Horror/Terminal Event")]
    public class TerminalEventSO : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Unique ID used by event triggers.")]
        public string eventId;

        [Header("Conditions")]
        [Tooltip("Only fires if ALL flags in this list are set.")]
        public List<string> requiredFlags = new();

        [Tooltip("Will not fire if ANY of these flags are set.")]
        public List<string> blockedByFlags = new();

        [Tooltip("How many times this event can fire. -1 = unlimited.")]
        public int maxFires = 1;

        [Header("Terminal Output")]
        [Tooltip("Lines printed to the terminal when this event fires.")]
        [TextArea(3, 10)]
        public List<string> outputLines = new();

        [Tooltip("Seconds to wait before printing each line (typewriter pacing).")]
        public float lineDelay = 0.05f;

        [Tooltip("If true, each output line is printed with a delay between them.")]
        public bool typewriterOutput = true;

        [Header("Filesystem Mutations")]
        [Tooltip("Files to add to the current directory when this event fires.")]
        public List<ScriptableObjects.FileDefinition> filesToAdd = new();

        [Tooltip("File names to remove from the current directory.")]
        public List<string> fileNamesToRemove = new();

        [Tooltip("Flags to set when this event fires.")]
        public List<string> flagsToSet = new();

        [Header("Dialogue System Events")]
        [Tooltip("Standard DialogueEventGroup, fires alongside terminal output.")]
        public DialogueEventGroup dialogueEvents;

        [Header("Corruption")]
        [Tooltip("Corruption intensity to apply when this event fires. 0 = no change.")]
        [Range(0f, 1f)]
        public float corruptionIntensityDelta = 0f;
    }

    // ─────────────────────────────────────────────────────────
    //  TIME-BASED TRIGGER DEFINITION
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class TimedEventTrigger
    {
        [Tooltip("Event ID to fire.")]
        public string eventId;

        [Tooltip("Seconds after game start to fire this event.")]
        public float triggerTime;

        [Tooltip("If true, fires repeatedly with this interval after the first trigger.")]
        public bool repeating;
        public float repeatInterval = 60f;

        [HideInInspector] public bool hasFired;
        [HideInInspector] public float nextFireTime;
    }

    // ─────────────────────────────────────────────────────────
    //  HESITATION TRIGGER
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class HesitationEventTrigger
    {
        [Tooltip("Seconds of player inactivity before firing.")]
        public float idleThreshold = 30f;

        [Tooltip("Event ID to fire on hesitation.")]
        public string eventId;

        [Tooltip("If true, this only fires once.")]
        public bool onceOnly = true;

        [HideInInspector] public bool hasFired;
    }

    // ─────────────────────────────────────────────────────────
    //  TERMINAL EVENT SYSTEM  (MonoBehaviour manager)
    // ─────────────────────────────────────────────────────────

    public class TerminalEventSystem : MonoBehaviour
    {
        [Header("Event Library")]
        [Tooltip("Drag all TerminalEventSO assets here.")]
        public List<TerminalEventSO> eventLibrary = new();

        [Header("Timed Events")]
        public List<TimedEventTrigger> timedEvents = new();

        [Header("Hesitation Detection")]
        public List<HesitationEventTrigger> hesitationTriggers = new();

        // Runtime state
        private readonly Dictionary<string, TerminalEventSO> _eventMap   = new();
        private readonly Dictionary<string, int>             _fireCounts = new();
        private readonly HashSet<string>                     _flags      = new();

        private Core.TerminalManager _terminal;
        private float _lastInputTime;
        private float _sessionTime;

        // Delegates for external hooks
        public event Action<string>              OnEventFired;
        public event Action<List<string>, float> OnTerminalOutputRequested;
        public event Action<float>               OnCorruptionIntensityChanged;

        // ─────────────────────────────────────────────────────

        private void Awake()
        {
            foreach (var so in eventLibrary)
                if (so != null && !string.IsNullOrEmpty(so.eventId))
                    _eventMap[so.eventId] = so;
        }

        private void Start()
        {
            _terminal      = GetComponent<Core.TerminalManager>()
                             ?? FindFirstObjectByType<Core.TerminalManager>();
            _lastInputTime = Time.time;
        }

        private void Update()
        {
            _sessionTime = Time.time;

            // Check timed events
            foreach (var t in timedEvents)
            {
                if (!t.hasFired && Time.time >= t.triggerTime)
                {
                    TriggerEvent(t.eventId);
                    t.hasFired     = true;
                    t.nextFireTime = Time.time + t.repeatInterval;
                }
                else if (t.hasFired && t.repeating && Time.time >= t.nextFireTime)
                {
                    TriggerEvent(t.eventId);
                    t.nextFireTime = Time.time + t.repeatInterval;
                }
            }

            // Check hesitation
            foreach (var h in hesitationTriggers)
            {
                if (h.hasFired && h.onceOnly) continue;
                if (Time.time - _lastInputTime >= h.idleThreshold)
                {
                    TriggerEvent(h.eventId);
                    h.hasFired     = true;
                    _lastInputTime = Time.time; // reset so it doesn't spam
                }
            }
        }

        // ─── Public API ──────────────────────────────────────

        /// <summary>
        /// Call this whenever the player provides any input,
        /// to reset hesitation timers.
        /// </summary>
        public void RegisterPlayerInput()
        {
            _lastInputTime = Time.time;
        }

        public void TriggerEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            if (!_eventMap.TryGetValue(eventId, out var ev))
            {
                Debug.LogWarning($"[TerminalEventSystem] Unknown event: '{eventId}'");
                return;
            }

            // Check required flags
            foreach (var f in ev.requiredFlags)
                if (!_flags.Contains(f)) return;

            // Check blocked flags
            foreach (var f in ev.blockedByFlags)
                if (_flags.Contains(f)) return;

            // Check max fires
            _fireCounts.TryGetValue(eventId, out int count);
            if (ev.maxFires >= 0 && count >= ev.maxFires) return;

            _fireCounts[eventId] = count + 1;

            // Set flags
            foreach (var f in ev.flagsToSet)
                _flags.Add(f);

            // Apply filesystem mutations
            ApplyFilesystemMutations(ev);

            // Print terminal output
            if (ev.outputLines.Count > 0)
                OnTerminalOutputRequested?.Invoke(ev.outputLines, ev.lineDelay);

            // Fire dialogue events
            ev.dialogueEvents?.ExecuteAll();

            // Corruption delta
            if (Mathf.Abs(ev.corruptionIntensityDelta) > 0.001f)
                OnCorruptionIntensityChanged?.Invoke(ev.corruptionIntensityDelta);

            OnEventFired?.Invoke(eventId);
        }

        public void SetFlag(string flag)   => _flags.Add(flag);
        public void ClearFlag(string flag) => _flags.Remove(flag);
        public bool HasFlag(string flag)   => _flags.Contains(flag);

        // ─── Internal ─────────────────────────────────────────

        private void ApplyFilesystemMutations(TerminalEventSO ev)
        {
            if (_terminal == null) return;
            var fs  = _terminal.FileSystem;
            var dir = fs.CurrentDirectory;

            foreach (var fileDef in ev.filesToAdd)
            {
                var file = new Filesystem.VirtualFile(fileDef.name, fileDef.fileType)
                {
                    permissions       = fileDef.permissions,
                    onReadEventId     = fileDef.onReadEventId,
                    progressiveReveal = fileDef.progressiveReveal,
                    revealedLineCount = fileDef.revealedLineCount
                };
                file.contentLines.AddRange(fileDef.contentLines);
                dir.AddFile(file);
            }

            foreach (var name in ev.fileNamesToRemove)
                dir.RemoveFile(name);
        }

        public IReadOnlyDictionary<string, int> GetFireCounts()
            => _fireCounts;

        public IReadOnlyCollection<string> GetFlags()
            => _flags;
    }
}
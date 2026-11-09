// ============================================================
//  CorruptionSystem.cs
//  Modular psychological horror corruption effects.
//  Intensity-driven system controlling:
//   - text glitching
//   - cursor hijack
//   - fake outputs
//   - command hallucinations
//   - ghost messages
//   - fake crashes / reboots
//   - delayed responses
//   - input overwriting
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TerminalHorror.Corruption
{
    // ─────────────────────────────────────────────────────────
    //  CORRUPTION EFFECT TYPES
    // ─────────────────────────────────────────────────────────

    [Flags]
    public enum CorruptionEffectFlags
    {
        None              = 0,
        TextGlitch        = 1 << 0,
        CursorHijack      = 1 << 1,
        FakeOutput        = 1 << 2,
        CommandHallucination = 1 << 3,
        GhostMessage      = 1 << 4,
        FakeCrash         = 1 << 5,
        DelayedResponse   = 1 << 6,
        InputOverwrite    = 1 << 7,
        TerminalTalk      = 1 << 8
    }

    // ─────────────────────────────────────────────────────────
    //  CORRUPTION SETTINGS  (ScriptableObject)
    // ─────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "CorruptionSettings", menuName = "Terminal Horror/Corruption Settings")]
    public class CorruptionSettingsSO : ScriptableObject
    {
        [Header("Global Control")]
        [Range(0f, 1f)]
        [Tooltip("0 = no corruption, 1 = maximum horror.")]
        public float baseIntensity = 0f;

        [Tooltip("Which effects are enabled at all.")]
        public CorruptionEffectFlags enabledEffects = CorruptionEffectFlags.TextGlitch
            | CorruptionEffectFlags.GhostMessage | CorruptionEffectFlags.TerminalTalk;

        [Header("Text Glitch")]
        [Tooltip("Fraction of characters corrupted at intensity 1.0")]
        [Range(0f, 1f)] public float maxGlitchFraction = 0.25f;

        [Header("Ghost Messages")]
        [Tooltip("Pool of ghost messages inserted randomly into outputs.")]
        public List<string> ghostMessages = new()
        {
            "<color=#330000>help me</color>",
            "<color=#330000>don't read this</color>",
            "<color=#330000>you found it</color>",
            "<color=#330000>it knows you're here</color>",
            "<color=#220022>she never left</color>",
            "<color=#002222>i can see you</color>",
        };

        [Header("Command Hallucinations")]
        [Tooltip("Commands that can silently replace what the player typed.")]
        public List<string> hallucinationCommands = new()
        {
            "leave",
            "run",
            "help",
            "why",
            "stop",
            "don't"
        };

        [Header("Terminal Talk Lines")]
        [Tooltip("Things the terminal 'says' to the player.")]
        public List<string> terminalTalkLines = new()
        {
            "I've been waiting.",
            "Why did you come back?",
            "You already know what this is.",
            "There is no safe directory.",
            "You can't clear this.",
            "Type anything. I'll answer.",
            "ls won't help you now.",
            "Run.",
        };

        [Header("Fake Crash Messages")]
        public List<string> fakeCrashLines = new()
        {
            "Segmentation fault (core dumped)",
            "KERNEL PANIC - not syncing: Fatal exception",
            "BUG: soft lockup - CPU#0 stuck",
            "Oops: general protection fault",
            "[ 0.000000] Calibrating delay loop...",
            "FATAL ERROR: heap corruption detected",
        };

        [Header("Delays")]
        [Range(0f, 5f)] public float maxResponseDelay = 3f;

        [Header("Probabilities at Max Intensity")]
        [Range(0f, 1f)] public float ghostMessageProbability   = 0.08f;
        [Range(0f, 1f)] public float inputOverwriteProbability = 0.05f;
        [Range(0f, 1f)] public float fakeCrashProbability      = 0.02f;
        [Range(0f, 1f)] public float hallucinationProbability  = 0.04f;
        [Range(0f, 1f)] public float terminalTalkProbability   = 0.06f;
    }

    // ─────────────────────────────────────────────────────────
    //  CORRUPTION SYSTEM  (MonoBehaviour)
    // ─────────────────────────────────────────────────────────

    public class CorruptionSystem : MonoBehaviour
    {
        [Header("Settings")]
        public CorruptionSettingsSO settings;

        [Header("Runtime Intensity")]
        [Range(0f, 1f)] public float currentIntensity = 0f;

        // Callbacks wired by TerminalManager
        public event Action<List<string>>       OnGhostMessageReady;
        public event Action<string, string>     OnInputOverwritten;   // (original, replacement)
        public event Action<List<string>>       OnFakeCrashSequence;
        public event Action<float>              OnDelayedResponseRequested; // seconds

        private readonly System.Random _rng = new();
        private static readonly char[] GlitchChars =
            { '█', '▓', '▒', '░', '■', '▪', '●', '◌', '◊', '¿', '¡', '§', '¶', '†' };

        // ─── Public API ──────────────────────────────────────

        public void AdjustIntensity(float delta)
        {
            currentIntensity = Mathf.Clamp01(currentIntensity + delta);
        }

        public void SetIntensity(float value)
        {
            currentIntensity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Corrupts a string based on current intensity.
        /// </summary>
        public string GlitchText(string input)
        {
            if (!IsEffectEnabled(CorruptionEffectFlags.TextGlitch)) return input;

            float glitchFraction = settings.maxGlitchFraction * currentIntensity;
            if (glitchFraction < 0.001f) return input;

            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c != '\n' && _rng.NextDouble() < glitchFraction)
                    sb.Append($"<color=#ff4444>{GlitchChars[_rng.Next(GlitchChars.Length)]}</color>");
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Roll all probability-based effects. Call once per command execution.
        /// </summary>
        public void RollEffects(string originalInput)
        {
            if (currentIntensity < 0.01f) return;

            float i = currentIntensity;

            if (IsEffectEnabled(CorruptionEffectFlags.GhostMessage)
                && Roll(settings.ghostMessageProbability * i))
                EmitGhostMessage();

            if (IsEffectEnabled(CorruptionEffectFlags.InputOverwrite)
                && Roll(settings.inputOverwriteProbability * i))
                EmitInputOverwrite(originalInput);

            if (IsEffectEnabled(CorruptionEffectFlags.FakeCrash)
                && Roll(settings.fakeCrashProbability * i))
                StartCoroutine(EmitFakeCrash());

            if (IsEffectEnabled(CorruptionEffectFlags.TerminalTalk)
                && Roll(settings.terminalTalkProbability * i))
                EmitTerminalTalk();

            if (IsEffectEnabled(CorruptionEffectFlags.DelayedResponse)
                && Roll(currentIntensity * 0.3f))
                OnDelayedResponseRequested?.Invoke(_rng.NextDouble() < 0.5
                    ? settings.maxResponseDelay * (float)_rng.NextDouble()
                    : 0f);
        }

        /// <summary>
        /// If hallucination is active, may return a different command string.
        /// Returns null if no hallucination.
        /// </summary>
        public string TryHallucinateInput(string input)
        {
            if (!IsEffectEnabled(CorruptionEffectFlags.CommandHallucination)) return null;
            if (!Roll(settings.hallucinationProbability * currentIntensity)) return null;
            if (settings.hallucinationCommands.Count == 0) return null;

            return settings.hallucinationCommands[_rng.Next(settings.hallucinationCommands.Count)];
        }

        /// <summary>
        /// Trigger a fake reboot sequence coroutine.
        /// </summary>
        public void TriggerFakeReboot()
        {
            StartCoroutine(FakeRebootSequence());
        }

        // ─── Internals ────────────────────────────────────────

        private bool IsEffectEnabled(CorruptionEffectFlags flag) =>
            settings != null && (settings.enabledEffects & flag) != 0;

        private bool Roll(float probability) =>
            _rng.NextDouble() < probability;

        private void EmitGhostMessage()
        {
            if (settings.ghostMessages.Count == 0) return;
            string msg = settings.ghostMessages[_rng.Next(settings.ghostMessages.Count)];
            OnGhostMessageReady?.Invoke(new List<string> { msg });
        }

        private void EmitTerminalTalk()
        {
            if (settings.terminalTalkLines.Count == 0) return;
            string line = settings.terminalTalkLines[_rng.Next(settings.terminalTalkLines.Count)];
            OnGhostMessageReady?.Invoke(new List<string>
                { $"<color=#ff2222>{line}</color>" });
        }

        private void EmitInputOverwrite(string original)
        {
            if (settings.hallucinationCommands.Count == 0) return;
            string replacement = settings.hallucinationCommands[
                _rng.Next(settings.hallucinationCommands.Count)];
            OnInputOverwritten?.Invoke(original, replacement);
        }

        private IEnumerator EmitFakeCrash()
        {
            var lines = new List<string>();
            int count = _rng.Next(2, Mathf.Min(settings.fakeCrashLines.Count, 5));
            for (int k = 0; k < count; k++)
                lines.Add($"<color=#ff4444>{settings.fakeCrashLines[_rng.Next(settings.fakeCrashLines.Count)]}</color>");

            OnFakeCrashSequence?.Invoke(lines);

            // Pause briefly then resume
            float pause = 1.5f + (float)_rng.NextDouble() * 2f;
            yield return new WaitForSeconds(pause);

            // Recovery line
            OnGhostMessageReady?.Invoke(new List<string>
                { "<color=#aaaaaa>System recovered. Continuing...</color>" });
        }

        private IEnumerator FakeRebootSequence()
        {
            var lines = new List<string>
            {
                "<color=#ff4444>CRITICAL ERROR: filesystem panic</color>",
                "<color=#ff4444>Attempting emergency reboot...</color>",
                "",
                "[ 0.000001] Kernel loaded",
                "[ 0.000002] Memory initialised: 16384KB",
                "[ 0.000003] Mounting filesystems...",
                "[ 0.000004] WARNING: fsck failed on /dev/sda1",
                "[ 0.000005] Continuing in degraded mode",
                "",
                "<color=#ffdd88>Welcome back.</color>"
            };

            OnFakeCrashSequence?.Invoke(lines);
            yield return new WaitForSeconds(3f);
        }
    }
}
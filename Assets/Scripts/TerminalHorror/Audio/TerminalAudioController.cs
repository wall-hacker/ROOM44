// ============================================================
//  TerminalAudioController.cs
//  Audio event hooks for the terminal simulation.
//  Exposes methods for: typing, beeps, static, ambient,
//  distant noises, jumpscare audio.
//  Wire AudioSources and clips via the Inspector.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerminalHorror.Audio
{
    public class TerminalAudioController : MonoBehaviour
    {
        [Header("Audio Sources")]
        [Tooltip("Plays short, per-keystroke sounds.")]
        public AudioSource keySoundSource;

        [Tooltip("Plays command confirmation / error beeps.")]
        public AudioSource beepSource;

        [Tooltip("Plays looping ambient atmosphere.")]
        public AudioSource ambientSource;

        [Tooltip("Plays one-shot horror stings (jumpscares, distant noise).")]
        public AudioSource horrorSource;

        [Tooltip("Plays static / glitch sounds.")]
        public AudioSource staticSource;

        [Header("Key Sound Clips")]
        [Tooltip("Multiple clips for key variety. A random one plays each press.")]
        public List<AudioClip> keyClips = new();

        [Header("Beep Clips")]
        public AudioClip successBeep;
        public AudioClip errorBeep;
        public AudioClip corruptBeep;

        [Header("Static Clips")]
        public List<AudioClip> staticClips = new();

        [Header("Horror Stings")]
        public List<AudioClip> horrorStings = new();
        public List<AudioClip> distantNoises = new();

        [Header("Ambient Layers")]
        public List<AudioClip> ambientLoops = new();

        [Header("Settings")]
        [Range(0f, 1f)] public float keyVolume         = 0.35f;
        [Range(0f, 1f)] public float beepVolume        = 0.6f;
        [Range(0f, 1f)] public float staticVolume      = 0.4f;
        [Range(0f, 1f)] public float horrorVolume      = 0.8f;
        [Range(0f, 1f)] public float ambientVolume     = 0.5f;

        [Tooltip("Minimum random pitch variation on key sounds.")]
        [Range(0.8f, 1f)]  public float keyPitchMin = 0.95f;
        [Range(1f,  1.2f)] public float keyPitchMax = 1.05f;

        private readonly System.Random _rng = new();
        private int _ambientIndex = 0;

        // ─── Lifecycle ──────────────────────────────────────

        private void Start()
        {
            if (ambientSource != null && ambientLoops.Count > 0)
                PlayAmbient(ambientLoops[0]);
        }

        // ─── Public API ──────────────────────────────────────

        /// <summary>Called every keystroke.</summary>
        public void PlayKeySound()
        {
            if (keySoundSource == null || keyClips.Count == 0) return;
            var clip = keyClips[_rng.Next(keyClips.Count)];
            keySoundSource.pitch  = UnityEngine.Random.Range(keyPitchMin, keyPitchMax);
            keySoundSource.PlayOneShot(clip, keyVolume);
        }

        /// <summary>
        /// Plays appropriate feedback beep after command execution.
        /// </summary>
        public void PlayCommandSound(bool isError)
        {
            if (beepSource == null) return;
            var clip = isError ? errorBeep : successBeep;
            if (clip != null)
                beepSource.PlayOneShot(clip, beepVolume);
        }

        /// <summary>Plays a corruption/glitch beep.</summary>
        public void PlayCorruptSound()
        {
            if (beepSource != null && corruptBeep != null)
                beepSource.PlayOneShot(corruptBeep, beepVolume);
        }

        /// <summary>Plays a random static hit.</summary>
        public void PlayStaticBurst()
        {
            if (staticSource == null || staticClips.Count == 0) return;
            var clip = staticClips[_rng.Next(staticClips.Count)];
            staticSource.PlayOneShot(clip, staticVolume);
        }

        /// <summary>Plays a random horror sting.</summary>
        public void PlayHorrorSting()
        {
            if (horrorSource == null || horrorStings.Count == 0) return;
            var clip = horrorStings[_rng.Next(horrorStings.Count)];
            horrorSource.PlayOneShot(clip, horrorVolume);
        }

        /// <summary>Plays a random distant noise.</summary>
        public void PlayDistantNoise()
        {
            if (horrorSource == null || distantNoises.Count == 0) return;
            var clip = distantNoises[_rng.Next(distantNoises.Count)];
            horrorSource.PlayOneShot(clip, horrorVolume * 0.6f);
        }

        /// <summary>
        /// Switch the ambient loop to a different track with a fade.
        /// </summary>
        public void ChangeAmbient(AudioClip newClip, float fadeDuration = 2f)
        {
            StartCoroutine(FadeAmbient(newClip, fadeDuration));
        }

        private void PlayAmbient(AudioClip clip)
        {
            if (ambientSource == null || clip == null) return;
            ambientSource.clip   = clip;
            ambientSource.loop   = true;
            ambientSource.volume = ambientVolume;
            ambientSource.Play();
        }

        private IEnumerator FadeAmbient(AudioClip newClip, float duration)
        {
            float elapsed = 0f;
            float startVol = ambientSource != null ? ambientSource.volume : 0f;

            while (elapsed < duration * 0.5f)
            {
                if (ambientSource != null)
                    ambientSource.volume = Mathf.Lerp(startVol, 0f, elapsed / (duration * 0.5f));
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (ambientSource != null)
            {
                ambientSource.Stop();
                ambientSource.clip   = newClip;
                ambientSource.loop   = true;
                ambientSource.Play();
            }

            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                if (ambientSource != null)
                    ambientSource.volume = Mathf.Lerp(0f, ambientVolume, elapsed / (duration * 0.5f));
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (ambientSource != null)
                ambientSource.volume = ambientVolume;
        }
    }
}
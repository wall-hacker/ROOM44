using UnityEngine;
using UnityEngine.Events;
using System;

namespace DialogueSystem.Events
{
    // ─────────────────────────────────────────────────────────────
    //  BASE EVENT
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public abstract class DialogueEventBase
    {
        [Tooltip("Optional delay in seconds before this event fires.")]
        public float delay = 0f;

        public abstract void Execute();
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores a GameObject name and resolves it once at runtime, then caches the result.
    /// Use this in place of any scene-object reference field.
    /// </summary>
    [Serializable]
    public class SceneObjectRef<T> where T : Component
    {
        [Tooltip("The name of the GameObject in the scene.")]
        public string objectName;

        [NonSerialized] private T _cached;
        [NonSerialized] private bool _searched;

        public T Resolve()
        {
            if (_searched) return _cached;
            _searched = true;

            if (string.IsNullOrEmpty(objectName)) return null;

            foreach (var go in UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name != objectName) continue;

                _cached = go.GetComponent<T>();
                if (_cached == null)
                    Debug.LogWarning($"[DialogueEvent] GameObject '{objectName}' has no {typeof(T).Name} component.");

                return _cached;
            }

            Debug.LogWarning($"[DialogueEvent] GameObject '{objectName}' not found in scene.");
            return null;
        }

        public void Invalidate() { _cached = null; _searched = false; }
    }

    [Serializable]
    public class SceneGameObjectRef
    {
        [Tooltip("The name of the GameObject in the scene.")]
        public string objectName;

        [NonSerialized] private GameObject _cached;
        [NonSerialized] private bool _searched;

        public GameObject Resolve()
        {
            if (_searched) return _cached;
            _searched = true;

            if (string.IsNullOrEmpty(objectName)) return null;

            foreach (var go in UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name != objectName) continue;
                _cached = go.gameObject;
                return _cached;
            }

            Debug.LogWarning($"[DialogueEvent] GameObject '{objectName}' not found in scene.");
            return null;
        }

        public void Invalidate() { _cached = null; _searched = false; }
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILT-IN EVENT TYPES
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class UnityDialogueEvent : DialogueEventBase
    {
        public UnityEvent onExecute;
        public override void Execute() => onExecute?.Invoke();
    }

    /// <summary>Plays an AudioClip through a named AudioSource in the scene.</summary>
    [Serializable]
    public class PlaySoundEvent : DialogueEventBase
    {
        public SceneObjectRef<AudioSource> audioSource = new();
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;

        public override void Execute()
        {
            var source = audioSource.Resolve();
            if (source != null && clip != null)
                source.PlayOneShot(clip, volume);
        }
    }

    /// <summary>Triggers an Animator parameter on a named GameObject.</summary>
    [Serializable]
    public class AnimatorEvent : DialogueEventBase
    {
        public SceneObjectRef<Animator> animator = new();
        public enum ParamType { Trigger, Bool, Int, Float }
        public ParamType paramType = ParamType.Trigger;
        public string paramName;
        public bool  boolValue;
        public int   intValue;
        public float floatValue;

        public override void Execute()
        {
            var anim = animator.Resolve();
            if (anim == null || string.IsNullOrEmpty(paramName)) return;
            switch (paramType)
            {
                case ParamType.Trigger: anim.SetTrigger(paramName);          break;
                case ParamType.Bool:    anim.SetBool(paramName, boolValue);  break;
                case ParamType.Int:     anim.SetInteger(paramName, intValue); break;
                case ParamType.Float:   anim.SetFloat(paramName, floatValue); break;
            }
        }
    }

    /// <summary>Moves a named Transform to a target position/rotation over time.</summary>
    [Serializable]
    public class MoveObjectEvent : DialogueEventBase
    {
        public SceneObjectRef<Transform> target      = new();
        public SceneObjectRef<Transform> destination = new();

        public enum PositionMode
        {
            WorldAbsolute,   // move to this world-space position
            WorldAdditive,   // offset from current world-space position
            LocalAbsolute,   // set local position directly
            LocalAdditive,    // offset from current local position
            None             // ignore position, only rotate
        }

        public enum RotationMode
        {
            WorldAbsolute,   // set world-space euler rotation
            WorldAdditive,   // add to current world-space euler rotation
            LocalAbsolute,   // set local euler rotation
            LocalAdditive,    // add to current local euler rotation
            None             // ignore rotation, only move
        }

        [Tooltip("How the target position is interpreted.")]
        public PositionMode positionMode = PositionMode.WorldAbsolute;
        [Tooltip("How the target rotation is interpreted.")]
        public RotationMode rotationMode = RotationMode.WorldAbsolute;

        [Tooltip("Used when no Destination transform is set.")]
        public Vector3 position;
        [Tooltip("Used when no Destination transform is set.")]
        public Vector3 rotationEuler;

        public float duration = 1f;

        [Tooltip("When set, position and rotation are taken from this transform instead.\n" +
                "Position/Rotation Mode still controls how they are applied to the target.")]
        public bool useDestinationTransform = true;

        public override void Execute()
        {
            var t = target.Resolve();
            if (t == null) return;

            var dest = destination.Resolve();

            // ── Resolve raw values ────────────────────────────────────────────
            Vector3 rawPos = useDestinationTransform && dest ? dest.position      : position;
            Vector3 rawRot = useDestinationTransform && dest ? dest.eulerAngles   : rotationEuler;

            // ── Convert to final world position ──────────────────────────────
            Vector3 endPos = positionMode switch
            {
                PositionMode.WorldAbsolute => rawPos,
                PositionMode.WorldAdditive => t.position + rawPos,
                PositionMode.LocalAbsolute => t.parent != null
                                                ? t.parent.TransformPoint(rawPos)
                                                : rawPos,
                PositionMode.LocalAdditive => t.parent != null
                                                ? t.parent.TransformPoint(t.localPosition + rawPos)
                                                : t.position + rawPos,
                PositionMode.None => t.position,
                _ => rawPos
            };

            // ── Convert to final world rotation ──────────────────────────────
            Quaternion endRot = rotationMode switch
            {
                RotationMode.WorldAbsolute => Quaternion.Euler(rawRot),
                RotationMode.WorldAdditive => Quaternion.Euler(t.eulerAngles + rawRot),
                RotationMode.LocalAbsolute => t.parent != null
                                                ? t.parent.rotation * Quaternion.Euler(rawRot)
                                                : Quaternion.Euler(rawRot),
                RotationMode.LocalAdditive => t.parent != null
                                                ? t.parent.rotation * Quaternion.Euler(t.localEulerAngles + rawRot)
                                                : Quaternion.Euler(t.eulerAngles + rawRot),
                RotationMode.None => t.rotation,
                _ => Quaternion.Euler(rawRot)
            };

            if (Application.isPlaying)
                DialogueSystem.Core.DialogueManager.Instance?.StartCoroutine(MoveCoroutine(t, endPos, endRot));
        }

        private System.Collections.IEnumerator MoveCoroutine(Transform t, Vector3 endPos, Quaternion endRot)
        {
            float      elapsed  = 0f;
            Vector3    startPos = t.position;
            Quaternion startRot = t.rotation;

            while (elapsed < duration)
            {
                float pct  = elapsed / duration;
                if (positionMode != PositionMode.None)
                    t.position = Vector3.Lerp(startPos, endPos, pct);
                if (rotationMode != RotationMode.None)
                    t.rotation = Quaternion.Lerp(startRot, endRot, pct);
                elapsed   += Time.deltaTime;
                yield return null;
            }

            if (positionMode != PositionMode.None)
                t.position = endPos;
            if (rotationMode != RotationMode.None)
                t.rotation = endRot;
        }
    }

    /// <summary>Changes a named Light's colour and/or intensity.</summary>
    [Serializable]
    public class ChangeLightEvent : DialogueEventBase
    {
        public SceneObjectRef<Light> light = new();
        public Color targetColor     = Color.white;
        public float targetIntensity = 1f;
        public float duration        = 0.5f;

        public override void Execute()
        {
            if (light.Resolve() == null) return;
            if (Application.isPlaying)
                DialogueSystem.Core.DialogueManager.Instance?.StartCoroutine(LightCoroutine());
        }

        private System.Collections.IEnumerator LightCoroutine()
        {
            var  l               = light.Resolve();
            float elapsed        = 0f;
            Color startColor     = l.color;
            float startIntensity = l.intensity;
            while (elapsed < duration)
            {
                float t      = elapsed / duration;
                l.color      = Color.Lerp(startColor, targetColor, t);
                l.intensity  = Mathf.Lerp(startIntensity, targetIntensity, t);
                elapsed     += Time.deltaTime;
                yield return null;
            }
            l.color     = targetColor;
            l.intensity = targetIntensity;
        }
    }

    [Serializable]
    public class ChangeLighting : DialogueEventBase
    {
        public Color targetAmbientColor = Color.white;
        public float targetAmbientIntensity = 1f;
        public float duration = 0.5f;

        public override void Execute()
        {
            if (Application.isPlaying)
                DialogueSystem.Core.DialogueManager.Instance?.StartCoroutine(LightingCoroutine());
        }

        private System.Collections.IEnumerator LightingCoroutine()
        {
            float elapsed = 0f;
            Color startColor = RenderSettings.ambientLight;
            float startIntensity = RenderSettings.ambientIntensity;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                RenderSettings.ambientLight = Color.Lerp(startColor, targetAmbientColor, t);
                RenderSettings.ambientIntensity = Mathf.Lerp(startIntensity, targetAmbientIntensity, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            RenderSettings.ambientLight = targetAmbientColor;
            RenderSettings.ambientIntensity = targetAmbientIntensity;
        }
    }

    [Serializable]
    public class CameraAnchorEvent : DialogueEventBase
    {
        public SceneObjectRef<Transform> anchor = new();
        public float blendDuration = 0.5f;
        public bool lockCamera = true;
        public bool hasParent = false;

        public override void Execute()
        {
            var cam = DialogueSystem.Core.DialogueManager.Instance?.cameraController;
            if (cam == null) return;

            if (lockCamera)
            {
                // Mid-dialogue re-point — does NOT overwrite the saved restore position
                cam.ReAnchor(anchor.Resolve(), blendDuration, hasParent);
            }
            else
            {
                // Explicit unlock — happens at the END of post-dialogue events
                cam.Unlock(anchor.Resolve(), blendDuration, hasParent);
            }
        }
    }

    /// <summary>Enables or disables a named GameObject.</summary>
    [Serializable]
    public class SetActiveEvent : DialogueEventBase
    {
        public SceneGameObjectRef target = new();
        public bool active = true;

        public override void Execute() => target.Resolve()?.SetActive(active);
    }

    /// <summary>Loads a Unity scene by name.</summary>
    [Serializable]
    public class LoadSceneEvent : DialogueEventBase
    {
        public string sceneName;
        public override void Execute()
        {
            if (!string.IsNullOrEmpty(sceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  GROUP
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class DialogueEventGroup
    {
        [SerializeReference]
        [Tooltip("Add any DialogueEventBase subclass here.")]
        public System.Collections.Generic.List<DialogueEventBase> events
            = new System.Collections.Generic.List<DialogueEventBase>();

        public void ExecuteAll()
        {
            foreach (var e in events)
            {
                if (e == null) continue;
                if (e.delay > 0f && Application.isPlaying)
                    DialogueSystem.Core.DialogueManager.Instance?.StartCoroutine(DelayedExecute(e));
                else
                    e.Execute();
            }
        }

        private System.Collections.IEnumerator DelayedExecute(DialogueEventBase e)
        {
            yield return new WaitForSeconds(e.delay);
            e.Execute();
        }
    }
}
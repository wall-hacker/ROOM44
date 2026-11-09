using System.Collections;
using UnityEngine;
using TMPro;
using Debug = UnityEngine.Debug;

namespace DialogueSystem.UI
{
    // ─────────────────────────────────────────────────────────────
    //  CAMERA CONTROLLER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly locks the main camera to an anchor Transform during interactions
    /// and restores it when the interaction ends.
    ///
    /// Attach to the same GameObject as DialogueManager (or any persistent object).
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Reference")]
        [Tooltip("Leave null to auto-find Camera.main.")]
        [SerializeField] public Camera targetCamera;

        [Header("Return Blend")]
        [Tooltip("Duration in seconds to smoothly return to the free-look camera after unlock.")]
        [SerializeField] private float returnDuration = 0.4f;

        // Stored free-look state
        private Transform  _cameraTransform;
        private Vector3    _savedPosition;
        private Quaternion _savedRotation;
        private Transform  _savedParent;
        public  bool       IsLocked { get; private set; }

        private Coroutine _blendCoroutine;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null) _cameraTransform = targetCamera.transform;
        }

        /// <summary>
        /// Called ONCE by DialogueManager at dialogue start.
        /// Saves the restore point, then blends to the anchor.
        /// </summary>
        public void LockToAnchor(Transform anchor, float blendDuration)
        {
            if (_cameraTransform == null) return;

            // Save restore point only on first lock
            if (!IsLocked)
            {
                _savedPosition  = _cameraTransform.position;
                _savedRotation  = _cameraTransform.rotation;
                _savedParent    = _cameraTransform.parent;
                // _cameraTransform.SetParent(null, true);
                IsLocked = true;
            }

            BlendTo(anchor.position, anchor.rotation, blendDuration, true);
        }

        /// <summary>
        /// Called by CameraAnchorEvent DURING a dialogue.
        /// Re-points the camera without touching the saved restore point.
        /// </summary>
        public void ReAnchor(Transform anchor, float blendDuration, bool hasParent = false)
        {
            if (_cameraTransform == null || anchor == null) return;
            
            if (hasParent)
            {
                BlendTo(anchor.position, anchor.rotation, blendDuration, hasParent);
                _cameraTransform.SetParent(anchor, true);
            }  
            else
            {
                _cameraTransform.SetParent(null, true);
                BlendTo(anchor.position, anchor.rotation, blendDuration);
            }
        }

        /// <summary>
        /// Called ONCE after all post-dialogue events are done.
        /// Restores the camera to where it was before the dialogue.
        /// Pass a custom restoreTarget if your camera is a follow-cam
        /// and the character has moved (so it snaps to the right follow position).
        /// </summary>
        public void Unlock(Transform restoreTarget = null, float blendDuration = 0.4f, bool hasParent = false)
        {
            if (!IsLocked || _cameraTransform == null) return;
            IsLocked = false;
            var fps = DialogueSystem.Core.DialogueManager.Instance?.playerController;

            // if (blendDuration <= 0f)
            // {
            //     // if (restoreTarget != null)
            //     // {
            //     //     _cameraTransform.position = restoreTarget.position;
            //     //     _cameraTransform.rotation = restoreTarget.rotation;
            //     // }
            //     // else
            //     // {
            //     //     _cameraTransform.position = _savedPosition;
            //     //     _cameraTransform.rotation = _savedRotation;
            //     // }
                
            //     fps?.SyncCameraState();
            //     DialogueSystem.Core.DialogueManager.Instance?.SetPlayerMovement(true);
            //     return;
            // }

            Vector3    restorePos = restoreTarget != null ? restoreTarget.position : _savedPosition;
            Quaternion restoreRot = restoreTarget != null ? restoreTarget.rotation : _savedRotation;

            // Debug.Log($"Unlocking camera. Restoring to {restorePos} / {restoreRot.eulerAngles}");

            BlendTo(restorePos, restoreRot, returnDuration, hasParent);
            _cameraTransform.SetParent(_savedParent, true);

            fps?.SyncCameraState();
            DialogueSystem.Core.DialogueManager.Instance?.SetPlayerMovement(true);
        }

        private void BlendTo(Vector3 endPos, Quaternion endRot, float duration, bool hasParent = false)
        {
            if (_blendCoroutine != null) StopCoroutine(_blendCoroutine);
            _blendCoroutine = StartCoroutine(BlendCoroutine(endPos, endRot, duration, hasParent));
        }

        private IEnumerator BlendCoroutine(Vector3 endPos, Quaternion endRot, float duration, bool hasParent = false)
        {
            float      elapsed  = 0f;
            Vector3    startPos = _cameraTransform.position;
            Quaternion startRot = _cameraTransform.rotation;

            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _cameraTransform.position = Vector3.Lerp(startPos, endPos, t);
                _cameraTransform.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _cameraTransform.position = endPos;
            _cameraTransform.rotation = endRot;
            if (hasParent)
            {
                _cameraTransform.localPosition = Vector3.zero;
                _cameraTransform.localRotation = Quaternion.identity;
            }
           
        }
    }
}
using UnityEngine;
using DialogueSystem.Data;
using DialogueSystem.Core;
using DialogueSystem.UI;

namespace DialogueSystem.Interactions
{
    [RequireComponent(typeof(Collider))]
    public class InteractionPoint : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private InteractionPointSO data;

        [Header("Hint UI")]
        [SerializeField] private HintUI hintUI;

        [Header("Interaction Key")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("Look Detection")]
        [Tooltip("Centre of the look rectangle in this object's local space.")]
        [SerializeField] private Vector3 lookRectCenter = Vector3.zero;
        [Tooltip("Rotation of the look rectangle in this object's local space (Euler angles).")]
        [SerializeField] private Vector3 lookRectRotation = Vector3.zero;
        [Tooltip("Width (X) and height (Y) of the look rectangle in world units.")]
        [SerializeField] private Vector2 lookRectSize = Vector2.one;
        [Tooltip("Leave null to use Camera.main.")]
        [SerializeField] private Camera playerCamera;

        [Header("Debug")]
        [SerializeField] private bool showTriggerGizmo = true;

        // ── State ──────────────────────────────────────────────────────────────
        private bool _playerInRange;
        private bool _hintShowing;
        private int  _currentEventIndex;

        // ── Rect helpers ───────────────────────────────────────────────────────

        // The rect's orientation baked into a single matrix every frame would
        // be wasteful; instead we compose on demand from the two transforms.
        private Matrix4x4 RectLocalToWorld =>
            Matrix4x4.TRS(
                transform.TransformPoint(lookRectCenter),
                transform.rotation * Quaternion.Euler(lookRectRotation),
                Vector3.one
            );

        // ── Unity ──────────────────────────────────────────────────────────────

        private void Start()
        {
            GetComponent<Collider>().isTrigger = true;

            if (hintUI == null)
                hintUI = GetComponentInChildren<HintUI>(includeInactive: true);

            if (playerCamera == null)
                playerCamera = Camera.main;

            hintUI?.Hide();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;

            if (interactKey == KeyCode.None)
                Interact();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            HideHint();
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (interactKey == KeyCode.None) return;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

            bool looking = IsLookingAtRect();

            if (looking && !_hintShowing)   ShowHint();
            else if (!looking && _hintShowing) HideHint();

            if (looking && Input.GetKeyDown(interactKey))
                Interact();
        }

        // ── Look Check ─────────────────────────────────────────────────────────

        private bool IsLookingAtRect()
        {
            if (playerCamera == null) return true;

            Matrix4x4 rectMtx = RectLocalToWorld;

            // The rect's world-space normal is its local Z axis
            Vector3 rectCenter = rectMtx.GetColumn(3);
            Vector3 rectNormal = rectMtx.GetColumn(2);   // forward / local Z

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // Plane-ray intersection
            float denom = Vector3.Dot(rectNormal, ray.direction);

            // Ray parallel to (or firing away from) the plane → no hit
            if (Mathf.Abs(denom) < 1e-6f) return false;

            float t = Vector3.Dot(rectNormal, rectCenter - ray.origin) / denom;

            // Plane is behind the camera
            if (t < 0f) return false;

            Vector3 hitWorld = ray.GetPoint(t);

            // Transform hit into the rect's local space and test half-extents
            // We only need X and Y axes of the rect's local frame
            Vector3 rectRight = rectMtx.GetColumn(0);
            Vector3 rectUp    = rectMtx.GetColumn(1);
            Vector3 delta     = hitWorld - rectCenter;

            float localX = Vector3.Dot(delta, rectRight);
            float localY = Vector3.Dot(delta, rectUp);

            return Mathf.Abs(localX) <= lookRectSize.x * 0.5f &&
                   Mathf.Abs(localY) <= lookRectSize.y * 0.5f;
        }

        // ── Hint Helpers ───────────────────────────────────────────────────────

        private void ShowHint()
{
    _hintShowing = true;
    if (hintUI == null) return;

    hintUI.Show(data != null ? data.hintText : "Press E");
}

private void HideHint()
{
    _hintShowing = false;
    if (hintUI == null) return;

    hintUI.Hide();
}

        // ── Interaction Logic ──────────────────────────────────────────────────

        private void Interact()
        {
            if (data == null || data.eventSequence == null || data.eventSequence.Count == 0) return;

            data.onActivated?.ExecuteAll();

            int index;
            if (data.randomizeEntry)
            {
                index = Random.Range(0, data.eventSequence.Count);
            }
            else
            {
                index = _currentEventIndex;
                if (index >= data.eventSequence.Count)
                {
                    if (data.loopLastEntry)
                        index = data.eventSequence.Count - 1;
                    else
                        return;
                }
            }

            var entry = data.eventSequence[index];
            HideHint();

            DialogueManager.Instance.OnAnyDialogueEnd += OnDialogueEnded;
            DialogueManager.Instance.ExecuteInteractionEntry(entry);

            if (!data.randomizeEntry && _currentEventIndex < data.eventSequence.Count)
                _currentEventIndex++;
        }

        private void OnDialogueEnded()
        {
            DialogueManager.Instance.OnAnyDialogueEnd -= OnDialogueEnded;

            data.onCompleted?.ExecuteAll();

            bool hasMore = data.randomizeEntry || data.loopLastEntry ||
                           _currentEventIndex < data.eventSequence.Count;
            if (_playerInRange && hasMore && IsLookingAtRect())
                ShowHint();
        }

        // ── Public Utilities ───────────────────────────────────────────────────

        public void ResetSequence() => _currentEventIndex = 0;

        public void SetEventIndex(int index) =>
            _currentEventIndex = Mathf.Clamp(index, 0, data.eventSequence.Count - 1);

        // ── Gizmos ─────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // Trigger volume
            if (showTriggerGizmo)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.25f);
                var col = GetComponent<Collider>();
                if (col is SphereCollider s)
                    Gizmos.DrawSphere(transform.position + s.center, s.radius);
                else if (col is BoxCollider b)
                    Gizmos.DrawCube(transform.position + b.center, b.size);
            }

            // Build the same matrix the runtime uses
            Matrix4x4 m = Matrix4x4.TRS(
                transform.TransformPoint(lookRectCenter),
                transform.rotation * Quaternion.Euler(lookRectRotation),
                Vector3.one
            );

            Vector3 center = m.GetColumn(3);
            Vector3 right  = m.GetColumn(0);
            Vector3 up     = m.GetColumn(1);
            Vector3 fwd    = m.GetColumn(2);

            float hw = lookRectSize.x * 0.5f;
            float hh = lookRectSize.y * 0.5f;

            Vector3 TL = center + (-right * hw) + ( up * hh);
            Vector3 TR = center + ( right * hw) + ( up * hh);
            Vector3 BR = center + ( right * hw) + (-up * hh);
            Vector3 BL = center + (-right * hw) + (-up * hh);

            // Border
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
            Gizmos.DrawLine(TL, TR);
            Gizmos.DrawLine(TR, BR);
            Gizmos.DrawLine(BR, BL);
            Gizmos.DrawLine(BL, TL);

            // Diagonals
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawLine(TL, BR);
            Gizmos.DrawLine(TR, BL);

            // Normal arrow (shows which side the player must face)
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawRay(center, fwd * (lookRectSize.magnitude * 0.25f));

            // Line from component pivot to rect centre
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.2f);
            Gizmos.DrawLine(transform.position, center);
        }
    }
}
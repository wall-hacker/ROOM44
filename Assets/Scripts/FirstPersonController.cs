using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Camera")]
    public Transform cameraTransform;
    public float mouseSensitivity = 100f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Animation")]
    public Animator animator;
    public string moveParam = "IsMoving";   // Bool parameter in Animator

    private CharacterController _controller;
    private Vector3 _velocity;
    private float _cameraPitch = 0f;

    void Start()
    {
        _controller = GetComponent<CharacterController>();

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Auto-find camera if not assigned
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        // Auto-find animator if not assigned
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleAnimation();
    }

    // ─── Mouse Look ───────────────────────────────────────────────────────────

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotate the character body left/right
        transform.Rotate(Vector3.up * mouseX);

        // Tilt the camera up/down (clamped)
        _cameraPitch -= mouseY;
        _cameraPitch = Mathf.Clamp(_cameraPitch, minPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Move relative to the character's facing direction (yaw only)
        Vector3 move = transform.right * h + transform.forward * v;

        // Clamp diagonal speed
        if (move.magnitude > 1f)
            move.Normalize();

        _controller.Move(move * moveSpeed * Time.deltaTime);

        // Simple gravity
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    // ─── Animation ────────────────────────────────────────────────────────────

    void HandleAnimation()
    {
        if (animator == null) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;

        animator.SetBool(moveParam, isMoving);
    }

    public void SyncCameraState()
    {
        // Sync body yaw
        Vector3 bodyEuler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, bodyEuler.y, 0f);

        // Sync camera pitch
        float pitch = cameraTransform.localEulerAngles.x;

        // Convert Unity 0-360 into -180 to 180
        if (pitch > 180f)
            pitch -= 360f;

        _cameraPitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Reset vertical velocity too
        _velocity.y = 0f;
    }

}
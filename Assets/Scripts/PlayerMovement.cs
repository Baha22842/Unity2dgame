using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float runMultiplier = 1.5f;
    public float jumpForce = 7f;

    [Header("Детекция земли (Ground Check)")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Jump Buffer & Coyote Time")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.1f;

    private Rigidbody2D rb;
    private bool isGrounded = false;
    public bool IsGrounded => isGrounded;

    private float jumpBufferTimer;
    private float coyoteTimer;
    private bool wasGroundedLastFrame;

    private PlayerGrab playerGrab;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerGrab = GetComponent<PlayerGrab>();
    }

    void Update()
    {
        CheckGrounded();

        float moveX = Input.GetAxisRaw("Horizontal");
        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isRunning ? speed * runMultiplier : speed;
        rb.linearVelocity = new Vector2(moveX * currentSpeed, rb.linearVelocity.y);

        // Jump buffer: remember jump input for a short time
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer -= Time.deltaTime;

        // Coyote time: grace period after leaving ground
        if (isGrounded)
            coyoteTimer = coyoteTime;
        else if (wasGroundedLastFrame)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;

        // Execute jump if buffered and within coyote time
        bool canJump = true;
        if (playerGrab != null && playerGrab.IsHoldingBox)
        {
            canJump = false; // Блокируем прыжок, если держим тяжелую коробку
        }

        if (jumpBufferTimer > 0f && coyoteTimer > 0f && canJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        wasGroundedLastFrame = isGrounded;
    }

    private void CheckGrounded()
    {
        if (groundCheckPoint != null)
        {
            // Проверяем, есть ли под ногами объекты со слоем groundLayer
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        else
        {
            Debug.LogWarning("У Игрока не назначен Ground Check Point!");
            isGrounded = false;
        }
    }

    // Рисуем кружок детекции в редакторе для удобства
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}

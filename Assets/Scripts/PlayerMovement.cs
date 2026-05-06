using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 7f;

    [Header("Рывок (Dash)")]
    public float dashForce = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    [Header("Лестницы (Climbing)")]
    public float climbSpeed = 5f;
    private bool isNearLadder;
    private bool isClimbing;
    public bool IsClimbing => isClimbing;
    private float defaultGravity;
    private float ladderCooldownTimer;

    [Header("Приседание (Crouching)")]
    public float crouchSpeedMultiplier = 0.5f;
    private bool isCrouching;
    public bool IsCrouching => isCrouching;
    private CapsuleCollider2D playerCollider;
    private Vector2 standingColliderSize;
    private Vector2 standingColliderOffset;

    [Header("Толкание ящика (Pushing)")]
    private bool isPushing;
    public bool IsPushing => isPushing;
    private bool isRollFalling;
    public bool IsRollFalling => isRollFalling;
    private float pushTimeBuffer;

    private float freezeTimer;

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

    // Метроидвания: Навыки
    private int remainingJumps;
    private bool isDashing;
    public bool IsDashing => isDashing;
    private float dashTimeLeft;
    private float dashCooldownTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
        
        playerCollider = GetComponent<CapsuleCollider2D>();
        if (playerCollider != null)
        {
            standingColliderSize = playerCollider.size;
            standingColliderOffset = playerCollider.offset;
        }
    }

    public void FreezeMovement(float duration)
    {
        freezeTimer = duration;
    }

    void Update()
    {
        CheckGrounded();

        if (freezeTimer > 0f)
        {
            freezeTimer -= Time.deltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return; // Игрок застыл (пьет зелье или получает навык)
        }

        // --- ПИТЬЕ ЗЕЛЬЯ (Q) ---
        if (Input.GetKeyDown(KeyCode.Q))
        {
            PlayerAnimator pa = GetComponent<PlayerAnimator>();
            if (pa != null) pa.TriggerDrink();
            FreezeMovement(1.5f);
            return;
        }

        if (pushTimeBuffer > 0f)
        {
            pushTimeBuffer -= Time.deltaTime;
            // Если мы толкали ящик, он пропал, и мы начали падать
            if (!isGrounded && rb.linearVelocity.y < -0.1f)
            {
                isRollFalling = true;
            }
        }

        // --- МЕХАНИКА РЫВКА (DASH) ---
        dashCooldownTimer -= Time.deltaTime;

        if (GameManager.Instance != null && GameManager.Instance.hasDash && Input.GetKeyDown(KeyCode.LeftShift) && dashCooldownTimer <= 0f)
        {
            isDashing = true;
            dashTimeLeft = dashDuration;
            dashCooldownTimer = dashCooldown;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Сброс вертикального падения для ровного рывка
        }

        if (isDashing)
        {
            dashTimeLeft -= Time.deltaTime;
            float dir = GetComponent<SpriteRenderer>().flipX ? -1f : 1f;
            rb.linearVelocity = new Vector2(dir * dashForce, 0f);

            if (dashTimeLeft <= 0f)
            {
                isDashing = false;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            return; // Во время рывка игнорируем обычное перемещение
        }

        // --- ПРИСЕДАНИЕ (CROUCH) ---
        if (isGrounded && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            isCrouching = true;
            if (playerCollider != null)
            {
                playerCollider.size = new Vector2(standingColliderSize.x, standingColliderSize.y / 2f);
                playerCollider.offset = new Vector2(standingColliderOffset.x, standingColliderOffset.y - (standingColliderSize.y / 4f));
            }
        }
        else
        {
            isCrouching = false;
            if (playerCollider != null)
            {
                playerCollider.size = standingColliderSize;
                playerCollider.offset = standingColliderOffset;
            }
        }

        // --- ОБЫЧНОЕ ПЕРЕМЕЩЕНИЕ И ЛЕСТНИЦЫ ---
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        float currentSpeed = isCrouching ? speed * crouchSpeedMultiplier : speed;

        ladderCooldownTimer -= Time.deltaTime;

        // Если мы у лестницы, и либо нажали вверх/вниз, ЛИБО просто падаем сквозь неё
        if (isNearLadder && ladderCooldownTimer <= 0f)
        {
            if (Mathf.Abs(moveY) > 0.1f || rb.linearVelocity.y < -0.1f)
            {
                if (!isClimbing)
                {
                    isClimbing = true;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Мгновенно гасим инерцию падения, чтобы повиснуть
                }
            }
        }

        if (isClimbing)
        {
            rb.gravityScale = 0f; // Отключаем падение
            rb.linearVelocity = new Vector2(moveX * currentSpeed, moveY * climbSpeed);

            // Если нажали прыжок на лестнице - спрыгиваем
            if (Input.GetButtonDown("Jump"))
            {
                isClimbing = false;
                ladderCooldownTimer = 0.2f; // Даем игроку 0.2 секунды, чтобы он успел выпрыгнуть из лестницы
            }
        }
        else
        {
            rb.gravityScale = defaultGravity; // Включаем гравитацию обратно
            rb.linearVelocity = new Vector2(moveX * currentSpeed, rb.linearVelocity.y);
        }

        // --- МЕХАНИКА ПРЫЖКА (DOUBLE JUMP) ---
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer -= Time.deltaTime;

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            // Восстанавливаем прыжки. Если есть навык - даем 2 прыжка (1 обычный + 1 в воздухе)
            remainingJumps = (GameManager.Instance != null && GameManager.Instance.hasDoubleJump) ? 1 : 0;
            
            // Если на земле - сбрасываем состояние падения кувырком
            isRollFalling = false;
        }
        else if (wasGroundedLastFrame)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        // Выполняем прыжок, если нажали кнопку
        if (jumpBufferTimer > 0f)
        {
            // Прыгаем, если мы на земле (coyoteTimer) ИЛИ если мы в воздухе, но есть запасные прыжки
            if (coyoteTimer > 0f || (!isGrounded && remainingJumps > 0))
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;

                if (!isGrounded) 
                {
                    remainingJumps--; // Тратим двойной прыжок
                }
            }
        }

        wasGroundedLastFrame = isGrounded;
    }

    private void CheckGrounded()
    {
        if (groundCheckPoint != null)
        {
            // Проверяем, есть ли под ногами объекты со слоем groundLayer
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

            // ХАК ДЛЯ ЛЕСТНИЦ: Если мы лезем по лестнице, игра должна думать, что мы "на земле".
            // Это починит анимацию (будет проигрываться Idle вместо Fall) и позволит нормально прыгать с лестницы!
            if (isClimbing)
            {
                isGrounded = true;
            }
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

    private void OnTriggerEnter2D(Collider2D collider)
    {
        // Проверяем, коснулись ли мы тайлмапа с лестницами
        if (collider.CompareTag("Ladder"))
        {
            isNearLadder = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        // Если вышли за пределы лестницы
        if (collider.CompareTag("Ladder"))
        {
            isNearLadder = false;
            isClimbing = false;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Нельзя толкать ящик в воздухе ИЛИ сидя (чтобы анимации не конфликтовали)
        if (isGrounded && !isCrouching && collision.gameObject.CompareTag("Box") && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f)
        {
            isPushing = true;
            pushTimeBuffer = 0.2f;
        }
        else if (collision.gameObject.CompareTag("Box"))
        {
            isPushing = false;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Box"))
        {
            isPushing = false;
            // Проверим в Update, упали ли мы сразу после отпускания ящика
        }
    }
}

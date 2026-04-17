using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Idle Animation")]
    public Sprite[] idleSprites;

    [Header("Run Animation")]
    public Sprite[] runSprites;

    [Header("Jump Animation")]
    public Sprite[] jumpUpSprites;

    [Header("Fall Animation")]
    public Sprite[] fallSprites;

    [Header("Attack Animation")]
    public Sprite[] attackSprites;

    [Header("Settings")]
    public float frameRate = 10f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;

    private int currentFrame;
    private float frameTimer;
    private string currentState = "";

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
    }

    private void Update()
    {
        float velocityX = rb.linearVelocity.x;
        float velocityY = rb.linearVelocity.y;
        bool grounded = playerMovement.IsGrounded;

        // Flip sprite based on horizontal direction
        if (velocityX > 0.1f)
            spriteRenderer.flipX = false;
        else if (velocityX < -0.1f)
            spriteRenderer.flipX = true;

        // Determine animation state
        string newState;
        Sprite[] activeSprites = null;

        if (playerCombat != null && playerCombat.IsAttacking)
        {
            newState = "Attack";
            activeSprites = attackSprites;
        }
        else
        {

            if (!grounded)
            {
                // Airborne: jump up or fall
                if (velocityY > 0.1f)
                {
                    newState = "JumpUp";
                    activeSprites = jumpUpSprites;
                }
                else
                {
                    newState = "Fall";
                    activeSprites = fallSprites;
                }
            }
            else if (Mathf.Abs(velocityX) > 0.1f)
            {
                newState = "Run";
                activeSprites = runSprites;
            }
            else
            {
                newState = "Idle";
                activeSprites = idleSprites;
            }
        }

        // Reset frame on state change
        if (newState != currentState)
        {
            currentState = newState;
            currentFrame = 0;
            frameTimer = 0f;
        }

        // Animate multi-frame states (idle/run/jump/fall)
        if (activeSprites == null || activeSprites.Length == 0)
            return;

        // Single-frame shortcut
        if (activeSprites.Length == 1)
        {
            spriteRenderer.sprite = activeSprites[0];
            return;
        }

        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / frameRate)
        {
            frameTimer -= 1f / frameRate;
            currentFrame = (currentFrame + 1) % activeSprites.Length;
        }

        spriteRenderer.sprite = activeSprites[currentFrame];
    }
}

using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Idle Animation")]
    public Sprite[] idleSprites;

    [Header("Run Animation")]
    public Sprite[] runSprites;

    [Header("Jump Sprites")]
    public Sprite jumpUpSprite;
    public Sprite fallSprite;

    [Header("Settings")]
    public float frameRate = 10f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;

    private int currentFrame;
    private float frameTimer;
    private string currentState = "";

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
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

        if (!grounded)
        {
            // Airborne: jump up or fall
            newState = velocityY > 0.1f ? "JumpUp" : "Fall";
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

        // Reset frame on state change
        if (newState != currentState)
        {
            currentState = newState;
            currentFrame = 0;
            frameTimer = 0f;
        }

        // Handle single-frame states (jump/fall)
        if (currentState == "JumpUp")
        {
            if (jumpUpSprite != null)
                spriteRenderer.sprite = jumpUpSprite;
            return;
        }

        if (currentState == "Fall")
        {
            if (fallSprite != null)
                spriteRenderer.sprite = fallSprite;
            return;
        }

        // Animate multi-frame states (idle/run)
        if (activeSprites == null || activeSprites.Length == 0)
            return;

        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / frameRate)
        {
            frameTimer -= 1f / frameRate;
            currentFrame = (currentFrame + 1) % activeSprites.Length;
        }

        spriteRenderer.sprite = activeSprites[currentFrame];
    }
}

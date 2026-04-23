using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    public float patrolDistance = 3f;

    private Vector3 startPos;
    private int direction = 1;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private void Start()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
        }
        else
        {
            transform.Translate(Vector2.right * direction * speed * Time.deltaTime);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = direction < 0;
        }

        float dist = transform.position.x - startPos.x;
        if (dist >= patrolDistance)
            direction = -1;
        else if (dist <= -patrolDistance)
            direction = 1;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        // Если игрок упал сверху — враг умирает
        if (collision.contacts[0].normal.y < -0.5f)
        {
            // Подбросить игрока немного вверх
            Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (playerRb != null)
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, 5f);

            Destroy(gameObject);
        }
        else
        {
            // Игрок задел врага сбоку/снизу — умирает
            if (GameManager.Instance != null)
                GameManager.Instance.PlayerDied();
        }
    }
}

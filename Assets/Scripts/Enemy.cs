using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    public float patrolDistance = 3f;
    
    [Header("Здоровье")]
    public int maxHealth = 3;
    private int currentHealth;

    private Vector3 startPos;
    private int direction = 1;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private void Start()
    {
        currentHealth = maxHealth;
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

        // Любое касание врага наносит урон игроку (убрали прыжок в стиле Марио)
        PlayerMovement pm = collision.gameObject.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.TakeDamage(transform.position);
        }
    }

    /// <summary>
    /// Получение урона врагом от атак игрока.
    /// </summary>
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        
        // Визуальная вспышка при получении урона (эффект мигания красным)
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            Invoke(nameof(ResetColor), 0.15f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void ResetColor()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }

    private void Die()
    {
        // В будущем здесь можно добавить анимацию смерти или спавн партиклов
        Destroy(gameObject);
    }
}

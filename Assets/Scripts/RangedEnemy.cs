using UnityEngine;

public class RangedEnemy : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;
    public float patrolDistance = 3f;

    [Header("Combat")]
    public int maxHealth = 3;
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 2f;
    public float detectionRange = 8f;

    private int currentHealth;
    private Vector3 startPos;
    private int direction = 1;
    private float fireTimer;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Transform player;

    private void Start()
    {
        currentHealth = maxHealth;
        startPos = transform.position;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        float distToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;

        if (player != null && distToPlayer <= detectionRange)
        {
            // Stop and shoot
            if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            // Face player
            direction = player.position.x < transform.position.x ? -1 : 1;
            if (spriteRenderer != null) spriteRenderer.flipX = direction < 0;

            fireTimer += Time.deltaTime;
            if (fireTimer >= 1f / fireRate)
            {
                Shoot();
                fireTimer = 0f;
            }
        }
        else
        {
            // Patrol
            fireTimer = 0f;
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
            }
            else
            {
                transform.Translate(Vector2.right * direction * speed * Time.deltaTime);
            }

            if (spriteRenderer != null) spriteRenderer.flipX = direction < 0;

            float dist = transform.position.x - startPos.x;
            if (dist >= patrolDistance) direction = -1;
            else if (dist <= -patrolDistance) direction = 1;
        }
    }

    private void Shoot()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            Projectile pScript = proj.GetComponent<Projectile>();
            if (pScript != null)
            {
                pScript.SetDirection(Vector2.right * direction);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            Invoke(nameof(ResetColor), 0.15f);
        }

        if (currentHealth <= 0) Die();
    }

    private void ResetColor()
    {
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    public float speed = 5f;
    public float lifetime = 3f;
    public int damage = 1;

    [Tooltip("По умолчанию спрайт направлен влево? (Например, для стрел)")]
    [SerializeField] private bool spriteFacesLeftByDefault = false;

    private Rigidbody2D rb;
    private Vector2 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        Destroy(gameObject, lifetime);
    }

    public void SetDirection(Vector2 dir)
    {
        moveDirection = dir.normalized;

        Vector3 scale = transform.localScale;
        float baseScaleX = Mathf.Abs(scale.x);

        if (moveDirection.x < 0)
        {
            // Летим влево:
            scale.x = spriteFacesLeftByDefault ? baseScaleX : -baseScaleX;
        }
        else if (moveDirection.x > 0)
        {
            // Летим вправо:
            scale.x = spriteFacesLeftByDefault ? -baseScaleX : baseScaleX;
        }

        transform.localScale = scale;
    }

    private void Update()
    {
        // Используем Space.World, чтобы изменение масштаба (scale.x) влияло только на визуал,
        // но не меняло направление движения стрелы в мировом пространстве!
        transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerMovement pm = collision.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.TakeDamage(transform.position);
            }
            Destroy(gameObject);
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Destroy(gameObject);
        }
    }
}

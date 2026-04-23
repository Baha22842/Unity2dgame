using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BoxPhysics : MonoBehaviour
{
    [Tooltip("Насколько сильно коробка тормозит по полу (чем больше, тем быстрее остановится)")]
    public float groundFriction = 15f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        // Берем текущую скорость (она включает и падение по Y, и скольжение по X)
        Vector2 vel = rb.linearVelocity;

        // Если коробка не летит в пропасть (почти не падает)
        if (Mathf.Abs(vel.y) < 0.1f)
        {
            // Плавно, но ОЧЕНЬ быстро гасим скорость только по горизонтали (X)
            vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * groundFriction);

            // Если она еле ползет - просто останавливаем намертво, чтобы не было микро-скольжений
            if (Mathf.Abs(vel.x) < 0.2f)
            {
                vel.x = 0;
            }
        }

        // Применяем скорость обратно
        rb.linearVelocity = vel;
    }
}

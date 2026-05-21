using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BoxPhysics : MonoBehaviour
{
    [Tooltip("Насколько сильно коробка тормозит по полу (чем больше, тем быстрее остановится)")]
    public float groundFriction = 15f;

    private Rigidbody2D rb;
    private bool _isBeingPushed;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (collision.gameObject.TryGetComponent<PlayerMovement>(out var player))
            {
                if (player.IsPushing)
                {
                    _isBeingPushed = true;
                    return;
                }
            }
        }
        _isBeingPushed = false;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            _isBeingPushed = false;
        }
    }

    void FixedUpdate()
    {
        // Берем текущую скорость (она включает и падение по Y, и скольжение по X)
        Vector2 vel = rb.linearVelocity;

        // Если коробка не летит в пропасть и игрок ее активно не толкает
        if (Mathf.Abs(vel.y) < 0.1f && !_isBeingPushed)
        {
            // Плавно, но ОЧЕНЬ быстро гасим скорость только по горизонтали (X)
            vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * groundFriction);

            // Если она еле ползет - просто останавливаем намертво, чтобы не было микро-скольжений
            if (Mathf.Abs(vel.x) < 0.2f)
            {
                vel.x = 0;
            }
        }
        else if (_isBeingPushed)
        {
            // Сбрасываем флаг для следующего кадра физики (OnCollisionStay2D обновит его, если контакт продолжается)
            _isBeingPushed = false;
        }

        // Применяем скорость обратно
        rb.linearVelocity = vel;
    }
}

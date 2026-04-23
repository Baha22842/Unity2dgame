using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerGrab : MonoBehaviour
{
    [Header("Настройки")]
    public KeyCode grabKey = KeyCode.E;
    public Transform holdPoint; // Точка над головой игрока, куда будем поднимать ящик
    public float grabDistance = 1f;
    public LayerMask boxLayer;

    public bool IsHoldingBox => isHoldingBox;

    private GameObject grabbedBox;
    private bool isHoldingBox = false;

    private PlayerMovement playerMovement;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (Input.GetKeyDown(grabKey))
        {
            if (!isHoldingBox)
            {
                TryGrabBox();
            }
            else
            {
                DropBox();
            }
        }

        // Если держим ящик над головой - жестко фиксируем его позицию
        if (isHoldingBox && grabbedBox != null)
        {
            grabbedBox.transform.position = holdPoint.position;
        }
    }

    private void TryGrabBox()
    {
        // Используем радар (круг) вместо луча
        // Радиус увеличен до 1.5, чтобы точно достать ящик
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.5f, boxLayer);
        
        foreach (Collider2D hit in hits)
        {
            // Мы больше не проверяем Теги, но мы проверяем ФИЗИКУ!
            // Рычаг не имеет "падающего" веса, поэтому у него нет Dynamic Rigidbody.
            // Берем только те объекты, у которых есть гравитация и вес (Rigidbody2D).
            Rigidbody2D boxRb = hit.GetComponent<Rigidbody2D>();
            
            // Если Rigidbody есть, и он динамический (т.е. это настоящая тяжелая коробка)
            if (boxRb != null && boxRb.bodyType == RigidbodyType2D.Dynamic)
            {
                grabbedBox = hit.gameObject;
                isHoldingBox = true;

                boxRb.isKinematic = true; // Отключаем гравитацию
                boxRb.linearVelocity = Vector2.zero; // Сбрасываем скорость
                
                Collider2D boxCol = grabbedBox.GetComponent<Collider2D>();
                if (boxCol != null) boxCol.enabled = false;
                
                break; // Берем только первую попавшуюся коробку
            }
        }
    }

    private void DropBox()
    {
        if (grabbedBox != null)
        {
            Rigidbody2D boxRb = grabbedBox.GetComponent<Rigidbody2D>();
            if (boxRb != null)
            {
                boxRb.isKinematic = false; // Возвращаем гравитацию

                // Включаем коллайдер обратно, чтобы коробка снова стала физической
                Collider2D boxCol = grabbedBox.GetComponent<Collider2D>();
                if (boxCol != null) boxCol.enabled = true;
            }

            isHoldingBox = false;
            grabbedBox = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabDistance);
    }
}

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CelesteRoomTrigger : MonoBehaviour
{
    [Header("Точка Перемещения")]
    [Tooltip("Создайте пустой объект (GameObject) в следующей комнате и перетащите его сюда. Туда телепортируется игрок.")]
    public Transform targetSpawnPoint;

    [Header("Настройки Перехода")]
    [Tooltip("Длительность затемнения экрана (в секундах).")]
    [Range(0.1f, 2f)]
    public float fadeDuration = 0.35f;

    [Tooltip("Время удержания полностью черного экрана (в секундах).")]
    [Range(0.0f, 1f)]
    public float holdDuration = 0.15f;

    private BoxCollider2D _collider;
    private bool _isTransitioning = false;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
        _collider.isTrigger = true; // Гарантируем, что коллайдер является триггером
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isTransitioning) return;

        // Переходим только если с триггером столкнулся игрок
        if (other.CompareTag("Player"))
        {
            if (targetSpawnPoint == null)
            {
                Debug.LogWarning($"[CelesteRoomTrigger] В триггере {gameObject.name} не назначена точка 'Target Spawn Point'!", this);
                return;
            }

            // Инициализируем синглтон переходов, если его нет на сцене
            if (CelesteTransition.Instance == null)
            {
                GameObject manager = new GameObject("CelesteTransitionManager");
                manager.AddComponent<CelesteTransition>();
            }

            if (CelesteTransition.Instance != null)
            {
                _isTransitioning = true;
                CelesteTransition.Instance.Transition(
                    targetSpawnPoint.position,
                    fadeDuration,
                    holdDuration,
                    () => { _isTransitioning = false; }
                );
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Красивое визуальное отображение триггера перехода в окне редактора Unity
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.color = new Color(0.9f, 0.4f, 0f, 0.3f);
            Gizmos.DrawCube(transform.position + (Vector3)box.offset, new Vector3(box.size.x, box.size.y, 0.1f));
            
            Gizmos.color = new Color(0.9f, 0.4f, 0f, 0.8f);
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, new Vector3(box.size.x, box.size.y, 0.1f));
        }

        if (targetSpawnPoint != null)
        {
            // Рисуем стрелку направления телепортации от триггера к целевой точке
            Gizmos.color = Color.orange;
            Gizmos.DrawLine(transform.position, targetSpawnPoint.position);
            Gizmos.DrawSphere(targetSpawnPoint.position, 0.25f);
        }
    }
}

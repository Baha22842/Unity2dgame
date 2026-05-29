using UnityEngine;
using System.Collections;

public class SpellCloud : MonoBehaviour
{
    [Header("Настройки Предупреждения (Telegraph)")]
    [Tooltip("Время в секундах, пока тучка/портал заряжается перед ударом (время, чтобы игрок успел среагировать и отбежать)")]
    [SerializeField] private float warningDuration = 0.6f;

    [Header("Настройки Удара")]
    [Tooltip("Префаб, который полетит вниз (например, молния, капля кислоты или снаряд). Если не назначен, тучка просто активирует свой собственный триггер-коллайдер.")]
    [SerializeField] private GameObject strikePrefab;
    [Tooltip("Скорость падения снаряда вниз (применяется только если назначен Strike Prefab)")]
    [SerializeField] private float strikeSpeed = 8f;

    [Header("Собственный Удар (без дополнительного префаба)")]
    [Tooltip("Включить этот коллайдер при ударе (если у тучки встроенная анимация молнии)")]
    [SerializeField] private Collider2D strikeCollider;
    [Tooltip("Время жизни коллайдера удара перед уничтожением тучки")]
    [SerializeField] private float strikeActiveDuration = 0.3f;

    private Animator animator;
    private bool hasStruck = false;

    private void Start()
    {
        animator = GetComponent<Animator>();

        Debug.Log($"[SpellCloud] Облако заклинания создано в {transform.position}! Запуск предупреждения на {warningDuration} сек.", this);

        // При старте выключаем коллайдер удара (чтобы игрок не получал урон во время зарядки/предупреждения)
        if (strikeCollider != null)
        {
            strikeCollider.enabled = false;
        }
        else
        {
            Debug.LogWarning("[SpellCloud] 'Strike Collider' не назначен! Если вы используете встроенный удар, назначьте коллайдер.", this);
        }

        // Запуск таймера предупреждения
        StartCoroutine(TelegraphRoutine());
    }

    private IEnumerator TelegraphRoutine()
    {
        // 1. Фаза предупреждения
        // В этот момент тучка просто висит в воздухе над игроком, искрится или увеличивается (проигрывается анимация заряда)
        if (animator != null)
        {
            Debug.Log("[SpellCloud] Активация триггера 'Charge' в Animator (зарядка/предупреждение).", this);
            animator.SetTrigger("Charge");
        }

        yield return new WaitForSeconds(warningDuration);

        // 2. Фаза удара
        hasStruck = true;
        Debug.Log("[SpellCloud] Таймер предупреждения истек! Запуск фазы УДАРА.", this);

        if (animator != null)
        {
            Debug.Log("[SpellCloud] Активация триггера 'Strike' в Animator (удар молнии).", this);
            animator.SetTrigger("Strike");
        }

        // Если назначен префаб молнии/снаряда, спавним его и пускаем вниз
        if (strikePrefab != null)
        {
            Debug.Log($"[SpellCloud] Спавн дополнительного префаба удара: {strikePrefab.name}", this);
            GameObject strikeObj = Instantiate(strikePrefab, transform.position, Quaternion.identity);
            Rigidbody2D rb = strikeObj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.down * strikeSpeed;
            }
            
            // Навешиваем на снаряд SpikeTrap для нанесения урона при соприкосновении
            if (strikeObj.GetComponent<SpikeTrap>() == null && strikeObj.GetComponent<Trap>() == null)
            {
                strikeObj.AddComponent<SpikeTrap>();
            }
        }
        else if (strikeCollider != null)
        {
            // Если префаба нет, активируем встроенный триггер-коллайдер (например, под размер молнии)
            Debug.Log("[SpellCloud] Активация встроенного Strike Collider.", this);
            strikeCollider.enabled = true;
            
            // Навешиваем SpikeTrap для нанесения урона, если его нет
            if (GetComponent<SpikeTrap>() == null && GetComponent<Trap>() == null)
            {
                Debug.Log("[SpellCloud] Добавление компонента SpikeTrap для нанесения урона игроку.", this);
                gameObject.AddComponent<SpikeTrap>();
            }

            yield return new WaitForSeconds(strikeActiveDuration);
        }

        Debug.Log("[SpellCloud] Уничтожение объекта облака.", this);
        // Удаляем тучку из памяти после завершения удара
        Destroy(gameObject);
    }
}

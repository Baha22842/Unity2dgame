using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    public KeyCode attackKey = KeyCode.J;
    public KeyCode heavyAttackKey = KeyCode.K; // Новая кнопка для тяжелой атаки
    public float attackDuration = 0.25f;
    public Transform attackPoint;
    public float attackRadius = 0.5f;
    [Tooltip("Не забудь добавить слой, на котором находятся Враги и Рычаги!")]
    public LayerMask attackLayers; // Раньше называлось enemyLayers

    public bool IsAttacking => isAttacking;

    private bool isAttacking;
    private float attackTimer;

    private void Update()
    {
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
                isAttacking = false;
            return;
        }

        if (Input.GetKeyDown(attackKey))
        {
            StartAttack(false); // Легкая атака
        }
        else if (Input.GetKeyDown(heavyAttackKey) && GameManager.Instance != null && GameManager.Instance.hasHeavyAttack)
        {
            StartAttack(true); // Тяжелая атака
        }
    }

    private void StartAttack(bool isHeavy)
    {
        isAttacking = true;
        attackTimer = attackDuration;

        if (attackPoint == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, attackLayers);

        bool hasHitSomething = false;

        foreach (Collider2D hit in hits)
        {
            // 1. Проверяем, враг ли это
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
                hasHitSomething = true;
            }

            // 2. Проверяем, можно ли по этому ударить (например, Рычаг или Стена)
            IHittable hittableObj = hit.GetComponent<IHittable>();
            if (hittableObj != null)
            {
                hittableObj.OnHit(isHeavy);
                hasHitSomething = true;
            }
        }

        // Если мы по чему-то попали, вызываем остановку времени (Hit Stop)
        if (hasHitSomething && GameManager.Instance != null)
        {
            GameManager.Instance.HitStop(0.04f); // Останавливаем время на 0.04 сек для крутого эффекта
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}

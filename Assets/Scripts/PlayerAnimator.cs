using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;

    private bool wasGroundedLastFrame;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
    }

    private void Update()
    {
        if (rb == null || playerMovement == null) return;

        if (playerMovement.IsDead)
        {
            // Жестко останавливаем анимацию, если она попытается пойти по кругу
            var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.normalizedTime >= 0.95f && stateInfo.IsTag("Death"))
            {
                anim.speed = 0f;
            }
            return; // Мертвые не двигаются и не обновляют параметры
        }

        // 2. Передача параметров в Unity Animator
        anim.SetFloat("VelocityX", Mathf.Abs(rb.linearVelocity.x));
        anim.SetFloat("VelocityY", rb.linearVelocity.y);
        anim.SetBool("IsGrounded", playerMovement.IsGrounded);
        anim.SetBool("IsClimbing", playerMovement.IsClimbing);
        anim.SetBool("IsDashing", playerMovement.IsDashing);
        anim.SetBool("IsPushing", playerMovement.IsPushing);
        anim.SetBool("IsCrouching", playerMovement.IsCrouching);
        anim.SetBool("IsRollFalling", playerMovement.IsRollFalling);

        // 3. Умная пауза анимации на лестнице
        if (playerMovement.IsClimbing)
        {
            // Если игрок висит на лестнице и не двигается, ставим анимацию на паузу
            if (Mathf.Abs(rb.linearVelocity.y) > 0.1f || Mathf.Abs(rb.linearVelocity.x) > 0.1f)
                anim.speed = 1f;
            else
                anim.speed = 0f;
        }
        else
        {
            anim.speed = 1f; // Для всех остальных анимаций скорость нормальная
        }

        // 4. Триггер приземления: срабатывает в МОМЕНТ касания земли, а не пока стоим
        bool isGroundedNow = playerMovement.IsGrounded;
        if (isGroundedNow && !wasGroundedLastFrame && rb.linearVelocity.y <= 0f)
        {
            anim.SetTrigger("JustLanded");
        }
        wasGroundedLastFrame = isGroundedNow;

        // 5. Передаем статус атаки обратно (нужно для блокировки бега)
        if (playerCombat != null)
        {
            anim.SetBool("IsAttacking", playerCombat.IsAttacking);
        }
    }

    // Триггеры для комбо-атак
    public void TriggerAttack1()
    {
        anim.SetTrigger("Attack1"); // Твоя старая базовая атака
    }

    public void TriggerAttack2()
    {
        anim.SetTrigger("Attack2"); // Анимация HeavyAttack (как часть комбо)
    }

    public void TriggerHeavyAttack()
    {
        anim.SetTrigger("HeavyAttack"); // Анимация SlashWide (мощный удар на К)
    }

    public void TriggerThrustAttack()
    {
        anim.SetTrigger("ThrustAttack"); // Отдельная колющая атака
    }

    public void TriggerDrink()
    {
        anim.SetTrigger("Drink");
    }

    public void TriggerPowerUp()
    {
        anim.SetTrigger("PowerUp");
    }

    public void TriggerHit()
    {
        if (playerMovement != null && playerMovement.IsDead) return;
        anim.SetTrigger("Hit");
    }

    public void TriggerDie()
    {
        anim.SetTrigger("Die");
        anim.SetBool("IsDead", true); // Для страховки
    }
}

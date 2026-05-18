using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    private Animator anim;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;

    private bool wasGroundedLastFrame;

    // Кэшируем хэши параметров для оптимизации (Unity не любит строки в Update)
    private readonly int velXHash = Animator.StringToHash("VelocityX");
    private readonly int velYHash = Animator.StringToHash("VelocityY");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int isClimbingHash = Animator.StringToHash("IsClimbing");
    private readonly int isDashingHash = Animator.StringToHash("IsDashing");
    private readonly int isPushingHash = Animator.StringToHash("IsPushing");
    private readonly int isCrouchingHash = Animator.StringToHash("IsCrouching");
    private readonly int isRollFallingHash = Animator.StringToHash("IsRollFalling");
    private readonly int isAttackingHash = Animator.StringToHash("IsAttacking");
    private readonly int isLedgeGrabbingHash = Animator.StringToHash("IsLedgeGrabbing");
    private readonly int isPowerUpHash = Animator.StringToHash("IsPowerUp");

    private void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(PlayerMovement.PlayerState newState)
    {
        // Здесь можно реагировать на смену стейтов напрямую
        // Например: если newState == PlayerState.Jump, запускаем партиклы
    }

    private void Update()
    {
        if (rb == null || playerMovement == null) return;

        if (playerMovement.IsDead || playerMovement.CurrentState == PlayerMovement.PlayerState.PowerUp)
        {
            // Сбрасываем все параметры движения, чтобы Unity Animator не пытался
            // переключиться на бег, падение или дэш поверх смерти/пауэрапа
            anim.SetFloat(velXHash, 0f);
            anim.SetBool(isDashingHash, false);
            anim.SetBool(isClimbingHash, false);
            anim.SetBool(isGroundedHash, true); // Чтобы не падал в воздухе по анимации
            anim.SetBool(isAttackingHash, false);
            anim.SetBool(isLedgeGrabbingHash, false);
            anim.SetBool(isPowerUpHash, true);

            if (playerMovement.IsDead)
            {
                var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 0.95f && stateInfo.IsTag("Death"))
                {
                    anim.speed = 0f; // Замораживаем на последнем кадре
                }
            }
            return; // Полностью блокируем остальную логику
        }
        
        anim.SetBool(isPowerUpHash, false);

        // Стабилизация анимации бега у стены
        float animVelX = Mathf.Abs(rb.linearVelocity.x);
        if (playerMovement.CurrentState == PlayerMovement.PlayerState.Run) animVelX = 1f;
        else if (playerMovement.CurrentState == PlayerMovement.PlayerState.Idle) animVelX = 0f;

        anim.SetFloat(velXHash, animVelX);
        anim.SetFloat(velYHash, rb.linearVelocity.y);
        anim.SetBool(isGroundedHash, playerMovement.IsGrounded);
        anim.SetBool(isClimbingHash, playerMovement.IsClimbing);
        anim.SetBool(isDashingHash, playerMovement.IsDashing);
        anim.SetBool(isPushingHash, playerMovement.IsPushing);
        anim.SetBool(isCrouchingHash, playerMovement.IsCrouching);
        anim.SetBool(isRollFallingHash, playerMovement.IsRollFalling);
        anim.SetBool(isLedgeGrabbingHash, playerMovement.CurrentState == PlayerMovement.PlayerState.LedgeGrab);

        // Пауза анимации на лестнице
        if (playerMovement.IsClimbing)
        {
            anim.speed = (Mathf.Abs(rb.linearVelocity.y) > 0.1f || Mathf.Abs(rb.linearVelocity.x) > 0.1f) ? 1f : 0f;
        }
        else
        {
            anim.speed = 1f;
        }

        // Триггер приземления
        bool isGroundedNow = playerMovement.IsGrounded;
        if (isGroundedNow && !wasGroundedLastFrame && rb.linearVelocity.y <= 0f)
        {
            anim.SetTrigger("JustLanded");
        }
        wasGroundedLastFrame = isGroundedNow;

        if (playerCombat != null)
        {
            anim.SetBool(isAttackingHash, playerCombat.IsAttacking);
        }
    }

    public void TriggerAttack1() => anim.SetTrigger("Attack1");
    public void TriggerAttack2() => anim.SetTrigger("Attack2");
    public void TriggerHeavyAttack() => anim.SetTrigger("HeavyAttack");
    public void TriggerThrustAttack() => anim.SetTrigger("ThrustAttack");
    public void TriggerDrink() => anim.SetTrigger("Drink");
    public void TriggerPowerUp() 
    {
        anim.Play("PlayerWomanPowerUp"); // Точное название из скриншота!
    }
    public void TriggerHit()
    {
        if (playerMovement != null && playerMovement.IsDead) return;
        anim.SetTrigger("Hit");
    }
    public void TriggerDie()
    {
        anim.Play("Death"); // Принудительно запускаем стейт смерти поверх всего
        anim.SetTrigger("Die");
        anim.SetBool("IsDead", true);
    }
}

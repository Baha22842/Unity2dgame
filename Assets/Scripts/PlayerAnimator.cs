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
    private readonly int isLedgeClimbingHash = Animator.StringToHash("IsLedgeClimbing");
    private readonly int isPowerUpHash = Animator.StringToHash("IsPowerUp");
    private readonly int isShieldingHash = Animator.StringToHash("IsShielding");

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
        if (rb == null || playerMovement == null || anim == null) return;

        bool isDead = playerMovement.IsDead;
        bool isPowerUp = playerMovement.CurrentState == PlayerMovement.PlayerState.PowerUp;

        // Вместо костылей с "return;" мы просто ставим всем параметрам нужные значения
        anim.SetBool("IsDead", isDead);
        anim.SetBool(isPowerUpHash, isPowerUp);

        // Стабилизация анимации бега у стены
        float animVelX = Mathf.Abs(rb.linearVelocity.x);
        if (playerMovement.CurrentState == PlayerMovement.PlayerState.Run) animVelX = 1f;
        else if (playerMovement.CurrentState == PlayerMovement.PlayerState.Idle || isDead || isPowerUp) animVelX = 0f;

        anim.SetFloat(velXHash, animVelX);
        anim.SetFloat(velYHash, rb.linearVelocity.y);
        
        // Блокируем параметры, когда игрок мертв или берет сферу
        anim.SetBool(isGroundedHash, (isDead || isPowerUp) ? true : playerMovement.IsGrounded);
        anim.SetBool(isClimbingHash, !isDead && !isPowerUp && playerMovement.IsClimbing);
        anim.SetBool(isDashingHash, !isDead && !isPowerUp && playerMovement.IsDashing);
        anim.SetBool(isPushingHash, !isDead && !isPowerUp && playerMovement.IsPushing);
        // Фикс зависания/залипания в crouch walk при отпускании Ctrl на бегу/защите:
        // Если игрок отжал приседание, но аниматор остался в состояниях приседа, принудительно переключаем его в Idle/Shield
        if (!isDead && !isPowerUp && !playerMovement.IsCrouching)
        {
            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("PlayerWomanCrouchWalk") || 
                state.IsName("PlayerWomanCrouchWalkShield") || 
                state.IsName("PlayerWomanCrouchShieldIdle"))
            {
                anim.Play(playerMovement.IsShielding ? "PlayerWomanShieldUp" : "PlayerWomanIdle");
            }
        }

        // Управление состояниями щита (ПКМ) в стойке и в приседе (покой/ходьба):
        if (!isDead && !isPowerUp && playerMovement.IsShielding)
        {
            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (playerMovement.IsCrouching)
            {
                if (animVelX > 0.1f)
                {
                    if (!state.IsName("PlayerWomanCrouchWalkShield"))
                    {
                        anim.Play("PlayerWomanCrouchWalkShield");
                    }
                }
                else
                {
                    if (!state.IsName("PlayerWomanCrouchShieldIdle"))
                    {
                        anim.Play("PlayerWomanCrouchShieldIdle");
                    }
                }
            }
            else
            {
                if (!state.IsName("PlayerWomanShieldUp"))
                {
                    anim.Play("PlayerWomanShieldUp");
                }
            }
        }

        bool isCrouchingParam = !isDead && !isPowerUp && playerMovement.IsCrouching;

        // Фикс глитча/петли Any State -> PlayerWomanCrouch:
        // Если мы уже идем в приседе (CrouchWalk),
        // временно сбрасываем IsCrouching в false в аниматоре, чтобы Any State переход не перезапускал анимацию на 1-й кадр.
        var currentState = anim.GetCurrentAnimatorStateInfo(0);
        if (isCrouchingParam && currentState.IsName("PlayerWomanCrouchWalk"))
        {
            isCrouchingParam = false;
        }

        anim.SetBool(isCrouchingHash, isCrouchingParam);
        anim.SetBool(isRollFallingHash, !isDead && !isPowerUp && playerMovement.IsRollFalling);
        anim.SetBool(isLedgeGrabbingHash, !isDead && !isPowerUp && playerMovement.CurrentState == PlayerMovement.PlayerState.LedgeGrab);
        anim.SetBool(isLedgeClimbingHash, !isDead && !isPowerUp && playerMovement.CurrentState == PlayerMovement.PlayerState.LedgeClimb);
        anim.SetBool(isShieldingHash, !isDead && !isPowerUp && playerMovement.IsShielding);

        if (playerMovement.IsClimbing && !isDead && !isPowerUp)
        {
            anim.speed = (Mathf.Abs(rb.linearVelocity.y) > 0.1f || Mathf.Abs(rb.linearVelocity.x) > 0.1f) ? 1f : 0f;
        }
        else
        {
            anim.speed = 1f;
        }

        bool isGroundedNow = playerMovement.IsGrounded;
        if (isGroundedNow && !wasGroundedLastFrame && rb.linearVelocity.y <= 0f && !isDead && !isPowerUp)
        {
            anim.SetTrigger("JustLanded");
        }
        wasGroundedLastFrame = isGroundedNow;

        if (playerCombat != null)
        {
            anim.SetBool(isAttackingHash, !isDead && !isPowerUp && playerCombat.IsAttacking);
        }
    }

    public void TriggerAttack1() => anim.SetTrigger("Attack1");
    public void TriggerAttack2() => anim.SetTrigger("Attack2");
    public void TriggerHeavyAttack() => anim.SetTrigger("HeavyAttack");
    public void TriggerThrustAttack() => anim.SetTrigger("ThrustAttack");
    public void TriggerDrink() => anim.SetTrigger("Drink");
    public void SetTrigger(string triggerName) => anim.SetTrigger(triggerName);
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
        anim.Play("PlayerWomanDying"); // Принудительно запускаем стейт смерти
    }
    public void TriggerJump()
    {
        if (anim != null)
        {
            anim.Play("PlayerWomanJump", 0, 0f); // Принудительно перезапускаем анимацию прыжка с 0 кадра
        }
    }
}

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

        // 1. Поворот спрайта (Flip) в зависимости от направления движения
        if (rb.linearVelocity.x > 0.1f)
            spriteRenderer.flipX = false;
        else if (rb.linearVelocity.x < -0.1f)
            spriteRenderer.flipX = true;

        // 2. Передача параметров в Unity Animator
        anim.SetFloat("VelocityX", Mathf.Abs(rb.linearVelocity.x));
        anim.SetFloat("VelocityY", rb.linearVelocity.y);
        anim.SetBool("IsGrounded", playerMovement.IsGrounded);

        // 3. Передача атаки 
        // В PlayerCombat.cs есть свойство IsAttacking, которое длится attackDuration
        if (playerCombat != null)
        {
            anim.SetBool("IsAttacking", playerCombat.IsAttacking);
        }
    }
}

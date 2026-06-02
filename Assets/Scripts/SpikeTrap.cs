using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied();
            }
            else
            {
                PlayerMovement pm = collision.gameObject.GetComponent<PlayerMovement>();
                if (pm != null) pm.TakeDamage(transform.position);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied();
            }
            else
            {
                PlayerMovement pm = collider.GetComponent<PlayerMovement>();
                if (pm != null) pm.TakeDamage(transform.position);
            }
        }
    }
}

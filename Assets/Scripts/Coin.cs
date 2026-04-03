using UnityEngine;

public class Coin : MonoBehaviour
{
    public int value = 10;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(value);
        }

        Destroy(gameObject);
    }
}


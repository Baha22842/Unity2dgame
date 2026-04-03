using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDied();
        }
        else
        {
            // На случай, если GameManager не настроен
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}


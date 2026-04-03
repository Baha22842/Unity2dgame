using UnityEngine;

public class Finish : MonoBehaviour
{
    public int scoreReward = 100;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreReward);
            GameManager.Instance.WinLevel();
        }
    }
}


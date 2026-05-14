using UnityEngine;

public class Portal : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Если игрок вошел в портал
        if (collision.CompareTag("Player"))
        {
            Debug.Log("Игрок вошел в портал! Конец игры!");

            if (GameManager.Instance != null)
            {
                // Вызываем нашу функцию победы, которая покажет красивое деревянное меню
                GameManager.Instance.WinLevel();
            }
        }
    }
}

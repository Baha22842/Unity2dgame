using UnityEngine;

public class LevelExit : MonoBehaviour
{
    [Tooltip("Если галочка стоит, игра закончится победой. Если нет - загрузится следующий уровень.")]
    public bool isFinalLevel = false;

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                if (isFinalLevel)
                {
                    GameManager.Instance.WinLevel(); // Конечная точка игры
                }
                else
                {
                    GameManager.Instance.LoadNextLevel(); // Переход на следующий этап
                }
            }
        }
    }
}

using UnityEngine;

public class BossDoor : MonoBehaviour
{
    [Header("Настройки двери")]
    public int requiredArtifacts = 3;
    public float openSpeed = 2f;
    public float openDistance = 5f; // На сколько юнитов вниз уедет дверь
    
    private bool isOpening = false;
    private Vector3 targetPosition;

    private void Start()
    {
        // Запоминаем конечную позицию (опускаем дверь вниз под землю)
        targetPosition = transform.position - new Vector3(0, openDistance, 0);
    }

    private void Update()
    {
        // Если дверь открывается, плавно двигаем её к targetPosition
        if (isOpening)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, openSpeed * Time.deltaTime);
            
            // Если дверь доехала, можно выключить скрипт, чтобы не кушал ресурсы
            if (transform.position == targetPosition)
            {
                enabled = false;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isOpening)
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.collectedArtifacts >= requiredArtifacts)
                {
                    Debug.Log("Дверь к боссу открывается!");
                    isOpening = true;
                    
                    // Опционально: звук открывающейся каменной двери
                }
                else
                {
                    int left = requiredArtifacts - GameManager.Instance.collectedArtifacts;
                    Debug.Log("Вам не хватает ключей! Нужно еще: " + left);
                    // Здесь в будущем можно добавить всплывающий текст на экране
                }
            }
        }
    }
}

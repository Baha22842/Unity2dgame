using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Artifact : MonoBehaviour
{
    [Header("Эффекты при подборе (Опционально)")]
    public GameObject collectEffect; // сюда можно кинуть Particle System искр

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Говорим менеджеру, что подобрали ключ
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CollectArtifact();
            }

            // Создаем искры, если они есть
            if (collectEffect != null)
            {
                Instantiate(collectEffect, transform.position, Quaternion.identity);
            }

            // Опционально: звук подбора (можно добавить AudioSource)

            // Уничтожаем объект ключа на сцене
            Destroy(gameObject);
        }
    }
}

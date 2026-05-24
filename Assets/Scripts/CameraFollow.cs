using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    private void LateUpdate()
    {
        // Если цель указывает на префаб в ассетах (а не на сцене), сбрасываем её
        if (target != null && !target.gameObject.scene.IsValid())
        {
            target = null;
        }

        // Если цель не назначена — пытаемся найти заспавненного игрока
        if (target == null)
        {
            // 1. Ищем по компоненту PlayerMovement (самый надежный способ, не зависит от тегов)
            PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                // 2. Резервный поиск для старых версий Unity
                #pragma warning disable CS0618
                player = FindObjectOfType<PlayerMovement>();
                #pragma warning restore CS0618
                if (player != null)
                {
                    target = player.transform;
                }
                else
                {
                    // 3. Если компонент не найден, пробуем найти по тегу "Player"
                    GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                    if (playerObj != null)
                    {
                        target = playerObj.transform;
                    }
                }
            }
        }

        if (target == null) return;

        transform.position = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );
    }
}

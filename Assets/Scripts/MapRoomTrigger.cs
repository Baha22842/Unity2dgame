using UnityEngine;

public class MapRoomTrigger : MonoBehaviour
{
    [Header("Координаты комнаты на карте (от 0 до 19)")]
    public int roomX = 0;
    public int roomY = 0;

    [Header("Границы камеры для этой комнаты")]
    [Tooltip("Создай PolygonCollider2D, очерти им комнату и перетащи сюда")]
    public PolygonCollider2D roomCameraBounds;

    // В метроидваниях обычно Y=0 это самый верхний ряд сетки (как читаем книгу: сверху вниз, слева направо).
    // Поставь этот скрипт на невидимый BoxCollider2D (Is Trigger) размером с целую локацию.

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (MapManager.Instance != null)
            {
                // Говорим менеджеру карты, что игрок сейчас в этой комнате
                MapManager.Instance.SetCurrentRoom(roomX, roomY);
            }

            // AAA-фишка: Передаем границы этой комнаты в менеджер камеры!
            if (CameraManager.Instance != null && roomCameraBounds != null)
            {
                CameraManager.Instance.SetRoomBounds(roomCameraBounds);
            }
        }
    }
}

using UnityEngine;

// Важно: Чтобы скрипт работал, в Unity должен быть установлен пакет Cinemachine!
// Если светится красным — зайди в Window -> Package Manager, найди Cinemachine и нажми Install.
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Настройки Камеры")]
    [Tooltip("Перетащи сюда компонент CinemachineConfiner2D с твоей виртуальной камеры")]
    public CinemachineConfiner2D confiner;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Эту функцию вызывает MapRoomTrigger, когда игрок заходит в новую комнату.
    /// Камера плавно переключится на новые границы и никогда не покажет пустоту за стенами!
    /// </summary>
    public void SetRoomBounds(PolygonCollider2D newBounds)
    {
        if (confiner == null)
        {
            Debug.LogWarning("CameraManager: Не назначен CinemachineConfiner2D!");
            return;
        }

        // Если границы уже эти — ничего не делаем
        if (confiner.BoundingShape2D == newBounds) return;

        // Назначаем новые границы
        confiner.BoundingShape2D = newBounds;

        // Заставляем Cinemachine пересчитать кеш границ (важно для плавности)
        confiner.InvalidateBoundingShapeCache();
        
        Debug.Log($"Камера заблокирована в границах новой комнаты: {newBounds.gameObject.name}");
    }
}

using UnityEngine;
using System;
using System.Reflection;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Настройки Камеры")]
    [Tooltip("Компонент CinemachineConfiner2D (для Cinemachine v3) или CinemachineConfiner (для Cinemachine v2)")]
    public Component confiner; // Используем базовый тип Component для 100% защиты от ошибок компиляции!

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
            // Если конфинер не назначен вручную в инспекторе, попробуем найти его на сцене автоматически
            confiner = FindConfinerOnScene();
        }

        if (confiner == null)
        {
            Debug.LogWarning("CameraManager: Не найден компонент Cinemachine Confiner на сцене!");
            return;
        }

        try
        {
            Type confinerType = confiner.GetType();

            // В Cinemachine v3 свойство называется BoundingShape2D
            PropertyInfo boundProp = confinerType.GetProperty("BoundingShape2D");
            if (boundProp == null)
            {
                // В Cinemachine v2 свойство называется m_BoundingShape2D
                boundProp = confinerType.GetProperty("m_BoundingShape2D") 
                            ?? confinerType.GetProperty("BoundingVolume"); // на всякий случай
            }

            if (boundProp != null)
            {
                // Если границы уже эти — ничего не делаем
                Collider2D currentBounds = boundProp.GetValue(confiner) as Collider2D;
                if (currentBounds == newBounds) return;

                // Назначаем новые границы
                boundProp.SetValue(confiner, newBounds);

                // Заставляем Cinemachine пересчитать кеш границ (InvalidateBoundingShapeCache / InvalidateCache)
                MethodInfo invalidateMethod = confinerType.GetMethod("InvalidateBoundingShapeCache") 
                                              ?? confinerType.GetMethod("InvalidateCache");
                if (invalidateMethod != null)
                {
                    invalidateMethod.Invoke(confiner, null);
                }

                Debug.Log($"[CameraManager] Камера заблокирована в границах новой комнаты: {newBounds.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[CameraManager] Не удалось найти свойство BoundingShape2D в компоненте конфинера.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CameraManager] Ошибка при установке границ камеры: {e.Message}");
        }
    }

    private Component FindConfinerOnScene()
    {
        string[] confinerTypes = new string[]
        {
            "Unity.Cinemachine.CinemachineConfiner2D", // v3
            "Cinemachine.CinemachineConfiner",         // v2
            "Unity.Cinemachine.CinemachineConfiner"
        };

        foreach (var typeName in confinerTypes)
        {
            Type t = Type.GetType(typeName + ", Unity.Cinemachine") 
                     ?? Type.GetType(typeName + ", Cinemachine")
                     ?? Type.GetType(typeName);
            if (t != null)
            {
                #pragma warning disable CS0618
                Component found = FindObjectOfType(t) as Component;
                #pragma warning restore CS0618
                if (found != null) return found;
            }
        }
        return null;
    }
}

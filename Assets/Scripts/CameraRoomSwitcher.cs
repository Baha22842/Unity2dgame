using UnityEngine;
using System;
using System.Reflection;

public class CameraRoomSwitcher : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Тег, который должен быть у триггеров комнат. По умолчанию 'Room'.")]
    public string roomTag = "Room";

    private Component _confiner;
    private Type _confinerType;

    private void Start()
    {
        FindConfiner();
    }

    private void FindConfiner()
    {
        string[] confinerTypes = new string[]
        {
            "Unity.Cinemachine.CinemachineConfiner2D", // v3
            "Cinemachine.CinemachineConfiner",         // v2
            "Unity.Cinemachine.CinemachineConfiner"
        };

        foreach (var typeName in confinerTypes)
        {
            _confinerType = CinemachineReflectionHelper.FindType(typeName);
            if (_confinerType != null)
            {
                #pragma warning disable CS0618
                _confiner = FindObjectOfType(_confinerType) as Component;
                #pragma warning restore CS0618
                if (_confiner != null)
                {
                    Debug.Log($"[CameraRoomSwitcher] Нашли конфайнер камеры: {_confiner.gameObject.name} ({_confinerType.Name})");
                    break;
                }
            }
        }

        if (_confiner == null)
        {
            Debug.LogWarning("[CameraRoomSwitcher] Не удалось найти компонент Cinemachine Confiner на сцене!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        SwitchRoomBounds(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        SwitchRoomBounds(other);
    }

    private void SwitchRoomBounds(Collider2D roomCollider)
    {
        if (roomCollider.CompareTag(roomTag))
        {
            if (_confiner == null)
            {
                FindConfiner();
                if (_confiner == null) return;
            }

            try
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // В Cinemachine v3 свойство называется BoundingShape2D, в v2 - m_BoundingShape2D
                PropertyInfo boundProp = _confinerType.GetProperty("BoundingShape2D", flags)
                                         ?? _confinerType.GetProperty("m_BoundingShape2D", flags)
                                         ?? _confinerType.GetProperty("BoundingVolume", flags);

                FieldInfo boundField = null;
                if (boundProp == null)
                {
                    boundField = _confinerType.GetField("BoundingShape2D", flags)
                                 ?? _confinerType.GetField("m_BoundingShape2D", flags)
                                 ?? _confinerType.GetField("m_BoundingVolume", flags);
                }

                Collider2D currentBounds = null;
                if (boundProp != null)
                {
                    currentBounds = boundProp.GetValue(_confiner) as Collider2D;
                }
                else if (boundField != null)
                {
                    currentBounds = boundField.GetValue(_confiner) as Collider2D;
                }

                // Меняем границы, только если они отличаются
                if (currentBounds != roomCollider)
                {
                    // 1. Сначала сбрасываем в null для полной очистки кэша
                    if (boundProp != null) boundProp.SetValue(_confiner, null);
                    else if (boundField != null) boundField.SetValue(_confiner, null);

                    MethodInfo invalidateMethod = _confinerType.GetMethod("InvalidateBoundingShapeCache", flags) 
                                                  ?? _confinerType.GetMethod("InvalidateCache", flags);
                    if (invalidateMethod != null)
                    {
                        invalidateMethod.Invoke(_confiner, null);
                    }

                    // 2. Устанавливаем новые границы комнат
                    if (boundProp != null) boundProp.SetValue(_confiner, roomCollider);
                    else if (boundField != null) boundField.SetValue(_confiner, roomCollider);

                    // 3. Снова обновляем кэш
                    if (invalidateMethod != null)
                    {
                        invalidateMethod.Invoke(_confiner, null);
                    }

                    Debug.Log($"[CameraRoomSwitcher] Успешно переключили границы камеры на комнату: {roomCollider.gameObject.name}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CameraRoomSwitcher] Ошибка при смене границ комнаты: " + ex.Message);
            }
        }
    }
}

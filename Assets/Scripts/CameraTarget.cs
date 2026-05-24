using UnityEngine;
using System;
using System.Reflection;

public class CameraTarget : MonoBehaviour
{
    private void Start()
    {
        // 1. Пытаемся найти Cinemachine камеру через рефлексию, 
        // чтобы избежать ошибок компиляции при отсутствии пакета Cinemachine или разнице его версий (v2 / v3)
        Component cinemachineCam = null;

        // Список возможных типов виртуальных камер для Cinemachine v2 и v3
        string[] typeNames = new string[] 
        {
            "Unity.Cinemachine.CinemachineCamera",           // Cinemachine v3 (новые версии Unity)
            "Cinemachine.CinemachineVirtualCamera",          // Cinemachine v2 (старые версии Unity)
            "Unity.Cinemachine.CinemachineVirtualCamera"     // Переходные версии
        };

        Type cameraType = null;
        foreach (var name in typeNames)
        {
            cameraType = Type.GetType(name + ", Unity.Cinemachine") 
                         ?? Type.GetType(name + ", Cinemachine")
                         ?? Type.GetType(name);
            if (cameraType != null)
            {
                // Нашли тип камеры! Пробуем найти объект этого типа на сцене
                #pragma warning disable CS0618
                cinemachineCam = FindObjectOfType(cameraType) as Component;
                #pragma warning restore CS0618
                if (cinemachineCam != null)
                {
                    break;
                }
            }
        }

        if (cinemachineCam != null && cameraType != null)
        {
            // Пытаемся установить свойство Follow
            PropertyInfo followProperty = cameraType.GetProperty("Follow");
            if (followProperty != null)
            {
                followProperty.SetValue(cinemachineCam, transform);
                Debug.Log($"[CameraTarget] Успешно привязали виртуальную камеру {cameraType.Name} к игроку через рефлексию!");
                return;
            }
        }

        // 2. Если Cinemachine не используется или не найден, настраиваем обычную камеру со скриптом CameraFollow
        CameraMainSetup();
    }

    private void CameraMainSetup()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            #pragma warning disable CS0618
            mainCam = FindObjectOfType<Camera>();
            #pragma warning restore CS0618
        }

        if (mainCam != null)
        {
            // Отключаем мешающий CinemachineBrain на главной камере, если виртуальная камера не используется
            // (так как CinemachineBrain блокирует ручное перемещение камеры скриптом CameraFollow)
            try
            {
                foreach (var comp in mainCam.GetComponents<Component>())
                {
                    if (comp != null && (comp.GetType().Name == "CinemachineBrain" || comp.GetType().FullName.Contains("CinemachineBrain")))
                    {
                        PropertyInfo enabledProp = comp.GetType().GetProperty("enabled");
                        if (enabledProp != null)
                        {
                            enabledProp.SetValue(comp, false);
                            Debug.Log("[CameraTarget] Отключили CinemachineBrain на главной камере, чтобы он не блокировал CameraFollow.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CameraTarget] Не удалось проверить CinemachineBrain: " + ex.Message);
            }

            // Ищем или добавляем скрипт CameraFollow на главную камеру
            CameraFollow followScript = mainCam.GetComponent<CameraFollow>();
            if (followScript == null)
            {
                followScript = mainCam.gameObject.AddComponent<CameraFollow>();
                Debug.Log("[CameraTarget] Автоматически ДОБАВИЛИ скрипт CameraFollow на главную камеру!");
            }

            if (followScript != null)
            {
                followScript.target = transform;
                followScript.enabled = true;
                Debug.Log("[CameraTarget] Успешно привязали CameraFollow на главной камере к игроку!");
            }
        }
        else
        {
            Debug.LogError("[CameraTarget] На сцене вообще нет ни одной камеры (Camera)!");
        }
    }
}

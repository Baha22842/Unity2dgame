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
            "Unity.Cinemachine.CinemachineCamera",           // Cinemachine v3 (Unity 6)
            "Cinemachine.CinemachineVirtualCamera",          // Cinemachine v2
            "Unity.Cinemachine.CinemachineVirtualCamera"     // Переходные версии
        };

        Type cameraType = null;
        foreach (var name in typeNames)
        {
            cameraType = CinemachineReflectionHelper.FindType(name);
            if (cameraType != null)
            {
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
            // Используем BindingFlags для поиска как публичных, так и приватных свойств/полей
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // В Cinemachine v3 свойство называется "TrackingTarget", в Cinemachine v2 - "Follow"
            PropertyInfo targetProp = cameraType.GetProperty("TrackingTarget", flags)
                                      ?? cameraType.GetProperty("Follow", flags);
                                      
            FieldInfo targetField = null;
            if (targetProp == null)
            {
                targetField = cameraType.GetField("TrackingTarget", flags)
                              ?? cameraType.GetField("Follow", flags);
            }

            // Дополнительно ищем LookAt для старых версий (v2)
            PropertyInfo lookAtProp = cameraType.GetProperty("LookAt", flags);
            if (lookAtProp != null)
            {
                try { lookAtProp.SetValue(cinemachineCam, transform); } catch { }
            }

            if (targetProp != null)
            {
                try { targetProp.SetValue(cinemachineCam, transform); } catch { }
            }
            else if (targetField != null)
            {
                try { targetField.SetValue(cinemachineCam, transform); } catch { }
            }

            // Дополнительно ищем любые субкомпоненты Cinemachine v3 на этом же объекте
            // для 100% гарантии следования (например, CinemachineFollow, CinemachineRotationComposer и т.д.)
            try
            {
                foreach (var component in cinemachineCam.GetComponents<Component>())
                {
                    if (component == null) continue;
                    Type compType = component.GetType();
                    string compName = compType.Name;

                    if (compName.Contains("Follow") || compType.FullName.Contains("Follow"))
                    {
                        PropertyInfo p = compType.GetProperty("Target", flags) ?? compType.GetProperty("Follow", flags);
                        if (p != null) p.SetValue(component, transform);
                        
                        FieldInfo f = compType.GetField("Target", flags) ?? compType.GetField("Follow", flags);
                        if (f != null) f.SetValue(component, transform);
                    }
                    
                    if (compName.Contains("LookAt") || compName.Contains("Composer") || compType.FullName.Contains("LookAt") || compType.FullName.Contains("Composer"))
                    {
                        PropertyInfo p = compType.GetProperty("LookAtTarget", flags) ?? compType.GetProperty("LookAt", flags);
                        if (p != null) p.SetValue(component, transform);
                        
                        FieldInfo f = compType.GetField("LookAtTarget", flags) ?? compType.GetField("LookAt", flags);
                        if (f != null) f.SetValue(component, transform);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CameraTarget] Ошибка при настройке субкомпонентов Cinemachine: " + ex.Message);
            }

            Debug.Log("[CameraTarget] Успешно привязали виртуальную камеру и её субкомпоненты Cinemachine к игроку!");
            return;
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
            try
            {
                foreach (var comp in mainCam.GetComponents<Component>())
                {
                    if (comp != null && (comp.GetType().Name == "CinemachineBrain" || comp.GetType().FullName.Contains("CinemachineBrain")))
                    {
                        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                        PropertyInfo enabledProp = comp.GetType().GetProperty("enabled", flags);
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

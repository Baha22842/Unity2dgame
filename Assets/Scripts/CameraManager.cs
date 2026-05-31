using UnityEngine;
using System;
using System.Reflection;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Настройки Камеры")]
    [Tooltip("Компонент CinemachineConfiner2D (для Cinemachine v3) или CinemachineConfiner (для Cinemachine v2). Оставьте пустым для автонастройки.")]
    public Component confiner;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // При старте игры сканируем сцену, находим в какой комнате игрок и мгновенно
        // зажимаем камеру в этой комнате без дерганий и пустоты!
        MapRoomTrigger[] triggers = FindObjectsByType<MapRoomTrigger>(FindObjectsSortMode.None);
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }

        if (player != null)
        {
            Vector2 playerPos = player.transform.position;
            foreach (var trigger in triggers)
            {
                if (trigger != null && trigger.roomCameraBounds != null)
                {
                    if (trigger.roomCameraBounds.OverlapPoint(playerPos))
                    {
                        SetRoomBounds(trigger.roomCameraBounds);
                        return;
                    }
                }
            }
        }

        // Резервный запуск по первому найденному объекту с границами
        if (triggers.Length > 0 && triggers[0] != null && triggers[0].roomCameraBounds != null)
        {
            SetRoomBounds(triggers[0].roomCameraBounds);
        }
        else
        {
            PolygonCollider2D poly = FindFirstObjectByType<PolygonCollider2D>();
            if (poly != null)
            {
                SetRoomBounds(poly);
            }
        }
    }

    /// <summary>
    /// Эту функцию вызывает MapRoomTrigger, когда игрок заходит в новую комнату.
    /// Камера плавно переключится на новые границы и никогда не покажет пустоту за стенами!
    /// </summary>
    public void SetRoomBounds(PolygonCollider2D newBounds)
    {
        // 1. Попытка применить границы для Cinemachine (виртуальные камеры)
        if (confiner == null)
        {
            confiner = FindOrCreateConfinerOnScene();
        }

        if (confiner != null)
        {
            try
            {
                Type confinerType = confiner.GetType();

                // Ищем свойства и приватные/публичные поля во избежание пропусков
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // В Cinemachine v3 свойство называется BoundingShape2D, в v2 - m_BoundingShape2D
                PropertyInfo boundProp = confinerType.GetProperty("BoundingShape2D", flags)
                                         ?? confinerType.GetProperty("m_BoundingShape2D", flags)
                                         ?? confinerType.GetProperty("BoundingVolume", flags);

                FieldInfo boundField = null;
                if (boundProp == null)
                {
                    boundField = confinerType.GetField("BoundingShape2D", flags)
                                 ?? confinerType.GetField("m_BoundingShape2D", flags)
                                 ?? confinerType.GetField("BoundingVolume", flags);
                }

                if (boundProp != null)
                {
                    Collider2D currentBounds = boundProp.GetValue(confiner) as Collider2D;
                    if (currentBounds != newBounds)
                    {
                        // Сначала сбрасываем в null для полной очистки старого кэша
                        boundProp.SetValue(confiner, null);

                        MethodInfo invalidateMethod = confinerType.GetMethod("InvalidateBoundingShapeCache", flags) 
                                                      ?? confinerType.GetMethod("InvalidateCache", flags);
                        if (invalidateMethod != null)
                        {
                            invalidateMethod.Invoke(confiner, null);
                        }

                        // Назначаем новые границы
                        boundProp.SetValue(confiner, newBounds);

                        // Заставляем пересчитать кэш для новых границ
                        if (invalidateMethod != null)
                        {
                            invalidateMethod.Invoke(confiner, null);
                        }

                        Debug.Log($"[CameraManager] Cinemachine камера заблокирована в границах (свойство {boundProp.Name}): {newBounds.gameObject.name}");
                    }
                }
                else if (boundField != null)
                {
                    Collider2D currentBounds = boundField.GetValue(confiner) as Collider2D;
                    if (currentBounds != newBounds)
                    {
                        // Сначала сбрасываем в null
                        boundField.SetValue(confiner, null);

                        MethodInfo invalidateMethod = confinerType.GetMethod("InvalidateBoundingShapeCache", flags) 
                                                      ?? confinerType.GetMethod("InvalidateCache", flags);
                        if (invalidateMethod != null)
                        {
                            invalidateMethod.Invoke(confiner, null);
                        }

                        // Назначаем новые границы в поле
                        boundField.SetValue(confiner, newBounds);

                        // Заставляем пересчитать кэш
                        if (invalidateMethod != null)
                        {
                            invalidateMethod.Invoke(confiner, null);
                        }

                        Debug.Log($"[CameraManager] Cinemachine камера заблокирована в границах (поле {boundField.Name}): {newBounds.gameObject.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CameraManager] Не удалось настроить Cinemachine: {e.Message}");
            }
        }

        // 2. Всегда передаем границы в стандартную камеру со скриптом CameraFollow
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraFollow follow = mainCam.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.SetBounds(newBounds);
                Debug.Log($"[CameraManager] Стандартная камера CameraFollow заблокирована в границах: {newBounds.gameObject.name}");
            }
        }

        // 3. Мгновенно телепортируем (Force Position) виртуальную камеру на координаты игрока,
        // чтобы избежать зависания камеры в старом центре или бесконечного скольжения.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }

        if (player != null)
        {
            try
            {
                string[] vcamTypes = new string[]
                {
                    "Unity.Cinemachine.CinemachineCamera",         // Cinemachine v3
                    "Cinemachine.CinemachineVirtualCamera",        // Cinemachine v2
                    "Unity.Cinemachine.CinemachineVirtualCamera"
                };

                foreach (var typeName in vcamTypes)
                {
                    Type vcamType = CinemachineReflectionHelper.FindType(typeName);
                    if (vcamType != null)
                    {
                        #pragma warning disable CS0618
                        Component vcam = FindObjectOfType(vcamType) as Component;
                        #pragma warning restore CS0618
                        if (vcam != null)
                        {
                            MethodInfo forcePosMethod = vcamType.GetMethod("ForceCameraPosition", new Type[] { typeof(Vector3), typeof(Quaternion) });
                            if (forcePosMethod != null)
                            {
                                Vector3 targetPos = new Vector3(player.transform.position.x, player.transform.position.y, vcam.transform.position.z);
                                forcePosMethod.Invoke(vcam, new object[] { targetPos, Quaternion.identity });
                                Debug.Log($"[CameraManager] Мгновенно переместили Cinemachine камеру к игроку при смене комнаты!");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CameraManager] Ошибка при сбросе позиции камеры: " + ex.Message);
            }
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
            Type t = CinemachineReflectionHelper.FindType(typeName);
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

    private Component FindOrCreateConfinerOnScene()
    {
        Component found = FindConfinerOnScene();
        if (found != null) return found;

        // Если конфайнер не найден, автоматически добавляем его на виртуальную камеру!
        string[] vcamTypes = new string[]
        {
            "Unity.Cinemachine.CinemachineCamera",         // Cinemachine v3
            "Cinemachine.CinemachineVirtualCamera",        // Cinemachine v2
            "Unity.Cinemachine.CinemachineVirtualCamera"
        };

        foreach (var typeName in vcamTypes)
        {
            Type vcamType = CinemachineReflectionHelper.FindType(typeName);
            if (vcamType != null)
            {
                #pragma warning disable CS0618
                Component vcam = FindObjectOfType(vcamType) as Component;
                #pragma warning restore CS0618
                
                if (vcam != null)
                {
                    string confinerTypeName = typeName.Contains("CinemachineCamera") 
                        ? "Unity.Cinemachine.CinemachineConfiner2D" 
                        : "Cinemachine.CinemachineConfiner";
                        
                    Type confinerType = CinemachineReflectionHelper.FindType(confinerTypeName);
                                        
                    if (confinerType != null)
                    {
                        Component newConfiner = vcam.gameObject.AddComponent(confinerType);
                        Debug.Log($"[CameraManager] Автоматически ДОБАВИЛИ конфайнер {confinerType.Name} на объект виртуальной камеры {vcam.gameObject.name}!");
                        return newConfiner;
                    }
                }
            }
        }
        return null;
    }
}

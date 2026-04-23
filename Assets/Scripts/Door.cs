using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("ID должен совпадать с ID кнопки (PressurePlate)")]
    public string expectedId = "Door_1"; 
    
    [Header("Как далеко открывается дверь")]
    public float openDistance = 3f; 
    public float speed = 3f;

    [Header("Логика головоломки")]
    [Tooltip("Сколько кнопок/рычагов должно быть включено одновременно, чтобы дверь открылась? (1 = любая кнопка, 2 = нужно нажать ровно две и т.д.)")]
    public int requiredSignals = 1;

    private int activeSignals = 0; // Считаем, сколько сигналов сейчас активно
    private Vector3 closedPosition;
    private Vector3 targetPosition;

    private void Start()
    {
        // Запоминаем изначальную позицию двери
        closedPosition = transform.position;
        targetPosition = closedPosition;
    }

    // Подписываемся на события, когда объект включается
    private void OnEnable()
    {
        GameEventManager.OnSwitchActivated += TryOpenDoor;
        GameEventManager.OnSwitchDeactivated += TryCloseDoor;
    }

    // Отписываемся, когда объект выключается (чтобы не было ошибок памяти)
    private void OnDisable()
    {
        GameEventManager.OnSwitchActivated -= TryOpenDoor;
        GameEventManager.OnSwitchDeactivated -= TryCloseDoor;
    }

    private void Update()
    {
        // Дверь всегда плавно едет к своей цели (закрытой или открытой позиции)
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }

    private void TryOpenDoor(string switchId)
    {
        // Если кто-то нажал кнопку с НАШИМ ID - мы увеличиваем счетчик активных сигналов
        if (switchId == expectedId)
        {
            activeSignals++;
            UpdateDoorState();
        }
    }

    private void TryCloseDoor(string switchId)
    {
        // Если отпустили кнопку с нашим ID - уменьшаем счетчик
        if (switchId == expectedId)
        {
            activeSignals--;
            if (activeSignals < 0) activeSignals = 0; // На всякий случай блокируем отрицательные значения
            UpdateDoorState();
        }
    }

    private void UpdateDoorState()
    {
        // Проверяем, хватает ли нам активных сигналов для открытия
        if (activeSignals >= requiredSignals)
        {
            targetPosition = closedPosition + Vector3.up * openDistance;
        }
        else
        {
            targetPosition = closedPosition;
        }
    }
}

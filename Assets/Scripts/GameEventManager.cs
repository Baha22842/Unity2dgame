using System;

public static class GameEventManager
{
    // Событие, которое передает текстовый ID (например "Door_1")
    public static event Action<string> OnSwitchActivated;
    public static event Action<string> OnSwitchDeactivated;

    // Этот метод мы будем вызывать из кнопок и рычагов
    public static void TriggerSwitch(string switchId, bool isActivated)
    {
        if (isActivated)
        {
            OnSwitchActivated?.Invoke(switchId);
        }
        else
        {
            OnSwitchDeactivated?.Invoke(switchId);
        }
    }
}

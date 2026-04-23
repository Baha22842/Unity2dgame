using UnityEngine;

public class Lever : MonoBehaviour, IHittable
{
    [Header("Настройки")]
    public string targetId = "Door_1";

    [Header("Визуал")]
    public Sprite activatedSprite;
    private Sprite deactivatedSprite;
    private SpriteRenderer sr;

    private bool isActivated = false;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            deactivatedSprite = sr.sprite;
        }
    }

    // Этот метод вызывается из PlayerCombat, когда меч касается Рычага
    public void OnHit()
    {
        // Переключаем состояние (вкл -> выкл, выкл -> вкл)
        isActivated = !isActivated;

        // Меняем картинку
        if (sr != null)
        {
            sr.sprite = isActivated ? activatedSprite : deactivatedSprite;
        }

        // Отправляем сигнал двери
        GameEventManager.TriggerSwitch(targetId, isActivated);
    }
}

using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("ID должен совпадать с ID двери, которую мы хотим открыть")]
    public string targetId = "Door_1"; 
    
    [Header("Визуал")]
    public Sprite pressedSprite;
    private Sprite unpressedSprite;
    private SpriteRenderer sr;

    private int objectsOnPlate = 0; // Считаем, сколько объектов стоит на кнопке

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            unpressedSprite = sr.sprite;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Кнопку могут нажать Игрок или Ящик
        if (collision.CompareTag("Player") || collision.CompareTag("Box"))
        {
            objectsOnPlate++;
            
            // Если мы первый объект, который встал на кнопку - активируем её
            if (objectsOnPlate == 1)
            {
                if (sr != null && pressedSprite != null) sr.sprite = pressedSprite;
                
                // Кричим всем через менеджер: "Я нажата!"
                GameEventManager.TriggerSwitch(targetId, true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.CompareTag("Box"))
        {
            objectsOnPlate--;
            
            // Если на кнопке никого не осталось - выключаем её
            if (objectsOnPlate <= 0)
            {
                objectsOnPlate = 0;
                if (sr != null && unpressedSprite != null) sr.sprite = unpressedSprite;
                
                // Кричим всем: "Я отжата!"
                GameEventManager.TriggerSwitch(targetId, false);
            }
        }
    }
}

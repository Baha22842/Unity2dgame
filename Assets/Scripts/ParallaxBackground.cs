using UnityEngine;

// Заставляем этот скрипт всегда выполняться ПОСЛЕ камеры (чтобы не было дерганий)
[DefaultExecutionOrder(10)]
public class ParallaxBackground : MonoBehaviour
{
    [Tooltip("Интенсивность параллакса (0 - стоит на месте, 1 - двигается вместе с камерой)")]
    public float parallaxEffect;

    private GameObject cam;
    private float startposX;

    void Start()
    {
        cam = Camera.main.gameObject;
        startposX = transform.position.x;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // Вычисляем, насколько нужно сдвинуть фон
        float dist = (cam.transform.position.x * parallaxEffect);

        // Плавно двигаем всю группу (или отдельный объект)
        transform.position = new Vector3(startposX + dist, transform.position.y, transform.position.z);
    }
}

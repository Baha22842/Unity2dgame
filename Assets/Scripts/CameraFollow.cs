using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    private void LateUpdate()
    {
        // Если цель не назначена (например, игрок заспавнился заново) — пытаемся найти объект с тегом Player
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        if (target == null) return;

        transform.position = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );
    }
}

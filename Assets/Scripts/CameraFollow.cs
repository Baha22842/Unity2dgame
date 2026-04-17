using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    private void LateUpdate()
    {
        // If target points to a prefab asset (not a scene object), clear it so we can auto-find the spawned player.
        if (target != null && !target.gameObject.scene.IsValid())
        {
            target = null;
        }

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

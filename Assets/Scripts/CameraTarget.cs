using UnityEngine;
using Unity.Cinemachine;

public class CameraTarget : MonoBehaviour
{
    private void Start()
    {
        // Ищем на сцене любую активную камеру Cinemachine
        CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
        
        if (cam != null)
        {
            // Приказываем ей следить за этим объектом (за Игроком)
            cam.Follow = transform;
        }
        else
        {
            Debug.LogWarning("CameraTarget: На сцене не найдена CinemachineCamera!");
        }
    }
}

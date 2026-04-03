using UnityEngine;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    public Text scoreDisplay;

    private void OnEnable()
    {
        if (scoreDisplay != null && GameManager.Instance != null)
        {
            scoreDisplay.text = "Score: " + GameManager.Instance.Score;
        }
    }
}

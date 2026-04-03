using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player")]
    public Transform playerSpawnPoint;
    public GameObject playerPrefab;

    [Header("UI")]
    public Text scoreText;
    public Text livesText;
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Gameplay")]
    public int startLives = 3;

    private int score;
    private int lives;
    private GameObject currentPlayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
        if (losePanel != null)
        {
            losePanel.SetActive(false);
        }

        lives = startLives;
        UpdateLivesUI();
        UpdateScoreUI();

        if (playerSpawnPoint != null && playerPrefab != null)
        {
            SpawnPlayer();
        }
    }

    private void SpawnPlayer()
    {
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
        }

        currentPlayer = Instantiate(playerPrefab, playerSpawnPoint.position, Quaternion.identity);
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateScoreUI();
    }

    public void PlayerDied()
    {
        lives--;
        UpdateLivesUI();

        if (lives <= 0)
        {
            ShowLose();
        }
        else
        {
            if (playerSpawnPoint != null && playerPrefab != null)
            {
                SpawnPlayer();
            }
            else
            {
                RestartLevel();
            }
        }
    }

    public void WinLevel()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
    }

    private void ShowLose()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(true);
        }
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    private void UpdateLivesUI()
    {
        if (livesText != null)
        {
            livesText.text = "Lives: " + lives;
        }
    }
}


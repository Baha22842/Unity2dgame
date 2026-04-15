using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private static bool hasSavedProgress;
    private static int savedScore;
    private static int savedLives;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        ResetProgress();
    }

    public static void ResetProgress()
    {
        hasSavedProgress = false;
        savedScore = 0;
        savedLives = 0;
    }

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

    public int Score => score;
    public bool IsGameOver { get; private set; }

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

        if (hasSavedProgress)
        {
            score = savedScore;
            lives = savedLives;
        }
        else
        {
            score = 0;
            lives = startLives;
            hasSavedProgress = true;
            savedScore = score;
            savedLives = lives;
        }

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
        savedScore = score;
        UpdateScoreUI();
    }

    public void PlayerDied()
    {
        lives--;
        savedLives = lives;
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
        IsGameOver = true;
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
        Time.timeScale = 0f;
    }

    private void ShowLose()
    {
        IsGameOver = true;
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(true);
        }
        Time.timeScale = 0f;
    }

    public void RestartLevel()
    {
        ResetProgress();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        Time.timeScale = 1f;
        IsGameOver = false;

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
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


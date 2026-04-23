using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static void ResetProgress()
    {
        SaveSystem.DeleteSave();
        if (Instance != null)
        {
            Instance.score = 0;
            Instance.lives = Instance.startLives;
            Instance.SaveGameData();
        }
    }

    public void SaveGameData()
    {
        SaveSystem.SaveGame(score, lives, SceneManager.GetActiveScene().buildIndex);
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

        // Загружаем данные с жесткого диска вместо статических переменных
        SaveData data = SaveSystem.LoadGame();
        if (data != null)
        {
            score = data.score;
            lives = data.lives;
        }
        else
        {
            score = 0;
            lives = startLives;
            SaveGameData();
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
        SaveGameData();
        UpdateScoreUI();
    }

    public void PlayerDied()
    {
        lives--;
        SaveGameData();
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

    public void HitStop(float duration = 0.05f)
    {
        // Вызываем корутину для остановки времени (читерский прием для сочности игры)
        StartCoroutine(HitStopRoutine(duration));
    }

    private System.Collections.IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.1f; // Замедляем время почти до нуля
        yield return new WaitForSecondsRealtime(duration); // Ждем долю секунды реального времени
        if (!IsGameOver) // Проверка: если за это время мы случайно не умерли
        {
            Time.timeScale = 1f; // Возвращаем обычное течение времени
        }
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


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
            Instance.currentHealth = Instance.maxHealth;
            Instance.collectedArtifacts = 0;
            Instance.SaveGameData();
        }
    }

    [Header("Навыки Игрока (Метроидвания)")]
    public bool hasDoubleJump = false;
    public bool hasDash = false;
    public bool hasHeavyAttack = false;

    public void SaveGameData()
    {
        // Мы сохраняем currentHealth в параметр lives, чтобы не ломать SaveSystem
        SaveSystem.SaveGame(score, currentHealth, UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, hasDoubleJump, hasDash, hasHeavyAttack, collectedArtifacts, exploredRooms);
    }

    [Header("Player")]
    public Transform playerSpawnPoint;
    public GameObject playerPrefab;

    [Header("UI")]
    public Text scoreText;
    public GameObject[] healthPoints; // Массив квадратиков здоровья (вместо текста)
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Сюжетные Артефакты (Ключи)")]
    public int collectedArtifacts = 0;
    public int requiredArtifacts = 3;
    public GameObject[] artifactIcons; // Иконки ключей в UI (холст)

    [Header("Карта (Метроидвания)")]
    public System.Collections.Generic.List<string> exploredRooms = new System.Collections.Generic.List<string>();

    [Header("Gameplay (Здоровье)")]
    public int maxHealth = 5;

    private int score;
    private int currentHealth;
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
            currentHealth = data.lives; // в старых сохранениях это lives
            if (currentHealth <= 0) currentHealth = maxHealth; // защита от бага при загрузке мертвого перса

            hasDoubleJump = data.hasDoubleJump;
            hasDash = data.hasDash;
            hasHeavyAttack = data.hasHeavyAttack;

            collectedArtifacts = data.collectedArtifacts;
            exploredRooms = data.exploredRooms != null ? data.exploredRooms : new System.Collections.Generic.List<string>();
        }
        else
        {
            score = 0;
            currentHealth = maxHealth;
            hasDoubleJump = false;
            hasDash = false;
            hasHeavyAttack = false;
            collectedArtifacts = 0;
            exploredRooms = new System.Collections.Generic.List<string>();
            SaveGameData();
        }

        UpdateHealthUI();
        UpdateScoreUI();
        UpdateArtifactsUI();

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

    public void UnlockAbility(string abilityName)
    {
        switch (abilityName)
        {
            case "DoubleJump": hasDoubleJump = true; break;
            case "Dash": hasDash = true; break;
            case "HeavyAttack": hasHeavyAttack = true; break;
        }
        SaveGameData();
        Debug.Log("Разблокирован новый навык: " + abilityName);
    }

    // --- ЛОГИКА АРТЕФАКТОВ ---
    public void CollectArtifact()
    {
        collectedArtifacts++;
        if (collectedArtifacts > requiredArtifacts) collectedArtifacts = requiredArtifacts;
        
        UpdateArtifactsUI();
        SaveGameData();
        Debug.Log("Собран артефакт! Всего: " + collectedArtifacts + " / " + requiredArtifacts);
    }

    private bool isRespawning = false;

    public void PlayerDied()
    {
        if (!isRespawning && !IsGameOver)
        {
            // Убедимся, что здоровье равно 0, если нас убила ловушка, чтобы не было десинхронизации UI
            currentHealth = 0;
            StartCoroutine(PlayerDiedRoutine());
        }
    }

    public void Heal(int amount = 1)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        SaveGameData();
        UpdateHealthUI();
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        SaveGameData();
        UpdateHealthUI();

        if (currentHealth <= 0 && !isRespawning && !IsGameOver)
        {
            // Если здоровье упало до 0, запускаем смерть
            StartCoroutine(PlayerDiedRoutine());
        }
    }

    private System.Collections.IEnumerator PlayerDiedRoutine()
    {
        isRespawning = true;
        // Убираем currentHealth-- здесь, так как мы уже либо получили урон в TakeDamage,
        // либо насильно сбросили в 0 в PlayerDied (от ловушки). Иначе здоровье уходит в минус.
        SaveGameData();
        UpdateHealthUI();

        if (currentPlayer != null)
        {
            PlayerAnimator pa = currentPlayer.GetComponent<PlayerAnimator>();
            PlayerMovement pm = currentPlayer.GetComponent<PlayerMovement>();
            
            if (pm != null) 
            {
                pm.Die(); // Новый метод, который полностью отключает кнопки
            }
            if (pa != null) pa.TriggerDie();
        }

        // Страховка: если игрок был поставлен на сцену вручную (и GameManager о нем не знал)
        PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            p.Die();
            PlayerAnimator panicAnim = p.GetComponent<PlayerAnimator>();
            if (panicAnim != null) panicAnim.TriggerDie();
        }

        // Ждем 1 секунду, пока проигрывается анимация смерти
        yield return new WaitForSeconds(1f);

        if (currentHealth <= 0)
        {
            ShowLose(); // Экран смерти (можно убрать потом и сделать бесконечное возрождение)
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
        
        isRespawning = false;
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

    private void UpdateHealthUI()
    {
        if (healthPoints == null) return;

        // Включаем квадратики в зависимости от currentHealth
        for (int i = 0; i < healthPoints.Length; i++)
        {
            if (i < currentHealth)
            {
                healthPoints[i].SetActive(true); // Квадратик красный/активный
            }
            else
            {
                healthPoints[i].SetActive(false); // Квадратик пропал
            }
        }
    }

    private void UpdateArtifactsUI()
    {
        if (artifactIcons == null) return;

        // Включаем ключи в интерфейсе
        for (int i = 0; i < artifactIcons.Length; i++)
        {
            if (i < collectedArtifacts)
            {
                artifactIcons[i].SetActive(true); // Ключ найден
            }
            else
            {
                artifactIcons[i].SetActive(false); // Ключ пока не найден (или скрыт)
            }
        }
    }
}


using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static void ResetProgress()
    {
        SaveSystem.DeleteSave(SaveSystem.SelectedSlot);
        if (Instance != null)
        {
            Instance.score = 0;
            Instance.maxHealth = 3;
            Instance.currentHealth = 3;
            Instance.hasDoubleJump = false;
            Instance.hasDash = false;
            Instance.hasHeavyAttack = false;
            Instance.hasThrust = false;
            Instance.collectedArtifacts = 0;
            Instance.totalPlayTime = 0f;
            Instance.potionsCount = 0;

            string potionsKey = "Slot_" + SaveSystem.SelectedSlot + "_Potions";
            PlayerPrefs.DeleteKey(potionsKey);
            PlayerPrefs.Save();

            Instance.SaveGameData();
        }
    }

    [Header("Навыки Игрока (Метроидвания)")]
    public bool hasDoubleJump = false;
    public bool hasDash = false;
    public bool hasHeavyAttack = false;
    public bool hasThrust = false;

    [Header("Зелья Целительного Духа")]
    public int potionsCount;
    public int maxPotions = 3;
    public int coinCraftCost = 30;
    public bool autoCraftPotionWithCoins = true;

    [Header("Штраф при смерти")]
    [Range(0f, 1f)] public float soulLossPercentage = 0.45f;

    public void SaveGameData()
    {
        SaveSystem.SaveGame(
            SaveSystem.SelectedSlot,
            score,
            currentHealth,
            maxHealth,
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
            hasDoubleJump,
            hasDash,
            hasHeavyAttack,
            hasThrust,
            collectedArtifacts,
            exploredRooms,
            totalPlayTime
        );
    }

    [Header("Player")]
    public Transform playerSpawnPoint;
    public GameObject playerPrefab;

    [Header("UI")]
    public Text scoreText; // Старый совмещенный текст (для обратной совместимости)
    public TMPro.TextMeshProUGUI soulsText; // Отдельный текст для душ (поместите рядом с иконкой душ)
    public TMPro.TextMeshProUGUI potionsText; // Отдельный текст для зелий (поместите рядом с иконкой зелий)
    public GameObject[] healthPoints; // Массив квадратиков здоровья (вместо текста)
    
    [Header("Heart Animation Settings")]
    [Tooltip("Название триггера в Аниматоре сердца, который запускает анимацию его убавления/исчезновения")]
    [SerializeField] private string heartBreakTrigger = "Hurt";
    [Tooltip("Название триггера в Аниматоре сердца для возвращения в полное состояние")]
    [SerializeField] private string heartResetTrigger = "Reset";
    [Tooltip("Время задержки в секундах перед отключением GameObject сердца (SetActive(false)), чтобы анимация успела проиграться")]
    [SerializeField] private float heartDisableDelay = 0.5f;

    private Coroutine[] _heartCoroutines;

    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Сюжетные Артефакты (Ключи)")]
    public int collectedArtifacts = 0;
    public int requiredArtifacts = 3;
    public GameObject[] artifactIcons; // Иконки ключей в UI (холст)

    [Header("Карта (Метроидвания)")]
    public System.Collections.Generic.List<string> exploredRooms = new System.Collections.Generic.List<string>();

    [Header("Gameplay (Здоровье)")]
    public int maxHealth = 3;

    private int score;
    private int currentHealth;
    private GameObject currentPlayer;
    private float totalPlayTime = 0f;
    private Vector3 lastSafePosition;
    private CanvasGroup fadeCanvasGroup;

    public int Score => score;
    public int CurrentHealth => currentHealth;
    public bool IsHealthFull => currentHealth >= maxHealth;
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (healthPoints != null)
        {
            _heartCoroutines = new Coroutine[healthPoints.Length];
        }

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
        if (losePanel != null)
        {
            losePanel.SetActive(false);
        }

        CreateFadeCanvas();

        // Загружаем данные с жесткого диска вместо статических переменных
        SaveData data = SaveSystem.LoadGame(SaveSystem.SelectedSlot);
        if (data != null)
        {
            score = data.score;
            
            // Загружаем максимальное здоровье, если оно сохранено
            if (data.maxHealth > 0)
            {
                maxHealth = data.maxHealth;
            }
            else
            {
                maxHealth = 3;
            }
            
            currentHealth = data.lives; // в старых сохранениях это lives
            if (currentHealth <= 0) currentHealth = maxHealth; // защита от бага при загрузке мертвого перса

            hasDoubleJump = data.hasDoubleJump;
            hasDash = data.hasDash;
            hasHeavyAttack = data.hasHeavyAttack;
            hasThrust = data.hasThrust;

            collectedArtifacts = data.collectedArtifacts;
            exploredRooms = data.exploredRooms != null ? data.exploredRooms : new System.Collections.Generic.List<string>();
            totalPlayTime = data.totalPlayTime;

            // Загружаем количество зелий из PlayerPrefs для этого слота
            string potionsKey = "Slot_" + SaveSystem.SelectedSlot + "_Potions";
            potionsCount = PlayerPrefs.GetInt(potionsKey, 0);
        }
        else
        {
            score = 0;
            maxHealth = 3; // Всегда начинаем новую игру с 3 сердцами
            currentHealth = maxHealth;
            hasDoubleJump = false;
            hasDash = false;
            hasHeavyAttack = false;
            hasThrust = false;
            collectedArtifacts = 0;
            exploredRooms = new System.Collections.Generic.List<string>();
            totalPlayTime = 0f;

            potionsCount = 0;
            string potionsKey = "Slot_" + SaveSystem.SelectedSlot + "_Potions";
            PlayerPrefs.DeleteKey(potionsKey);
            PlayerPrefs.Save();

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

    private void Update()
    {
        if (!IsGameOver && Time.timeScale > 0f)
        {
            totalPlayTime += Time.deltaTime;
        }

        UpdateLastSafePosition();
    }

    private void UpdateLastSafePosition()
    {
        if (currentPlayer == null) return;

        PlayerMovement pm = currentPlayer.GetComponent<PlayerMovement>();
        if (pm != null && pm.IsGrounded && (pm.CurrentState == PlayerMovement.PlayerState.Idle || pm.CurrentState == PlayerMovement.PlayerState.Run))
        {
            Rigidbody2D rb = currentPlayer.GetComponent<Rigidbody2D>();
            if (rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.05f)
            {
                lastSafePosition = currentPlayer.transform.position;
            }
        }
    }

    private void SpawnPlayer()
    {
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
        }

        currentPlayer = Instantiate(playerPrefab, playerSpawnPoint.position, Quaternion.identity);

        if (playerSpawnPoint != null)
        {
            lastSafePosition = playerSpawnPoint.position;
        }
    }

    private void SpawnPlayerAtPosition(Vector3 position)
    {
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
        }

        currentPlayer = Instantiate(playerPrefab, position, Quaternion.identity);
    }

    public void AddScore(int amount)
    {
        score += amount;
        TryAutoCraftPotion();
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
            case "Thrust": hasThrust = true; break;
        }
        SaveGameData();
        Debug.Log("Разблокирован новый навык: " + abilityName);
    }

    public void TryAutoCraftPotion()
    {
        if (autoCraftPotionWithCoins && potionsCount < maxPotions)
        {
            while (score >= coinCraftCost && potionsCount < maxPotions)
            {
                score -= coinCraftCost;
                potionsCount++;
                Debug.Log($"🏺 [КРАФТ] Автоматически создано зелье целительного духа! Зелий: {potionsCount}/{maxPotions}. Оставшиеся духи: {score}");
                
                string potionsKey = "Slot_" + SaveSystem.SelectedSlot + "_Potions";
                PlayerPrefs.SetInt(potionsKey, potionsCount);
                PlayerPrefs.Save();
            }
        }
    }

    public void UsePotion()
    {
        if (potionsCount > 0)
        {
            potionsCount--;
            string potionsKey = "Slot_" + SaveSystem.SelectedSlot + "_Potions";
            PlayerPrefs.SetInt(potionsKey, potionsCount);
            PlayerPrefs.Save();

            Heal(1); // Восстанавливаем только 1 ХП (одно сердечко) вместо полного здоровья
            UpdateScoreUI(); // Немедленно обновляем HUD зелий
            Debug.Log($"🏺 [ЗЕЛЬЕ] Зелье использовано! Восстановлено 1 ХП. Зелий осталось: {potionsCount}");
        }
    }

    public void IncreaseMaxHealth(int amount = 1)
    {
        maxHealth += amount;
        
        if (healthPoints != null)
        {
            if (maxHealth > healthPoints.Length)
            {
                maxHealth = healthPoints.Length;
            }
            _heartCoroutines = new Coroutine[healthPoints.Length];
        }
        
        currentHealth = maxHealth;
        SaveGameData();
        
        UpdateHealthUI();
        
        Debug.Log($"💖 [МАКС ХП] Максимальное здоровье увеличено на {amount}! Текущее макс ХП: {maxHealth}");
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
        currentHealth--;

        // Применяем штраф списания духов только при окончательной потере всех жизней (Game Over)
        if (currentHealth <= 0)
        {
            int lostSpirits = Mathf.RoundToInt(score * soulLossPercentage);
            score -= lostSpirits;
            if (score < 0) score = 0;
            Debug.Log($"🏺 [ПОЛНАЯ СМЕРТЬ] Игрок потерял все жизни! Потеряно {lostSpirits} целей. духов ({soulLossPercentage * 100}%).");
        }
        else
        {
            Debug.Log($"🏺 [УРОН] Игрок потерял 1 жизнь. Осталось жизней: {currentHealth}.");
        }

        SaveGameData();
        UpdateHealthUI();
        UpdateScoreUI();

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

        // Восполняем здоровье боссов и мобов до фулла при смерти игрока!
        HealAllEnemiesToFull();

        // 1. Затемнение экрана начинается МГНОВЕННО после смерти (длится 0.8 сек для плавности)
        yield return StartCoroutine(FadeRoutine(0f, 1f, 0.8f));

        // 2. Ждем задержку в 2.0 секунды на полностью черном экране
        yield return new WaitForSeconds(2.0f);

        if (currentHealth <= 0)
        {
            ShowLose(); // Экран смерти
        }
        else
        {
            if (lastSafePosition != Vector3.zero)
            {
                SpawnPlayerAtPosition(lastSafePosition);
            }
            else if (playerSpawnPoint != null && playerPrefab != null)
            {
                SpawnPlayer();
            }
            else
            {
                RestartLevel();
            }

            // Ждем 0.3 секунды в полной темноте для стабилизации камеры
            yield return new WaitForSeconds(0.3f);
        }

        // 3. Плавно возвращаем экран из черного (0.8 сек для плавности)
        yield return StartCoroutine(FadeRoutine(1f, 0f, 0.8f));
        
        isRespawning = false;
    }

    /// <summary>
    /// Полностью восстанавливает здоровье всем активным на сцене монстрам и боссам
    /// </summary>
    private void HealAllEnemiesToFull()
    {
        // 1. Исцеляем до фулла обычных врагов
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null) enemy.HealToFull();
        }

        // 2. Исцеляем до фулла красных боссов
        Boss[] bosses = FindObjectsByType<Boss>(FindObjectsSortMode.None);
        foreach (var boss in bosses)
        {
            if (boss != null) boss.HealToFull();
        }

        // 3. Исцеляем до фулла фиолетовых боссов (Bringer of Death)
        BringerOfDeath[] deathBringers = FindObjectsByType<BringerOfDeath>(FindObjectsSortMode.None);
        foreach (var db in deathBringers)
        {
            if (db != null) db.HealToFull();
        }

        Debug.Log("🏺 [ДУХИ] Все активные монстры и боссы на карте полностью исцелены после смерти игрока!");
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
            if (CelesteTransition.Instance == null)
            {
                GameObject manager = new GameObject("CelesteTransitionManager");
                manager.AddComponent<CelesteTransition>();
            }

            if (CelesteTransition.Instance != null)
            {
                CelesteTransition.Instance.TransitionToScene(nextIndex);
            }
            else
            {
                SceneManager.LoadScene(nextIndex);
            }
        }
        else
        {
            LoadMainMenu();
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        
        if (CelesteTransition.Instance == null)
        {
            GameObject manager = new GameObject("CelesteTransitionManager");
            manager.AddComponent<CelesteTransition>();
        }

        if (CelesteTransition.Instance != null)
        {
            CelesteTransition.Instance.TransitionToScene("MainMenu");
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
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

    public void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"🔮 {score}    🧪 {potionsCount}/{maxPotions}";
        }

        if (soulsText != null)
        {
            soulsText.text = score.ToString();
        }

        if (potionsText != null)
        {
            potionsText.text = $"{potionsCount}/{maxPotions}";
        }
    }

    private int _visualHealth = -1; // Текущее визуально отображаемое здоровье в интерфейсе
    private Coroutine _healthAnimationCoroutine;

    private void UpdateHealthUI()
    {
        if (healthPoints == null) return;

        // Обновляем видимость фоновых пустых сердечек (ячеек)
        UpdateBackgroundHeartsVisibility();

        // Ограничиваем активные элементы здоровья на основе maxHealth
        for (int i = 0; i < healthPoints.Length; i++)
        {
            if (healthPoints[i] != null && i >= maxHealth)
            {
                healthPoints[i].SetActive(false);
            }
        }

        // Если это первый запуск (или после смерти/загрузки), мгновенно выставляем нужное здоровье без каскадной задержки
        if (_visualHealth == -1)
        {
            _visualHealth = currentHealth;
            InstantUpdateHealthUI();
            return;
        }

        // Если визуальное здоровье не совпадает с реальным, запускаем красивый каскадный переход по одному сердцу
        if (_visualHealth != currentHealth)
        {
            if (_healthAnimationCoroutine != null)
            {
                StopCoroutine(_healthAnimationCoroutine);
            }
            _healthAnimationCoroutine = StartCoroutine(AnimateHealthChangeRoutine(_visualHealth, currentHealth));
        }
    }

    private void InstantUpdateHealthUI()
    {
        // Обновляем видимость фоновых ячеек
        UpdateBackgroundHeartsVisibility();

        for (int i = 0; i < healthPoints.Length; i++)
        {
            if (healthPoints[i] == null) continue;

            // Если индекс превышает максимальное здоровье, принудительно гасим и прячем сердце
            if (i >= maxHealth)
            {
                healthPoints[i].SetActive(false);
                continue;
            }

            Animator anim = healthPoints[i].GetComponent<Animator>();

            if (i < _visualHealth)
            {
                // Сердце должно быть полным
                if (_heartCoroutines != null && i < _heartCoroutines.Length && _heartCoroutines[i] != null)
                {
                    StopCoroutine(_heartCoroutines[i]);
                    _heartCoroutines[i] = null;
                }

                healthPoints[i].SetActive(true);

                if (anim != null)
                {
                    if (!string.IsNullOrEmpty(heartResetTrigger)) anim.ResetTrigger(heartResetTrigger);
                    if (!string.IsNullOrEmpty(heartBreakTrigger)) anim.ResetTrigger(heartBreakTrigger);
                }
            }
            else
            {
                // Сердце должно быть скрыто
                healthPoints[i].SetActive(false);
            }
        }
    }

    private System.Collections.IEnumerator AnimateHealthChangeRoutine(int fromHealth, int toHealth)
    {
        float cascadeDelay = 0.15f; // Время задержки между анимациями соседних сердечек (для каскадного эффекта)

        if (toHealth < fromHealth)
        {
            // УБАВЛЕНИЕ (Урон) -> Убавляем СПРАВА НАЛЕВО (от большего индекса к меньшему)
            for (int i = fromHealth - 1; i >= toHealth; i--)
            {
                if (i >= 0 && i < healthPoints.Length && healthPoints[i] != null)
                {
                    Animator anim = healthPoints[i].GetComponent<Animator>();

                    if (healthPoints[i].activeSelf)
                    {
                        if (anim != null)
                        {
                            // Запускаем анимацию разбивания сердца
                            if (!string.IsNullOrEmpty(heartBreakTrigger))
                            {
                                anim.SetTrigger(heartBreakTrigger);
                            }

                            // Выключаем объект с задержкой, чтобы анимация успела проиграться
                            if (_heartCoroutines != null && i < _heartCoroutines.Length)
                            {
                                if (_heartCoroutines[i] != null) StopCoroutine(_heartCoroutines[i]);
                                _heartCoroutines[i] = StartCoroutine(DisableHeartDelayed(healthPoints[i], i, heartDisableDelay));
                            }
                        }
                        else
                        {
                            healthPoints[i].SetActive(false);
                        }
                    }

                    _visualHealth = i;
                    yield return new WaitForSeconds(cascadeDelay);
                }
            }
        }
        else
        {
            // ВОССТАНОВЛЕНИЕ (Лечение/Возрождение) -> Добавляем СЛЕВА НАПРАВО (от меньшего индекса к большему)
            for (int i = fromHealth; i < toHealth; i++)
            {
                if (i >= 0 && i < healthPoints.Length && healthPoints[i] != null)
                {
                    Animator anim = healthPoints[i].GetComponent<Animator>();

                    if (_heartCoroutines != null && i < _heartCoroutines.Length && _heartCoroutines[i] != null)
                    {
                        StopCoroutine(_heartCoroutines[i]);
                        _heartCoroutines[i] = null;
                    }

                    healthPoints[i].SetActive(true);

                    if (anim != null)
                    {
                        // Запускаем анимацию сборки сердца
                        if (!string.IsNullOrEmpty(heartResetTrigger))
                        {
                            anim.SetTrigger(heartResetTrigger);
                        }
                        if (!string.IsNullOrEmpty(heartBreakTrigger))
                        {
                            anim.ResetTrigger(heartBreakTrigger);
                        }
                    }

                    _visualHealth = i + 1;
                    yield return new WaitForSeconds(cascadeDelay);
                }
            }
        }

        _visualHealth = toHealth;
        _healthAnimationCoroutine = null;
    }

    private System.Collections.IEnumerator DisableHeartDelayed(GameObject heart, int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (heart != null && index >= currentHealth)
        {
            heart.SetActive(false);
        }
        if (_heartCoroutines != null && index < _heartCoroutines.Length)
        {
            _heartCoroutines[index] = null;
        }
    }

    private void UpdateBackgroundHeartsVisibility()
    {
        Transform bgTransform = null;

        // Попытка найти через иерархию относительно активных сердечек (работает даже если выключено)
        if (healthPoints != null && healthPoints.Length > 0 && healthPoints[0] != null)
        {
            Transform healthBarTrans = healthPoints[0].transform.parent;
            if (healthBarTrans != null && healthBarTrans.parent != null)
            {
                bgTransform = healthBarTrans.parent.Find("HealthBar_Background");
            }
        }

        // Резервный поиск через GameObject.Find
        if (bgTransform == null)
        {
            GameObject bgGo = GameObject.Find("HealthBar_Background");
            if (bgGo != null) bgTransform = bgGo.transform;
        }

        if (bgTransform != null)
        {
            int childCount = bgTransform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = bgTransform.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(i < maxHealth);
                }
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

    private void CreateFadeCanvas()
    {
        // Создаем Canvas для затемнения при смерти
        GameObject canvasGo = new GameObject("DeathFadeCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998;
        
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Создаем черную панель на весь экран
        GameObject panelGo = new GameObject("DeathFadePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        
        RectTransform rect = panelGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        
        Image img = panelGo.AddComponent<Image>();
        img.color = Color.black;
        
        fadeCanvasGroup = panelGo.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        
        DontDestroyOnLoad(canvasGo);
    }

    private System.Collections.IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fadeCanvasGroup.alpha = Mathf.SmoothStep(startAlpha, endAlpha, t);
            yield return null;
        }
        fadeCanvasGroup.alpha = endAlpha;
    }
}


using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    [Header("Панели Интерфейса")]
    public GameObject pausePanel;         // Вся оверлей-панель паузы (черный полупрозрачный фон)
    public GameObject pauseButtonsPanel;   // Панель с кнопками паузы (книжка/свиток)
    public GameObject settingsPanel;       // Панель настроек (тоже книжка/свиток)

    [Header("Элементы Настроек")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle fullscreenToggle;
    public TMP_Dropdown musicDropDown;     // Дропдаун выбора фоновой музыки

    [Header("Кнопки Меню Паузы (Настроятся автоматически, если не заданы)")]
    public Button resumeButton;
    public Button settingsButton;
    public Button closeSettingsButton;
    public Button restartButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("Плейлист Сцены (BGM)")]
    public AudioClip[] bgmTracks;          // Треки для геймплея

    private bool isPaused = false;
    private AudioSource bgmAudioSource;    // Аудиосурс фоновой музыки на сцене

    // Рекурсивный поиск дочерних объектов по имени для автонастройки
    private GameObject FindChildByName(string childName)
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t.gameObject.name.ToLower() == childName.ToLower() || t.gameObject.name.ToLower().Contains(childName.ToLower()))
            {
                return t.gameObject;
            }
        }
        return null;
    }

    // Рекурсивный поиск кнопок по имени для автонастройки
    private Button FindButtonByName(string btnName)
    {
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        foreach (var btn in allButtons)
        {
            string nameLower = btn.gameObject.name.ToLower();
            string searchLower = btnName.ToLower();
            if (nameLower == searchLower || nameLower.Contains(searchLower))
            {
                return btn;
            }
        }
        return null;
    }

    private void Awake()
    {
        isPaused = false;
        Time.timeScale = 1f;

        // Автоматический поиск панелей, если они пропущены в Инспекторе
        if (pausePanel == null) pausePanel = FindChildByName("PausePanel");
        if (pausePanel == null) pausePanel = FindChildByName("PauseMenuPanel");
        if (pausePanel == null) pausePanel = FindChildByName("Background");

        if (pauseButtonsPanel == null) pauseButtonsPanel = FindChildByName("PauseButtonsPanel");
        if (pauseButtonsPanel == null) pauseButtonsPanel = FindChildByName("ButtonsPanel");
        if (pauseButtonsPanel == null) pauseButtonsPanel = FindChildByName("MainPanel");

        if (settingsPanel == null) settingsPanel = FindChildByName("SettingsPanel");
        if (settingsPanel == null) settingsPanel = FindChildByName("Settings");

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseButtonsPanel != null) pauseButtonsPanel.SetActive(true);
    }

    private void Start()
    {
        // Пытаемся найти фоновую музыку на сцене геймплея
        bgmAudioSource = FindFirstObjectByType<AudioSource>();

        // Автоматический поиск элементов настроек, если они пусты
        if (musicSlider == null)
        {
            musicSlider = FindChildByName("MusicSlider")?.GetComponent<Slider>();
            if (musicSlider == null) musicSlider = FindChildByName("VolumeSlider")?.GetComponent<Slider>();
            if (musicSlider == null) musicSlider = GetComponentInChildren<Slider>(true);
        }

        if (sfxSlider == null)
        {
            sfxSlider = FindChildByName("SFXSlider")?.GetComponent<Slider>();
            if (sfxSlider == null) sfxSlider = FindChildByName("SoundSlider")?.GetComponent<Slider>();
        }

        if (fullscreenToggle == null) fullscreenToggle = GetComponentInChildren<Toggle>(true);
        if (musicDropDown == null) musicDropDown = GetComponentInChildren<TMP_Dropdown>(true);

        // Автоматический поиск кнопок по имени
        if (resumeButton == null) resumeButton = FindButtonByName("ResumeButton");
        if (resumeButton == null) resumeButton = FindButtonByName("Resume");

        if (settingsButton == null) settingsButton = FindButtonByName("SettingsButton");
        if (settingsButton == null) settingsButton = FindButtonByName("Settings");

        if (closeSettingsButton == null) closeSettingsButton = FindButtonByName("CloseSettingsButton");
        if (closeSettingsButton == null) closeSettingsButton = FindButtonByName("CloseButton");
        if (closeSettingsButton == null) closeSettingsButton = FindButtonByName("BackButton");

        if (restartButton == null) restartButton = FindButtonByName("RestartButton");
        if (restartButton == null) restartButton = FindButtonByName("Restart");

        if (mainMenuButton == null) mainMenuButton = FindButtonByName("MainMenuButton");
        if (mainMenuButton == null) mainMenuButton = FindButtonByName("MenuButton");

        if (quitButton == null) quitButton = FindButtonByName("QuitButton");
        if (quitButton == null) quitButton = FindButtonByName("Quit");

        // Динамическая привязка методов к событиям нажатия (onClick)
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
        if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        // Загружаем настройки звука
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            SetMusicVolume(musicSlider.value); // Применяем сразу
        }
        
        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }
        
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        // Инициализируем дропдаун музыки
        if (musicDropDown != null)
        {
            int savedTrack = PlayerPrefs.GetInt("SelectedBGM", 0);
            musicDropDown.value = savedTrack;
            musicDropDown.onValueChanged.AddListener(PlayBGMTrack);
            
            // Если музыка на сцене уже играет, запускаем сохраненный трек
            PlayBGMTrack(savedTrack);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
                return;

            if (isPaused)
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    CloseSettings();
                }
                else
                {
                    Resume();
                }
            }
            else
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;
        
        if (pausePanel != null) pausePanel.SetActive(true);
        if (pauseButtonsPanel != null) pauseButtonsPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void Resume()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;
        
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // --- НАВИГАЦИЯ НАСТРОЕК ---
    public void OpenSettings()
    {
        if (pauseButtonsPanel != null) pauseButtonsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseButtonsPanel != null) pauseButtonsPanel.SetActive(true);
    }

    // --- ЛОГИКА НАСТРОЕК ---
    public void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = value;
        }
    }

    public void SetSFXVolume(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void PlayBGMTrack(int trackIndex)
    {
        if (bgmAudioSource == null || bgmTracks == null || trackIndex >= bgmTracks.Length) return;

        if (bgmTracks[trackIndex] != null)
        {
            bgmAudioSource.Stop();
            bgmAudioSource.clip = bgmTracks[trackIndex];
            bgmAudioSource.loop = true;
            
            // Устанавливаем текущую громкость из настроек
            bgmAudioSource.volume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            
            bgmAudioSource.Play();
            PlayerPrefs.SetInt("SelectedBGM", trackIndex);
        }
    }

    // --- ГЛОБАЛЬНЫЕ ФУНКЦИИ ---
    public void RestartLevel()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
            return;
        }
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}


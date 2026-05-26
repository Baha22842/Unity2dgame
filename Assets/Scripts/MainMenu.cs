using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Сцена геймплея")]
    [SerializeField] private string gameplaySceneName = "SampleScene";

    [Header("Панели меню (Canvas Groups)")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup saveSlotsPanel;
    [SerializeField] private CanvasGroup settingsPanel;

    [Header("Настройки переходов")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Элементы Слотов Сохранений")]
    [SerializeField] private TextMeshProUGUI[] slotCoinsTexts;     // Тексты монет для слотов
    [SerializeField] private TextMeshProUGUI[] slotTimeTexts;      // Тексты времени для слотов
    [SerializeField] private GameObject[] deleteButtons;// Кнопки удаления для слотов
    [SerializeField] private GameObject[] emptyOnlyObjects;    // Декор, видимый только в ПУСТОМ слоте
    [SerializeField] private GameObject[] occupiedOnlyObjects; // Декор, видимый только в ЗАНЯТОМ слоте

    [Header("Элементы Настроек")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Sprite greenCheckmarkSprite; // Спрайт галочки (полный экран)
    [SerializeField] private Sprite redCrossSprite;       // Спрайт крестика (в окне)

    [Header("Элементы Дополнительно (Аудиоплеер)")]
    [SerializeField] private AudioClip[] bgmTracks;    // Фоновые треки для меню

    private AudioSource menuAudioSource;
    private CanvasGroup currentPanel;
    private bool isTransitioning = false;

    private void Start()
    {
        // Устанавливаем начальное состояние панелей
        InitializePanel(mainMenuPanel, true);
        InitializePanel(saveSlotsPanel, false);
        InitializePanel(settingsPanel, false);

        currentPanel = mainMenuPanel;

        // Инициализируем слоты сохранений
        UpdateSaveSlotsUI();

        // Автоматически находим AudioSource на этом же объекте, если он не привязан вручную
        if (menuAudioSource == null)
        {
            menuAudioSource = GetComponent<AudioSource>();
        }

        // Загружаем настройки звука
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
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
            UpdateFullscreenToggleUI(Screen.fullScreen);
        }

        // Запускаем фоновую музыку
        if (menuAudioSource != null && bgmTracks != null && bgmTracks.Length > 0)
        {
            int savedTrack = PlayerPrefs.GetInt("SelectedBGM", 0);

            if (savedTrack < bgmTracks.Length && bgmTracks[savedTrack] != null)
            {
                menuAudioSource.clip = bgmTracks[savedTrack];
                menuAudioSource.loop = true;
                
                // Устанавливаем громкость из сохраненных настроек
                float savedVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
                menuAudioSource.volume = savedVolume;
                
                menuAudioSource.Play();
            }
        }
    }

    private void InitializePanel(CanvasGroup panel, bool active)
    {
        if (panel == null) return;
        panel.alpha = active ? 1f : 0f;
        panel.blocksRaycasts = active;
        panel.interactable = active;
        panel.gameObject.SetActive(active);
    }

    // --- ЛОГИКА ПЕРЕХОДОВ ---
    public void GoToMainMenu() => TransitionTo(mainMenuPanel);
    public void GoToSaveSlots() => TransitionTo(saveSlotsPanel);
    public void GoToSettings() => TransitionTo(settingsPanel);

    public void TransitionTo(CanvasGroup targetPanel)
    {
        if (isTransitioning || targetPanel == null || currentPanel == targetPanel) return;
        StartCoroutine(TransitionRoutine(currentPanel, targetPanel));
    }

    private IEnumerator TransitionRoutine(CanvasGroup fromPanel, CanvasGroup toPanel)
    {
        isTransitioning = true;

        // Блокируем взаимодействие во время перехода
        fromPanel.interactable = false;
        fromPanel.blocksRaycasts = false;

        // Плавное исчезновение (Fade Out)
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fromPanel.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        fromPanel.alpha = 0f;
        fromPanel.gameObject.SetActive(false);

        // Включаем новую панель с нулевой прозрачностью
        toPanel.gameObject.SetActive(true);
        toPanel.alpha = 0f;
        toPanel.interactable = false;
        toPanel.blocksRaycasts = false;

        // Плавное проявление (Fade In)
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            toPanel.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        toPanel.alpha = 1f;
        toPanel.interactable = true;
        toPanel.blocksRaycasts = true;

        currentPanel = toPanel;
        isTransitioning = false;
    }

    public void UpdateSaveSlotsUI()
    {
        int slotsCount = slotCoinsTexts != null ? slotCoinsTexts.Length : 0;
        for (int i = 0; i < slotsCount; i++)
        {
            int slotIndex = i + 1;
            bool exists = SaveSystem.SaveExists(slotIndex);

            if (exists)
            {
                SaveData data = SaveSystem.LoadGame(slotIndex);
                if (data != null)
                {
                    if (slotCoinsTexts != null && i < slotCoinsTexts.Length && slotCoinsTexts[i] != null)
                        slotCoinsTexts[i].text = data.score.ToString() + " монет";
                    
                    if (slotTimeTexts != null && i < slotTimeTexts.Length && slotTimeTexts[i] != null)
                        slotTimeTexts[i].text = FormatTime(data.totalPlayTime);
                }

                if (deleteButtons != null && i < deleteButtons.Length && deleteButtons[i] != null)
                    deleteButtons[i].SetActive(true);

                // Включаем декор для занятого слота, выключаем для пустого
                if (occupiedOnlyObjects != null && i < occupiedOnlyObjects.Length && occupiedOnlyObjects[i] != null)
                    occupiedOnlyObjects[i].SetActive(true);
                if (emptyOnlyObjects != null && i < emptyOnlyObjects.Length && emptyOnlyObjects[i] != null)
                    emptyOnlyObjects[i].SetActive(false);
            }
            else
            {
                if (slotCoinsTexts != null && i < slotCoinsTexts.Length && slotCoinsTexts[i] != null)
                    slotCoinsTexts[i].text = "Новая игра";
                
                if (slotTimeTexts != null && i < slotTimeTexts.Length && slotTimeTexts[i] != null)
                    slotTimeTexts[i].text = ""; // Очищаем время, чтобы не мешалось

                if (deleteButtons != null && i < deleteButtons.Length && deleteButtons[i] != null)
                    deleteButtons[i].SetActive(false);

                // Выключаем декор для занятого слота, включаем для пустого
                if (occupiedOnlyObjects != null && i < occupiedOnlyObjects.Length && occupiedOnlyObjects[i] != null)
                    occupiedOnlyObjects[i].SetActive(false);
                if (emptyOnlyObjects != null && i < emptyOnlyObjects.Length && emptyOnlyObjects[i] != null)
                    emptyOnlyObjects[i].SetActive(true);
            }
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (isTransitioning) return;

        SaveSystem.SelectedSlot = slotIndex;

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void DeleteSlot(int slotIndex)
    {
        SaveSystem.DeleteSave(slotIndex);
        UpdateSaveSlotsUI();
    }

    private string FormatTime(float seconds)
    {
        int h = Mathf.FloorToInt(seconds / 3600f);
        int m = Mathf.FloorToInt((seconds % 3600f) / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:00}:{1:00}:{2:00}", h, m, s);
    }

    // --- ЛОГИКА НАСТРОЕК ---
    public void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        if (menuAudioSource != null)
        {
            menuAudioSource.volume = value;
        }
    }

    public void SetSFXVolume(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        UpdateFullscreenToggleUI(isFullscreen);
    }

    private void UpdateFullscreenToggleUI(bool isFullscreen)
    {
        if (fullscreenToggle != null)
        {
            // Отключаем стандартный дочерний Checkmark (чтобы он не перекрывал)
            if (fullscreenToggle.graphic != null)
            {
                fullscreenToggle.graphic.gameObject.SetActive(false);
            }

            // Меняем спрайт на фоновом Image самого переключателя
            if (fullscreenToggle.targetGraphic != null)
            {
                Image bgImage = fullscreenToggle.targetGraphic.GetComponent<Image>();
                if (bgImage != null)
                {
                    if (isFullscreen && greenCheckmarkSprite != null)
                    {
                        bgImage.sprite = greenCheckmarkSprite;
                    }
                    else if (!isFullscreen && redCrossSprite != null)
                    {
                        bgImage.sprite = redCrossSprite;
                    }
                }
            }
        }
    }

    // --- ЛОГИКА ДОПОЛНИТЕЛЬНО (ПЛЕЕР) ---
    public void PlayBGMTrack(int trackIndex)
    {
        if (menuAudioSource == null || bgmTracks == null || trackIndex >= bgmTracks.Length) return;

        if (bgmTracks[trackIndex] != null)
        {
            menuAudioSource.Stop();
            menuAudioSource.clip = bgmTracks[trackIndex];
            menuAudioSource.Play();
            PlayerPrefs.SetInt("SelectedBGM", trackIndex);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

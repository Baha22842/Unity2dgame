using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Reflection;

public class CelesteTransition : MonoBehaviour
{
    public static CelesteTransition Instance { get; private set; }

    private CanvasGroup _canvasGroup;
    private bool _isTransitioning = false;
    
    private float _savedGravity = 1f;
    private bool _hasSavedPlayer = false;
    private Rigidbody2D _playerRb;
    private MonoBehaviour _playerMovement;
    private MonoBehaviour _playerCombat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateFadeCanvas();
    }

    private void CreateFadeCanvas()
    {
        // 1. Создаем Canvas поверх всех окон
        GameObject canvasGo = new GameObject("CelesteFadeCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGo.AddComponent<GraphicRaycaster>();
        
        // 2. Создаем черную панель на весь экран
        GameObject panelGo = new GameObject("FadePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        
        RectTransform rect = panelGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        
        Image img = panelGo.AddComponent<Image>();
        img.color = Color.black;
        
        // 3. Добавляем CanvasGroup для плавного фейда
        _canvasGroup = panelGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        
        DontDestroyOnLoad(canvasGo);
    }

    /// <summary>
    /// Совершает плавный переход в стиле Celeste: затемняет экран, замораживает игрока,
    /// телепортирует его в новую комнату, сбрасывает камеры и осветляет экран обратно.
    /// </summary>
    public void Transition(Vector3 targetPosition, float fadeDuration = 0.4f, float holdDuration = 0.2f, System.Action onComplete = null)
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionRoutine(targetPosition, fadeDuration, holdDuration, onComplete));
    }

    private IEnumerator TransitionRoutine(Vector3 targetPosition, float fadeDuration, float holdDuration, System.Action onComplete)
    {
        _isTransitioning = true;
        _canvasGroup.blocksRaycasts = true;

        // 1. Замораживаем игрока в пространстве и отключаем управление
        FreezePlayer(true);

        // 2. Плавный фейд экрана до черного
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

        // Рассчитываем вектор перемещения для сброса камер Cinemachine
        Vector3 oldPos = _playerMovement != null ? _playerMovement.transform.position : targetPosition;
        Vector3 delta = targetPosition - oldPos;

        // 3. Телепортируем игрока в новую комнату
        TeleportPlayer(targetPosition);

        // Мгновенно сообщаем всем Cinemachine камерам о телепортации, чтобы избежать резкого панорамирования
        WarpCinemachineCameras(delta);

        // 4. Задержка в темноте
        yield return new WaitForSeconds(holdDuration);

        // 5. Плавный фейд экрана обратно в прозрачный
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        // 6. Размораживаем управление игрока
        FreezePlayer(false);

        _canvasGroup.blocksRaycasts = false;
        _isTransitioning = false;

        onComplete?.Invoke();
    }

    /// <summary>
    /// Переход на сцену с фейдом по индексу
    /// </summary>
    public void TransitionToScene(int sceneIndex, float fadeDuration = 0.5f, float holdDuration = 0.3f)
    {
        if (_isTransitioning) return;
        StartCoroutine(SceneTransitionRoutine(sceneIndex, fadeDuration, holdDuration));
    }

    private IEnumerator SceneTransitionRoutine(int sceneIndex, float fadeDuration, float holdDuration)
    {
        _isTransitioning = true;
        _canvasGroup.blocksRaycasts = true;

        // Замораживаем игрока перед сменой сцены
        FreezePlayer(true);

        // Фейд к черному
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

        yield return new WaitForSecondsRealtime(holdDuration);

        // Загружаем новую сцену асинхронно
        var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneIndex);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Ждем один кадр для полной инициализации сцены
        yield return null;

        // Сбрасываем кэш игрока, так как в новой сцене создастся новый
        _hasSavedPlayer = false;

        // Фейд обратно в прозрачный
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        _canvasGroup.blocksRaycasts = false;
        _isTransitioning = false;
    }

    /// <summary>
    /// Переход на сцену с фейдом по имени
    /// </summary>
    public void TransitionToScene(string sceneName, float fadeDuration = 0.5f, float holdDuration = 0.3f)
    {
        if (_isTransitioning) return;
        StartCoroutine(SceneTransitionRoutine(sceneName, fadeDuration, holdDuration));
    }

    private IEnumerator SceneTransitionRoutine(string sceneName, float fadeDuration, float holdDuration)
    {
        _isTransitioning = true;
        _canvasGroup.blocksRaycasts = true;

        // Замораживаем игрока перед сменой сцены
        FreezePlayer(true);

        // Фейд к черному
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

        yield return new WaitForSecondsRealtime(holdDuration);

        // Загружаем новую сцену асинхронно
        var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Ждем один кадр для полной инициализации сцены
        yield return null;

        // Сбрасываем кэш игрока, так как в новой сцене создастся новый
        _hasSavedPlayer = false;

        // Фейд обратно в прозрачный
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

        _canvasGroup.blocksRaycasts = false;
        _isTransitioning = false;
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = endAlpha;
    }

    private void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            // Динамически ищем скрипт движения игрока по имени класса, защищая сборку от ошибок
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                // Резервный поиск по компоненту
                MonoBehaviour pm = FindFirstObjectByType<PlayerMovement>() as MonoBehaviour;
                if (pm != null) playerObj = pm.gameObject;
            }

            if (playerObj != null)
            {
                _playerMovement = playerObj.GetComponent<PlayerMovement>();
                _playerCombat = playerObj.GetComponent("PlayerCombat") as MonoBehaviour;
                _playerRb = playerObj.GetComponent<Rigidbody2D>();
                
                if (_playerRb != null)
                {
                    _savedGravity = _playerRb.gravityScale;
                    _playerRb.gravityScale = 0f;
                    _playerRb.linearVelocity = Vector2.zero;
                }

                if (_playerMovement != null) _playerMovement.enabled = false;
                if (_playerCombat != null) _playerCombat.enabled = false;
                
                _hasSavedPlayer = true;
            }
        }
        else
        {
            if (_hasSavedPlayer && _playerMovement != null)
            {
                if (_playerRb != null)
                {
                    _playerRb.gravityScale = _savedGravity;
                    _playerRb.linearVelocity = Vector2.zero;
                }
                
                if (_playerMovement != null) _playerMovement.enabled = true;
                if (_playerCombat != null) _playerCombat.enabled = true;
            }
            _hasSavedPlayer = false;
        }
    }

    private void TeleportPlayer(Vector3 position)
    {
        if (_hasSavedPlayer && _playerMovement != null)
        {
            _playerMovement.transform.position = position;
            
            // Также сбрасываем классические скрипты следования камеры, если они есть
            CameraFollow follow = FindFirstObjectByType<CameraFollow>();
            if (follow != null)
            {
                follow.transform.position = new Vector3(position.x, position.y, follow.transform.position.z);
            }
        }
    }

    /// <summary>
    /// Динамический варп Cinemachine через рефлексию для 100% совместимости с Cinemachine v2 и v3 без ошибок компиляции!
    /// </summary>
    private void WarpCinemachineCameras(Vector3 delta)
    {
        if (!_hasSavedPlayer || _playerMovement == null) return;
        Transform playerT = _playerMovement.transform;

        string[] vcamTypes = new string[]
        {
            "Unity.Cinemachine.CinemachineCamera",         // Cinemachine v3
            "Cinemachine.CinemachineVirtualCamera",        // Cinemachine v2
            "Unity.Cinemachine.CinemachineVirtualCamera"
        };

        foreach (var typeName in vcamTypes)
        {
            System.Type t = CinemachineReflectionHelper.FindType(typeName);
            if (t != null)
            {
                #pragma warning disable CS0618
                var vcams = FindObjectsByType(t, FindObjectsSortMode.None);
                #pragma warning restore CS0618
                foreach (var vcam in vcams)
                {
                    if (vcam != null)
                    {
                        var warpMethod = t.GetMethod("OnTargetObjectWarped");
                        if (warpMethod != null)
                        {
                            warpMethod.Invoke(vcam, new object[] { playerT, delta });
                        }
                    }
                }
            }
        }
    }
}

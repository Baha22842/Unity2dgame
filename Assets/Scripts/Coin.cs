using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Настройки Сферы Духа")]
    public int value = 10;
    
    [Tooltip("Радиус обнаружения игрока для магнитного притяжения")]
    public float magnetRadius = 5f;
    
    [Tooltip("Скорость полета к игроку")]
    public float flySpeed = 12f;

    [Tooltip("Сила импульса разброса при спавне")]
    public float popForce = 6f;

    private Rigidbody2D _rb;
    private Transform _playerTransform;
    private bool _isAttracted = false;
    private float _collectCooldown = 0.3f; // Задержка сбора, чтобы сферы успели разлететься фонтаном
    private float _spawnTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spawnTime = Time.time;
    }

    private void Start()
    {
        // Пытаемся найти игрока на сцене
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
    }

    private void Update()
    {
        if (_playerTransform == null)
        {
            // На случай, если игрок переродился
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            return;
        }

        // Если не притянуто магнитом, проверяем расстояние
        if (!_isAttracted)
        {
            float distance = Vector2.Distance(transform.position, _playerTransform.position);
            if (distance <= magnetRadius && (Time.time - _spawnTime) >= _collectCooldown)
            {
                _isAttracted = true;
                if (_rb != null)
                {
                    _rb.gravityScale = 0f; // Отключаем гравитацию для плавного полета
                    _rb.linearVelocity = Vector2.zero;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        // Плавный полет к игроку под действием магнита
        if (_isAttracted && _playerTransform != null)
        {
            Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            // Увеличиваем скорость по мере приближения для динамичности
            float currentSpeed = Mathf.Lerp(flySpeed * 0.5f, flySpeed * 1.5f, 1f - (Vector2.Distance(transform.position, _playerTransform.position) / magnetRadius));
            
            if (_rb != null)
            {
                _rb.linearVelocity = direction * currentSpeed;
            }
            else
            {
                transform.Translate(direction * currentSpeed * Time.fixedDeltaTime, Space.World);
            }
        }
    }

    /// <summary>
    /// Прикладывает случайную силу разброса, имитируя фонтан опыта в Minecraft
    /// </summary>
    public void ApplyPopForce()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
        {
            // Возвращаем физический режим
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 1.5f; // Сила падения

            // Случайный вектор силы: вверх под углом влево или вправо
            float randomX = Random.Range(-3f, 3f);
            float randomY = Random.Range(4f, 8f);
            Vector2 force = new Vector2(randomX, randomY).normalized * popForce;

            _rb.AddForce(force, ForceMode2D.Impulse);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Сферу нельзя подобрать мгновенно при спавне, давая проиграться фонтану
        if ((Time.time - _spawnTime) < _collectCooldown)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(value);
            
            // Лог в консоль в духе лора игры
            Debug.Log($"[ДУХИ] Целительный дух спасен! +{value} духов.");
        }

        // Спавним небольшой звуковой/визуальный эффект сбора, если нужно, и удаляем
        Destroy(gameObject);
    }
}



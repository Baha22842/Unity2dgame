using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
public class Mushroom : MonoBehaviour, IHittable
{
    public enum MushroomState
    {
        Idle,
        Walk,
        Hide,
        Peek,
        Pop
    }

    [Header("Bounce Settings")]
    [Tooltip("Сила отскока игрока при прыжке на гриб сверху (Trampoline effect)")]
    [SerializeField] private float _bounceForce = 11f;

    [Header("Patrol Settings")]
    [SerializeField] private float _speed = 1.5f;
    [SerializeField] private float _idleTimeAtEdge = 1.0f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _checkDistance = 0.5f;
    [Tooltip("Максимальный радиус патрулирования от стартовой позиции (0 = бесконечно / ходить от края до края)")]
    [SerializeField] private float _patrolRange = 0f;

    [Header("Original Collider Dimensions (Tall state)")]
    [Tooltip("Исходный полный размер коллайдера по высоте и ширине. Это защищает от багов сохранения префаба в сжатом состоянии!")]
    [SerializeField] private Vector2 _originalColliderSize = new Vector2(0.984f, 0.951f);
    [Tooltip("Исходный оффсет коллайдера по осям X и Y.")]
    [SerializeField] private Vector2 _originalColliderOffset = new Vector2(0f, 0.0027f);

    [Header("Height Scaling Settings")]
    [Tooltip("Во сколько раз уменьшается высота коллайдера при прятании (0.5f = уменьшение в 2 раза)")]
    [SerializeField] private float _shrunkHeightMultiplier = 0.5f;
    [Tooltip("Множитель высоты для состояния Peek и Pop (когда гриб выглядывает / готовится вырасти)")]
    [SerializeField] private float _peekHeightMultiplier = 0.55f;

    [Header("State Durations")]
    [SerializeField] private float _hideDuration = 2.0f;
    [SerializeField] private float _peekDuration = 1.5f;
    [SerializeField] private float _popDuration = 0.5f;

    // Константы имен анимаций, соответствующих файлам в Assets/Animations
    private const string ANIM_IDLE = "ShoomIdle";
    private const string ANIM_WALK = "ShoomWalk";
    private const string ANIM_HIT  = "ShoomHit";
    private const string ANIM_HIDE = "ShoomHide";
    private const string ANIM_PEEK = "ShoomPeek";
    private const string ANIM_POP  = "ShoomPop";

    private MushroomState _currentState;
    private float _stateTimer;
    private int _facingDirection = 1; // 1 = Вправо, -1 = Влево
    private Vector3 _startPosition;
    private bool _wasHitLast; // Флаг, определяющий, было ли последнее действие ударом или прыжком сверху

    private Rigidbody2D _rb;
    private Animator _animator;
    private Collider2D _collider;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<Collider2D>();

        if (_animator != null)
        {
            _animator.applyRootMotion = false;
        }

        if (_rb != null)
        {
            // Устанавливаем Kinematic, чтобы игрок не сдвигал тело гриба своим телом
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Блокируем вращение Z
        }

        // Предохранитель: если в инспекторе захардкожены старые сжатые значения, сбрасываем их к верным исходным размерам prefab
        if (_originalColliderSize.y < 0.7f)
        {
            _originalColliderSize = new Vector2(0.984f, 0.951f);
            _originalColliderOffset = new Vector2(0f, 0.0027f);
        }

        // Кэшируем реальные размеры коллайдера на старте, если они полные
        if (_collider is BoxCollider2D box)
        {
            if (box.size.y > 0.7f)
            {
                _originalColliderSize = box.size;
                _originalColliderOffset = box.offset;
            }
        }
        else if (_collider is CapsuleCollider2D capsule)
        {
            if (capsule.size.y > 0.7f)
            {
                _originalColliderSize = capsule.size;
                _originalColliderOffset = capsule.offset;
            }
        }

        // Задаем изначальное направление взгляда по scale.x
        _facingDirection = (transform.localScale.x >= 0f) ? 1 : -1;
    }

    private void Start()
    {
        _startPosition = transform.position;

        // Запуск патрулирования со старта
        ChangeState(MushroomState.Walk);
    }

    private void Update()
    {
        UpdateStateLogic();
    }

    private void FixedUpdate()
    {
        if (_rb != null && _rb.bodyType == RigidbodyType2D.Kinematic)
        {
            // 1. Проверяем наличие земли под центром гриба с помощью луча
            // Запускаем луч чуть выше пивота (на 0.2f), чтобы исключить старт луча внутри коллайдера земли
            Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y + 0.2f);
            float rayLength = _originalColliderSize.y + 0.5f + 0.2f;
            RaycastHit2D groundHit = default;
            bool isGrounded = false;
            
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.down, rayLength, _groundLayer);
            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
                {
                    groundHit = hit;
                    isGrounded = true;
                    break;
                }
            }

            float velocityY = 0f;
            float velocityX = 0f;

            if (isGrounded)
            {
                // Всегда прижимаем по полной высоте, чтобы база гриба никогда не проваливалась под землю!
                float distToBottom = (_originalColliderSize.y / 2f) - _originalColliderOffset.y;
                float targetY = groundHit.point.y + distToBottom;
                
                // Мгновенно прижимаем к земле, чтобы избежать провисания и левитации
                _rb.position = new Vector2(_rb.position.x, targetY);
                velocityY = 0f;
            }
            else
            {
                // Падение под силой гравитации
                velocityY = -5f;
            }

            // Движение по горизонтали только во время патрулирования Walk
            if (_currentState == MushroomState.Walk)
            {
                velocityX = _facingDirection * _speed;
            }

            // Перемещаем Kinematic Rigidbody
            _rb.position += new Vector2(velocityX, velocityY) * Time.fixedDeltaTime;
        }
    }

    private void UpdateStateLogic()
    {
        if (_currentState == MushroomState.Walk)
        {
            UpdatePatrol();
        }
        else
        {
            // Отсчет таймера для временных состояний
            _stateTimer -= Time.deltaTime;
            
            if (_stateTimer <= 0f)
            {
                TransitionToNextState();
            }
        }
    }

    private void UpdatePatrol()
    {
        bool hasWall = CheckWallAhead();
        bool hasEdge = CheckGroundAhead();

        // Ограничение дальности патрулирования от точки старта
        bool outOfRange = false;
        if (_patrolRange > 0f)
        {
            float distFromStart = transform.position.x - _startPosition.x;
            if (distFromStart > _patrolRange && _facingDirection > 0)
            {
                outOfRange = true;
            }
            else if (distFromStart < -_patrolRange && _facingDirection < 0)
            {
                outOfRange = true;
            }
        }

        if (hasWall || !hasEdge || outOfRange)
        {
            // В отличие от багнутой корутины, мгновенно разворачиваем и уходим в Idle на _idleTimeAtEdge секунд
            Flip();
            ChangeState(MushroomState.Idle);
        }
    }

    private bool CheckGroundAhead()
    {
        float distToBottom = (_originalColliderSize.y / 2f) - _originalColliderOffset.y;
        float bottomY = transform.position.y - distToBottom;
        
        // Проверяем на 0.1 единицы впереди границы коллайдера гриба
        float checkX = transform.position.x + _facingDirection * (_originalColliderSize.x / 2f + 0.1f);
        Vector2 checkStart = new Vector2(checkX, bottomY + 0.05f);
        
        RaycastHit2D[] hits = Physics2D.RaycastAll(checkStart, Vector2.down, _checkDistance, _groundLayer);
        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
            {
                return true; // Земля есть!
            }
        }
        return false; // Край платформы!
    }

    private bool CheckWallAhead()
    {
        // Автоматический расчет точки проверки стены впереди коллайдера
        float checkX = transform.position.x + _facingDirection * (_originalColliderSize.x / 2f + 0.1f);
        Vector2 checkStart = new Vector2(checkX, transform.position.y);
        
        RaycastHit2D[] hits = Physics2D.RaycastAll(checkStart, Vector2.right * _facingDirection, _checkDistance, _groundLayer);
        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
            {
                return true; // Стена обнаружена!
            }
        }
        return false;
    }

    private void TransitionToNextState()
    {
        switch (_currentState)
        {
            case MushroomState.Idle:
                ChangeState(MushroomState.Walk);
                break;

            case MushroomState.Hide:
                // Отсидевшись в шляпке, гриб осторожно выглядывает
                ChangeState(MushroomState.Peek);
                break;

            case MushroomState.Peek:
                // Если все спокойно, гриб вылезает обратно (Pop)
                ChangeState(MushroomState.Pop);
                break;

            case MushroomState.Pop:
                // Гриб полностью вылез и возвращается к ходьбе
                ChangeState(MushroomState.Walk);
                break;
        }
    }

    private void ChangeState(MushroomState newState)
    {
        _currentState = newState;
        string animName = ANIM_IDLE;

        switch (newState)
        {
            case MushroomState.Idle:
                SetMushroomHeight(1.0f);
                animName = ANIM_IDLE;
                _stateTimer = _idleTimeAtEdge;
                break;

            case MushroomState.Walk:
                SetMushroomHeight(1.0f);
                animName = ANIM_WALK;
                break;

            case MushroomState.Hide:
                SetMushroomHeight(_shrunkHeightMultiplier);
                animName = _wasHitLast ? ANIM_HIT : ANIM_HIDE;
                _stateTimer = _hideDuration;
                break;

            case MushroomState.Peek:
                SetMushroomHeight(_peekHeightMultiplier);
                animName = ANIM_PEEK;
                _stateTimer = _peekDuration;
                break;

            case MushroomState.Pop:
                // Удерживаем уменьшенный размер коллайдера, пока гриб не вырастет полностью в анимации
                SetMushroomHeight(_peekHeightMultiplier);
                animName = ANIM_POP;
                _stateTimer = _popDuration;
                break;
        }

        if (_animator != null)
        {
            _animator.Play(animName);
        }
    }

    private void SetMushroomHeight(float multiplier)
    {
        if (_collider == null) return;

        float newHeight = _originalColliderSize.y * multiplier;
        float distToBottom = (_originalColliderSize.y / 2f) - _originalColliderOffset.y;
        
        // Математически сдвигаем оффсет так, чтобы НИЗ коллайдера оставался неподвижным на земле (на уровне подошвы гриба),
        // а сжатие происходило только сверху вниз!
        float newOffsetY = -distToBottom + (newHeight / 2f);
        Vector2 newOffset = new Vector2(_originalColliderOffset.x, newOffsetY);

        // Применяем параметры коллайдера математически точно
        if (_collider is BoxCollider2D box)
        {
            box.size = new Vector2(_originalColliderSize.x, newHeight);
            box.offset = newOffset;
        }
        else if (_collider is CapsuleCollider2D capsule)
        {
            capsule.size = new Vector2(_originalColliderSize.x, newHeight);
            capsule.offset = newOffset;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ProcessPlayerCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ProcessPlayerCollision(collision);
    }

    private void ProcessPlayerCollision(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // Если игрок приземляется точно сверху на гриб (Trampoline Jump)
                if (contact.normal.y < -0.5f)
                {
                    Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
                    if (playerRb != null)
                    {
                        // Подкидываем игрока вертикально вверх с заданной силой
                        playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, _bounceForce);

                        // ТРЕБОВАНИЕ: Первый прыжок проигрывает ShoomHide единожды.
                        // Если игрок прыгает снова, пока гриб спрятан или оглядывается (Hide/Peek/Pop),
                        // то проигрывается анимация боли ANIM_HIT ("ShoomHit") и сбрасывается таймер!
                        if (_currentState == MushroomState.Hide || _currentState == MushroomState.Peek || _currentState == MushroomState.Pop)
                        {
                            _wasHitLast = true; // Используем анимацию ANIM_HIT
                        }
                        else
                        {
                            _wasHitLast = false; // Первый прыжок использует ANIM_HIDE
                        }

                        ChangeState(MushroomState.Hide);
                    }
                    break;
                }
            }
        }
    }

    // Реализация интерфейса IHittable — вызывается мечом игрока
    public void OnHit(bool isHeavyAttack = false)
    {
        // Удар мечом всегда сбрасывает таймер прятания, заставляет гриб спрятаться и включает анимацию HIT!
        _wasHitLast = true;
        _stateTimer = _hideDuration;
        ChangeState(MushroomState.Hide);
    }

    private void Flip()
    {
        _facingDirection *= -1;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * _facingDirection;
        transform.localScale = scale;
    }

    private void OnDrawGizmosSelected()
    {
        // Отрисовка зон проверок в Редакторе Unity
        Gizmos.color = Color.yellow;
        float distToBottom = (_originalColliderSize.y / 2f) - _originalColliderOffset.y;
        float bottomY = transform.position.y - distToBottom;
        Vector2 checkStart = new Vector2(transform.position.x + _facingDirection * (_originalColliderSize.x / 2f + 0.1f), bottomY + 0.05f);
        Gizmos.DrawLine(checkStart, checkStart + Vector2.down * _checkDistance);

        Gizmos.color = Color.cyan;
        Vector2 checkStartWall = new Vector2(transform.position.x + _facingDirection * (_originalColliderSize.x / 2f + 0.1f), transform.position.y);
        Gizmos.DrawLine(checkStartWall, checkStartWall + Vector2.right * _facingDirection * _checkDistance);

        if (_patrolRange > 0f)
        {
            Gizmos.color = Color.red;
            Vector3 center = Application.isPlaying ? _startPosition : transform.position;
            Gizmos.DrawLine(center + Vector3.left * _patrolRange, center + Vector3.right * _patrolRange);
            Gizmos.DrawLine(center + Vector3.left * _patrolRange + Vector3.up * 0.2f, center + Vector3.left * _patrolRange + Vector3.down * 0.2f);
            Gizmos.DrawLine(center + Vector3.right * _patrolRange + Vector3.up * 0.2f, center + Vector3.right * _patrolRange + Vector3.down * 0.2f);
        }
    }
}
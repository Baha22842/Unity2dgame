using UnityEngine;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Header("Настройки Карты")]
    public GameObject mapUI; // Само окно Карты, которое открывается по Tab
    public Transform gridContainer; // Объект с GridLayoutGroup
    public GameObject cellPrefab; // Префаб квадратика (Image)
    
    [Header("Графика (Momodora style)")]
    public Color hiddenColor = new Color(0, 0, 0, 0); // Туман войны (прозрачный)
    public Color exploredColor = new Color(0.8f, 0.2f, 0.2f, 1f); // Красный (исследовано)
    public Color currentColor = new Color(1f, 1f, 1f, 1f); // Белый (мы сейчас здесь)

    private Image[,] mapCells = new Image[20, 20];
    private bool isMapOpen = false;
    private Vector2Int currentRoom = new Vector2Int(-1, -1);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (mapUI != null) mapUI.SetActive(false);

        GenerateMapGrid();
    }

    private void GenerateMapGrid()
    {
        if (gridContainer == null || cellPrefab == null) return;

        // Очищаем контейнер, если там что-то было (на случай перезагрузки)
        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        // Создаем 20x20 = 400 квадратиков! 
        // Скрипт сам заполнит UI-сетку, чтобы тебе не пришлось руками копировать 400 объектов.
        // Координата (0,0) будет в левом верхнем углу интерфейса.
        for (int y = 0; y < 20; y++) 
        {
            for (int x = 0; x < 20; x++)
            {
                GameObject newCell = Instantiate(cellPrefab, gridContainer);
                newCell.name = $"MapCell_{x}_{y}";
                
                Image img = newCell.GetComponent<Image>();
                img.color = hiddenColor;
                
                mapCells[x, y] = img;
            }
        }
    }

    private void Start()
    {
        RefreshMapVisuals();
    }

    private void Update()
    {
        // Открытие/закрытие карты по Tab (если мы не мертвы)
        if (Input.GetKeyDown(KeyCode.Tab) && (GameManager.Instance == null || !GameManager.Instance.IsGameOver))
        {
            isMapOpen = !isMapOpen;
            mapUI.SetActive(isMapOpen);
            
            if (isMapOpen)
            {
                RefreshMapVisuals();
                Time.timeScale = 0f; // Ставим игру на паузу (как в Hollow Knight)
            }
            else
            {
                Time.timeScale = 1f; // Снимаем с паузы
            }
        }
    }

    // Эту функцию будут вызывать триггеры комнат, когда игрок в них заходит
    public void SetCurrentRoom(int x, int y)
    {
        currentRoom = new Vector2Int(x, y);
        
        string roomKey = $"{x}_{y}";
        
        // Если этой комнаты еще нет в списке открытых - добавляем и сохраняем!
        if (GameManager.Instance != null && !GameManager.Instance.exploredRooms.Contains(roomKey))
        {
            GameManager.Instance.exploredRooms.Add(roomKey);
            GameManager.Instance.SaveGameData();
        }

        if (isMapOpen) RefreshMapVisuals();
    }

    private void RefreshMapVisuals()
    {
        if (GameManager.Instance == null) return;

        // Проходимся по всем 400 квадратам и красим их
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                string roomKey = $"{x}_{y}";
                
                if (currentRoom.x == x && currentRoom.y == y)
                {
                    mapCells[x, y].color = currentColor; // Мы здесь (Белый)
                }
                else if (GameManager.Instance.exploredRooms.Contains(roomKey))
                {
                    mapCells[x, y].color = exploredColor; // Исследовано (Красный)
                }
                else
                {
                    mapCells[x, y].color = hiddenColor; // Туман войны
                }
            }
        }
    }
}

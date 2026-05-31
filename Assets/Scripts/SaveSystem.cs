using UnityEngine;
using System.IO;

[System.Serializable]
public class SaveData
{
    public int score;
    public int lives;
    public int currentLevelIndex;
    public int maxHealth;
    
    // Навыки (Метроидвания)
    public bool hasDoubleJump;
    public bool hasDash;
    public bool hasHeavyAttack;
    public bool hasThrust;

    // Сюжетные предметы
    public int collectedArtifacts;

    // Карта (Метроидвания)
    public System.Collections.Generic.List<string> exploredRooms;

    // Время в игре
    public float totalPlayTime;
}

public static class SaveSystem
{
    public static int SelectedSlot = 1;

    private static string GetSavePath(int slotIndex)
    {
        return Application.persistentDataPath + "/game_save_slot_" + slotIndex + ".json";
    }

    public static void SaveGame(int slotIndex, int _score, int _lives, int _maxHealth, int _levelIndex, bool _doubleJump, bool _dash, bool _heavyAttack, bool _thrust, int _artifacts, System.Collections.Generic.List<string> _exploredRooms, float _totalPlayTime)
    {
        SaveData data = new SaveData
        {
            score = _score,
            lives = _lives,
            maxHealth = _maxHealth,
            currentLevelIndex = _levelIndex,
            hasDoubleJump = _doubleJump,
            hasDash = _dash,
            hasHeavyAttack = _heavyAttack,
            hasThrust = _thrust,
            collectedArtifacts = _artifacts,
            exploredRooms = _exploredRooms,
            totalPlayTime = _totalPlayTime
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(slotIndex), json);
        Debug.Log("Прогресс сохранен в слот " + slotIndex + ": " + GetSavePath(slotIndex));
    }

    public static SaveData LoadGame(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("Прогресс загружен из слота " + slotIndex + "!");
            return data;
        }
        else
        {
            Debug.LogWarning("Файл сохранения в слоте " + slotIndex + " не найден.");
            return null; 
        }
    }

    public static bool SaveExists(int slotIndex)
    {
        return File.Exists(GetSavePath(slotIndex));
    }

    public static void DeleteSave(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Сохранение в слоте " + slotIndex + " удалено.");
        }
    }
}

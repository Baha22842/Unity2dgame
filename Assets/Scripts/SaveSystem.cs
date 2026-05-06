using UnityEngine;
using System.IO;

[System.Serializable]
public class SaveData
{
    public int score;
    public int lives;
    public int currentLevelIndex;
    
    // Навыки (Метроидвания)
    public bool hasDoubleJump;
    public bool hasDash;
    public bool hasHeavyAttack;
}

public static class SaveSystem
{
    private static string GetSavePath()
    {
        return Application.persistentDataPath + "/game_save.json";
    }

    public static void SaveGame(int _score, int _lives, int _levelIndex, bool _doubleJump, bool _dash, bool _heavyAttack)
    {
        SaveData data = new SaveData
        {
            score = _score,
            lives = _lives,
            currentLevelIndex = _levelIndex,
            hasDoubleJump = _doubleJump,
            hasDash = _dash,
            hasHeavyAttack = _heavyAttack
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(), json);
        Debug.Log("Прогресс сохранен в: " + GetSavePath());
    }

    public static SaveData LoadGame()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("Прогресс загружен!");
            return data;
        }
        else
        {
            Debug.LogWarning("Файл сохранения не найден.");
            return null; // Возвращаем пустоту, если сохранений еще нет
        }
    }

    public static void DeleteSave()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

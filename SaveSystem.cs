using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    public static void SaveToFile(string json)
    {
        File.WriteAllText(SavePath, json);
        Debug.Log($"💾 存档已保存到：{SavePath}");
    }

    public static string LoadFromFile()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning("⚠️ 没有找到存档文件。");
            return null;
        }
        return File.ReadAllText(SavePath);
    }

    public static void ClearFile()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("🗑️ 存档文件已删除。");
        }
    }
}

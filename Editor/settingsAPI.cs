// File: Assets/Editor/SettingsAPI.cs
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// JSON にシリアライズする設定データ
/// </summary>
[System.Serializable]
public class SettingsData
{
    public string rootFolder = "";
    public bool importOnClick = true;
    public int PackageSize = 80;
}

/// <summary>
/// 他スクリプトから SettingsData を読み書きする API
/// </summary>
public static class SettingsAPI
{
    // settings.json をプロジェクトの Assets/Editor フォルダ以下に配置
    private static readonly string _filePath =
        Path.Combine(Application.dataPath, "useful packager", "Data", "settings.json");

    private static SettingsData _data;

    // static コンストラクタで一度ロード
    static SettingsAPI()
    {
        Load();
    }

    /// <summary>
    /// settings.json から読み込み。存在しなければデフォルト生成。
    /// </summary>
    public static void Load()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            _data = JsonUtility.FromJson<SettingsData>(json);
        }
        else
        {
            _data = new SettingsData();
            Save();
        }
    }

    /// <summary>
    /// 現在の設定を settings.json に書き出し。
    /// </summary>
    public static void Save()
    {
        var json = JsonUtility.ToJson(_data, true);
        File.WriteAllText(_filePath, json);
        AssetDatabase.Refresh();  // Unity にファイルの更新を通知
    }

    // --- 以下、外部から使うプロパティ／メソッド ---

    public static string GetRootFolder() => _data.rootFolder;
    public static void SetRootFolder(string path)
    {
        _data.rootFolder = path;
        Save();
    }

    public static bool GetImportOnClick() => _data.importOnClick;
    public static void SetImportOnClick(bool enabled)
    {
        _data.importOnClick = enabled;
        Save();
    }

    public static int GetPackageSize() => _data.PackageSize;
    public static void SetPackageSize(int size)
    {
        _data.PackageSize = size;
        Save();
    }
}

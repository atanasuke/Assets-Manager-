// File: Assets/Editor/ImportStatusAPI.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public enum ImportFilter
{
    All = 0,
    Imported = 1,
    NotImported = 2,
    InfoMissing = 3,
}

[Serializable]
public class ImportStatusData
{
    public List<string> importedFolders = new List<string>();
}

public static class ImportStatusAPI
{
    private static readonly string FilePath = Path.Combine(Application.dataPath, "useful packager", "Data", "import_status.json");
    private static HashSet<string> _imported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded = false;

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        try
        {
            string full = Path.GetFullPath(path);
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            full = full.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return full.ToLowerInvariant();
        }
        catch
        {
            return path.ToLowerInvariant();
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<ImportStatusData>(json);
                _imported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in data?.importedFolders ?? new List<string>())
                    _imported.Add(NormalizePath(p));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"ImportStatus load failed: {e.Message}");
        }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var data = new ImportStatusData { importedFolders = new List<string>(_imported) };
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError($"ImportStatus save failed: {e.Message}");
        }
    }

    public static bool IsImported(string folderPath)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(folderPath)) return false;
        return _imported.Contains(NormalizePath(folderPath));
    }

    public static void MarkImported(string folderPath)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(folderPath)) return;
        string norm = NormalizePath(folderPath);
        if (_imported.Add(norm))
        {
            Save();
        }
    }
}



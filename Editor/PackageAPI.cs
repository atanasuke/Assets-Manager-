// File: Assets/Editor/PackageAPI.cs
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Compression;

public class PackageAPIEntry
{
    public int                 Index;             // フォルダ単位の連番
    public string              FolderName;        // フォルダ名
    public string              FolderPath;        // フォルダのフルパス
    public List<string>        ZipPaths;          // フォルダ内のすべての ZIP ファイルパス
    public List<List<string>>  PackageNames;      // ZIPごとに含まれる .unitypackage 名リスト
    public List<List<string>>  PackagePathsInZip; // ZIPごとの .unitypackage 内パスリスト
    public string              MetaPath;          // meta.json のパス（なければ null）
    public Texture2D           Thumbnail;         // thumbnail.jpg テクスチャ（なければ null）
}

public static class PackageAPI
{
    // ─── キャッシュ ───
    private static List<PackageAPIEntry> _cache = new List<PackageAPIEntry>();

    // ─── 選択中インデックス ───
    private static int _selectedIndex = -1;
    public static int GetSelectedIndex() => _selectedIndex;
    public static void SetSelectedIndex(int i) => _selectedIndex = i;

    // ─── 実際の走査を行いキャッシュを更新 ───
    public static void LoadPackages()
    {
        _cache.Clear();
        string root = SettingsAPI.GetRootFolder();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return;

        int idx = 0;
        foreach (var dir in Directory.GetDirectories(root))
        {
            // 基本フォルダ情報
            var entry = new PackageAPIEntry
            {
                Index           = idx++,
                FolderName      = Path.GetFileName(dir),
                FolderPath      = dir,
                ZipPaths        = new List<string>(),
                PackageNames    = new List<List<string>>(),
                PackagePathsInZip = new List<List<string>>(),
                MetaPath        = Path.Combine(dir, "meta.json"),
                Thumbnail       = null
            };

            // meta.json と thumbnail.jpg のチェック
            if (!File.Exists(entry.MetaPath)) entry.MetaPath = null;
            var thumbFile = Path.Combine(dir, "thumbnail.jpg");
            entry.Thumbnail = LoadTexture(thumbFile);

            // フォルダ内の全 ZIP を処理
            foreach (var zipPath in Directory.GetFiles(dir, "*.zip"))
            {
                entry.ZipPaths.Add(zipPath);

                // ZIP内の unitypackage をまとめるリスト
                var names   = new List<string>();
                var pathsIn = new List<string>();

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var zipEntry in archive.Entries.Where(e => e.Name.EndsWith(".unitypackage")))
                    {
                        names.Add(zipEntry.Name);
                        pathsIn.Add(zipEntry.FullName);
                    }
                }

                entry.PackageNames.Add(names);
                entry.PackagePathsInZip.Add(pathsIn);
            }

            _cache.Add(entry);
        }

        // ソート：フォルダ名順
        _cache = _cache.OrderBy(e => e.FolderName).ToList();

        // インデックス再付与
        for (int i = 0; i < _cache.Count; i++)
            _cache[i].Index = i;
    }

    // ─── キャッシュを返すオーバーロード群 ───

    /// <summary>
    /// キャッシュをそのまま返します（キャッシュが空なら初回は再走査してください）。
    /// </summary>
    public static List<PackageAPIEntry> GetPackages()
    {
        return new List<PackageAPIEntry>(_cache);
    }

    /// <summary>
    /// 指定インデックスのみを抽出して返します。
    /// </summary>
    public static List<PackageAPIEntry> GetPackages(IEnumerable<int> indices)
    {
        if (indices == null)
        {
            // null を渡された場合は空集合を返す（フィルタで0件を明示したいケース）
            return new List<PackageAPIEntry>();
        }
        var list = indices.ToList();
        var result = new List<PackageAPIEntry>();
        foreach (var i in list)
        {
            var entry = _cache.FirstOrDefault(e => e.Index == i);
            if (entry != null)
                result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// "Load" を渡すと再走査 → キャッシュ全返し。
    /// それ以外の文字列は無視してキャッシュ全返し。
    /// </summary>
    public static List<PackageAPIEntry> GetPackages(string command)
    {
        if (command == "Load")
            LoadPackages();
        return GetPackages();
    }


    // thumbnail.jpg を Texture2D として読み込むヘルパー
    private static Texture2D LoadTexture(string path)
    {
        byte[] bytes = null;
        var tex = new Texture2D(2, 2);
        if (path == null || !File.Exists(path))
        {
            string errorpath = Path.Combine(Application.dataPath, "useful packager", "Data", "console.png");
            bytes = File.ReadAllBytes(errorpath);
            tex.LoadImage(bytes);
            return tex;
        }
        bytes = File.ReadAllBytes(path);
        tex.LoadImage(bytes);
        return tex;
    }
}

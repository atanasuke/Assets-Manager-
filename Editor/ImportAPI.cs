// File: Assets/Editor/ImportAPI.cs
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Diagnostics;


public class ImportItem
{
    /// <summary>ZIP ファイルのフルパス</summary>
    public string ZipPath;
    /// <summary>ZIP 内の .unitypackage ファイル名（例: "MyPackage.unitypackage"）</summary>
    public string PackageFileName;
}

public class ImportProgress
{
    public string ZipPath;
    public string PackageFileName;
    public TimeSpan Elapsed;
    public bool Success;
    public string Message;
}

public static class ImportAPI
{
    public static event Action<ImportProgress> PackageImported;
    public static event Action<TimeSpan, int, int> AllImportsCompleted; // totalElapsed, successCount, totalCount

    /// <summary>
    /// 複数の ImportItem を受け取り、それぞれの ZIP 内にある
    /// 指定 .unitypackage を一時展開→インポート→削除します。
    /// </summary>
    /// <param name="items">ZipPath と PackageFileName を設定したリスト</param>
    public static void PackageImporter(List<ImportItem> items)
    {
        var totalSw = Stopwatch.StartNew();
        int successCount = 0;
        int totalCount = items?.Count ?? 0;

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.ZipPath) || string.IsNullOrEmpty(item.PackageFileName))
            {
                UnityEngine.Debug.LogWarning("ImportAPI: 無効な ImportItem をスキップ");
                PackageImported?.Invoke(new ImportProgress
                {
                    ZipPath = item?.ZipPath,
                    PackageFileName = item?.PackageFileName,
                    Elapsed = TimeSpan.Zero,
                    Success = false,
                    Message = "無効な ImportItem をスキップ"
                });
                continue;
            }

            if (!File.Exists(item.ZipPath))
            {
                UnityEngine.Debug.LogError($"ImportAPI: ZIP が見つかりません: {item.ZipPath}");
                PackageImported?.Invoke(new ImportProgress
                {
                    ZipPath = item.ZipPath,
                    PackageFileName = item.PackageFileName,
                    Elapsed = TimeSpan.Zero,
                    Success = false,
                    Message = "ZIP が見つかりません"
                });
                continue;
            }
            UnityEngine.Debug.Log($"ImportAPI: ZIP を読み込みます: {item.ZipPath} {item.PackageFileName}");

            try
            {
                var sw = Stopwatch.StartNew();
                using (var archive = ZipFile.OpenRead(item.ZipPath))
                {
                    // ZIP 内の対象エントリを FullName (パス込み) で検索
                    var entry = archive.Entries
                        .FirstOrDefault(e => e.FullName.EndsWith(item.PackageFileName, StringComparison.OrdinalIgnoreCase));
                    if (entry == null)
                    {
                        UnityEngine.Debug.LogError($"ImportAPI: ZIP 内に該当パッケージがありません: {item.PackageFileName}");
                        sw.Stop();
                        PackageImported?.Invoke(new ImportProgress
                        {
                            ZipPath = item.ZipPath,
                            PackageFileName = item.PackageFileName,
                            Elapsed = sw.Elapsed,
                            Success = false,
                            Message = "ZIP 内に該当パッケージがありません"
                        });
                        continue;
                    }

                    // 一時フォルダに展開
                    string tempDir = Path.Combine(Path.GetTempPath(), "UnityZipImport");
                    Directory.CreateDirectory(tempDir);
                    string tempPath = Path.Combine(tempDir, entry.Name);
                    entry.ExtractToFile(tempPath, overwrite: true);

                    // インポート（ダイアログ無し・自動承認で全てインポート）
                    AssetDatabase.ImportPackage(tempPath, false);
                    UnityEngine.Debug.Log($"ImportAPI: インポート完了 {entry.Name}");

                    // 一時ファイル／フォルダを削除
                    File.Delete(tempPath);
                    // ディレクトリが空なら消す
                    if (Directory.GetFiles(tempDir).Length == 0)
                        Directory.Delete(tempDir);
                }
                sw.Stop();
                successCount++;
                // 成功時に、フォルダ単位でインポート済みフラグを立てる
                try
                {
                    // item.ZipPath はフォルダ内の ZIP。親フォルダがパッケージフォルダ
                    string folder = Path.GetDirectoryName(item.ZipPath);
                    ImportStatusAPI.MarkImported(folder);
                }
                catch {}
                PackageImported?.Invoke(new ImportProgress
                {
                    ZipPath = item.ZipPath,
                    PackageFileName = item.PackageFileName,
                    Elapsed = sw.Elapsed,
                    Success = true,
                    Message = "インポート完了"
                });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"ImportAPI: インポート中に例外: {e}");
                PackageImported?.Invoke(new ImportProgress
                {
                    ZipPath = item.ZipPath,
                    PackageFileName = item.PackageFileName,
                    Elapsed = TimeSpan.Zero,
                    Success = false,
                    Message = $"例外: {e.Message}"
                });
            }
        }
        totalSw.Stop();
        AllImportsCompleted?.Invoke(totalSw.Elapsed, successCount, totalCount);
    }
}

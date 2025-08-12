// File: Assets/Editor/InfoUI.cs
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;

[Serializable]
public class MetaInfo
{
    public string productName;
    public string productUrl;
    public string shopName;
    public string shopUrl;
    public string mainCategory;
    public string subCategory;
    public string[] extraCategories;
    public long publishedTimestamp;
    public long currentTimestamp;
}

public static class InfoUI
{
    private static PackageAPIEntry _entry;
    private static MetaInfo _meta;

    private static string productUrl = "";

    private static bool _selectAll = false;
    private static bool[] _zipFoldouts;   // ZIPごとの折りたたみ制御
    private static bool[] _zipToggles;    // ZIPごとの全選択トグル
    private static bool[][] _pkgToggles;    // [zipIndex][pkgIndex] の個別トグル

    // ログ管理（マルチ選択時に使用）
    private static List<string> _logLines = new List<string>();
    private static DateTime _lastAllImportStartedAt;

    private static string SanitizePathSegment(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var ch in invalid)
            name = name.Replace(ch.ToString(), "_");
        return name.Trim();
    }

    private static int ExtractAnimFilesFromZip(string zipPath, string destRelativeUnderAssets)
    {
        try
        {
            string dest = Path.Combine(Application.dataPath, destRelativeUnderAssets);
            if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

            int count = 0;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) continue;
                    string outPath = Path.Combine(dest, Path.GetFileName(entry.FullName));
                    entry.ExtractToFile(outPath, true);
                    count++;
                }
            }
            AssetDatabase.Refresh();
            return count;
        }
        catch (Exception e)
        {
            Debug.LogError($"Extract .anim failed: {e.Message}");
            return 0;
        }
    }
    private static int ExtractPngFilesFromZip(string zipPath, string destRelativeUnderAssets)
    {
        try
        {
            string dest = Path.Combine(Application.dataPath, destRelativeUnderAssets);
            if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

            int count = 0;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                    string outPath = Path.Combine(dest, Path.GetFileName(entry.FullName));
                    entry.ExtractToFile(outPath, true);
                    count++;
                }
            }
            AssetDatabase.Refresh();
            return count;
        }
        catch (Exception e)
        {
            Debug.LogError($"Extract .png failed: {e.Message}");
            return 0;
        }
    }

    private static void WriteLogsToFile()
    {
        try
        {
            string logsDir = Path.Combine(Application.dataPath, "useful packager", "Logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(logsDir, $"import_{stamp}.txt");

            var content = new List<string>();
            content.Add($"Started: {_lastAllImportStartedAt:yyyy-MM-dd HH:mm:ss}");
            content.Add($"Ended  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            content.Add("");
            content.AddRange(_logLines);
            File.WriteAllLines(filePath, content);

            // 5個以上なら最古を削除
            var files = new DirectoryInfo(logsDir)
                .GetFiles("*.txt")
                .OrderBy(f => f.CreationTimeUtc)
                .ToList();
            if (files.Count >= 5)
            {
                try { files[0].Delete(); }
                catch { /* ignore */ }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write logs: {e.Message}");
        }
    }

    /// <summary>
    /// 選択されたパッケージインデックスを受け取り、必要データを読み込みます。
    /// </summary>
    /// <param name="index">PackageAPIEntry.Index</param>
    public static void ReloadPackageInfo(int index)
    {
        // 指定インデックスのエントリを取得
        var list = PackageAPI.GetPackages(new List<int> { index });
        _entry = list.FirstOrDefault();
        _meta = null;
        _logLines.Clear();

        if (_entry != null && File.Exists(_entry.MetaPath))
        {
            try
            {
                var json = File.ReadAllText(_entry.MetaPath);
                _meta = JsonUtility.FromJson<MetaInfo>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse meta.json: {e}");
            }
            int zips = _entry.ZipPaths.Count;
            _zipFoldouts = new bool[zips];
            _zipToggles = new bool[zips];
            _pkgToggles = new bool[zips][];
            for (int i = 0; i < zips; i++)
            {
                // 初期は全部オフ
                _zipFoldouts[i] = true;
                _zipToggles[i] = false;
                int cnt = _entry.PackageNames[i].Count;
                _pkgToggles[i] = new bool[cnt];
                for (int j = 0; j < cnt; j++)
                    _pkgToggles[i][j] = false;
            }
            _selectAll = false;
        }
    }

    /// <summary>
    /// 情報パネルを描画します。呼び出し元でスクロール位置を管理してください。
    /// </summary>
    public static void DrawInfoPanel(ref Vector2 scrollPos)
    {
        EditorGUILayout.BeginVertical();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (_entry != null)
        {

            GUIStyle centeredStyle = new GUIStyle(GUI.skin.label);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            // サムネイル
            if (_entry.Thumbnail != null)
                GUILayout.BeginHorizontal();     // 横並び
            GUILayout.FlexibleSpace();       // 左側の余白で中央寄せ
            GUILayout.Label(_entry.Thumbnail, centeredStyle, GUILayout.Width(200), GUILayout.Height(200));
            GUILayout.FlexibleSpace();       // 右側の余白
            GUILayout.EndHorizontal();

            if (_meta != null)
            {
                GUIStyle style = new GUIStyle(EditorStyles.label);
                Vector2 size = style.CalcSize(new GUIContent(_meta.productName));
                LinkLabelUtility.DrawLink(
                    _meta.productName,
                    _meta.productUrl,
                    width: size.x * 1.2f,
                    height: 24
                );

                size = style.CalcSize(new GUIContent(_meta.shopName));
                LinkLabelUtility.DrawLink(
                    _meta.shopName,
                    _meta.shopUrl,
                    width: size.x * 1.2f,
                    height: 20
                );



                EditorGUILayout.Space();

                // カテゴリ
                EditorGUILayout.LabelField("カテゴリ:", _meta.mainCategory);
                EditorGUILayout.LabelField("サブカテゴリ:", _meta.subCategory);
                if (_meta.extraCategories.Length > 0)
                {
                    EditorGUILayout.LabelField("追加カテゴリ:", _meta.extraCategories[0]);
                }
                EditorGUILayout.Space();

                // タイムスタンプ表示
                var pubTime = DateTimeOffset.FromUnixTimeSeconds(_meta.publishedTimestamp / 1000)
                                           .ToLocalTime()
                                           .ToString("yyyy-MM-dd HH:mm:ss");
                var nowTime = DateTimeOffset.FromUnixTimeSeconds(_meta.currentTimestamp / 1000)
                                           .ToLocalTime()
                                           .ToString("yyyy-MM-dd HH:mm:ss");
                EditorGUILayout.LabelField("発売日:", pubTime);
                EditorGUILayout.LabelField("ダウンロード時間:", nowTime);
            }
            else
            {
                EditorGUILayout.HelpBox("meta.jsonまたはThumbnail.jpg が見つからないか、解析に失敗しました。\n以下のテキストボックスに商品のurlを入力してリダイレクトボタンを押すと取得できます", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("商品のurl", GUILayout.Width(100));
                productUrl = EditorGUILayout.TextField("", productUrl);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("商品の情報を取得※ブラウザを開きます！", GUILayout.Width(200)))
                {
                    Debug.Log("url: " + productUrl);
                    // クリップボードにコピー
                    EditorGUIUtility.systemCopyBuffer = productUrl + "?boothDL=2";
                    // ブラウザで開く
                    Application.OpenURL(productUrl + "?boothDL=2");
                }
            }
            if (_entry == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import Packages", EditorStyles.boldLabel);

            // 全選択トグル
            bool newSelectAll = EditorGUILayout.ToggleLeft("全選択", _selectAll);
            if (newSelectAll != _selectAll)
            {
                _selectAll = newSelectAll;
                for (int zi = 0; zi < _zipToggles.Length; zi++)
                {
                    _zipToggles[zi] = _selectAll;
                    for (int pj = 0; pj < _pkgToggles[zi].Length; pj++)
                        _pkgToggles[zi][pj] = _selectAll;
                }
            }

            for (int zi = 0; zi < _entry.ZipPaths.Count; zi++)
            {
                EditorGUILayout.BeginHorizontal();

                // ① 左端にチェックボックス
                bool newZip = EditorGUILayout.Toggle(
_zipToggles[zi],
GUILayout.Width(14)
    );
                if (newZip != _zipToggles[zi])
                {
                    _zipToggles[zi] = newZip;
                    // ZIP直下のすべての unitypackage に連動
                    for (int pj = 0; pj < _pkgToggles[zi].Length; pj++)
                        _pkgToggles[zi][pj] = newZip;
                }

                // ② 折りたたみ矢印＋ZIP名
                //    toggleOnLabelClick を true にすると、ラベルクリックでも開閉
                _zipFoldouts[zi] = EditorGUILayout.Foldout(
                    _zipFoldouts[zi],
                    Path.GetFileName(_entry.ZipPaths[zi]),
                    true
                );

                EditorGUILayout.EndHorizontal();

                if (_zipFoldouts[zi])
                {
                    // パッケージごと
                    for (int pj = 0; pj < _entry.PackageNames[zi].Count; pj++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        _pkgToggles[zi][pj] = EditorGUILayout.Toggle(
                            _pkgToggles[zi][pj],
                            GUILayout.Width(14)
                        );
                        EditorGUILayout.LabelField(_entry.PackageNames[zi][pj], GUILayout.Width(200));
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            // インポート実行ボタン
            if (GUILayout.Button("Import Selected", GUILayout.Height(30)))
            {
                // 特殊処理: unitypackage が存在しない ZIP をカテゴリに応じて展開
                bool isDanceOrPose = _meta?.extraCategories != null && _meta.extraCategories.Any(c => c != null && (c.Contains("ダンス") || c.Contains("ポーズ")));
                bool isEyeOrMakeup = _meta?.extraCategories != null && _meta.extraCategories.Any(c => c != null && (c.Contains("アイテクスチャ") || c.Contains("メイクアップ")));

                if (isDanceOrPose || isEyeOrMakeup)
                {
                    for (int zi = 0; zi < _entry.ZipPaths.Count; zi++)
                    {
                        var namesInZip = _entry.PackageNames[zi];
                        if (namesInZip == null || namesInZip.Count == 0)
                        {
                            string zipPath = _entry.ZipPaths[zi];
                            string shop = SanitizePathSegment(_meta?.shopName ?? "UnknownShop");
                            string product = SanitizePathSegment(_meta?.productName ?? "UnknownProduct");
                            string destRel = Path.Combine(shop, product);
                            if (isDanceOrPose)
                            {
                                int extracted = ExtractAnimFilesFromZip(zipPath, destRel);
                                Debug.Log($"[Extracted] {Path.GetFileName(zipPath)} -> Assets/{destRel} : {extracted} .anim");
                            }
                            if (isEyeOrMakeup)
                            {
                                int extractedPng = ExtractPngFilesFromZip(zipPath, destRel);
                                Debug.Log($"[Extracted] {Path.GetFileName(zipPath)} -> Assets/{destRel} : {extractedPng} .png");
                            }
                        }
                    }
                }

                var toImport = new List<ImportItem>();
                for (int zi = 0; zi < _entry.ZipPaths.Count; zi++)
                {
                    string zipPath = _entry.ZipPaths[zi];
                    for (int pj = 0; pj < _pkgToggles[zi].Length; pj++)
                    {
                        if (_pkgToggles[zi][pj])
                        {
                            toImport.Add(new ImportItem
                            {
                                ZipPath = zipPath,
                                PackageFileName = _entry.PackageNames[zi][pj]
                            });
                        }
                    }
                }
                // Material を優先
                var ordered = toImport
                    .OrderByDescending(it => (it.PackageFileName ?? "").ToLower().Contains("material"))
                    .ThenBy(it => it.PackageFileName)
                    .ToList();
                ImportAPI.PackageImporter(ordered);


            }
        }
        else
        {
            EditorGUILayout.LabelField("パッケージが選択されていません。", EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 複数選択モード用の情報パネル。
    /// 選択されたフォルダ群の概要と ALL IMPORT ボタンを表示。
    /// </summary>
    public static void DrawMultiSelectPanel(HashSet<int> selectedIndices, ref Vector2 scrollPos)
    {
        EditorGUILayout.BeginVertical();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (selectedIndices == null || selectedIndices.Count == 0)
        {
            EditorGUILayout.LabelField("複数選択モード: パッケージを選択してください。", EditorStyles.wordWrappedLabel);
        }
        else
        {
            var entries = PackageAPI.GetPackages(selectedIndices);
            EditorGUILayout.LabelField($"選択中: {entries.Count} パッケージ", EditorStyles.boldLabel);

            // 簡易リスト表示
            foreach (var e in entries)
            {
                EditorGUILayout.LabelField($"- {e.FolderName}");
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("ALL IMPORT", GUILayout.Height(30)))
            {
                _logLines.Clear();
                _lastAllImportStartedAt = DateTime.Now;

                // 一時的にイベント購読してログ収集
                void OnPkgImported(ImportProgress p)
                {
                    _logLines.Add($"[Imported] {p.PackageFileName} ({p.Elapsed.TotalSeconds:F2}s) : {(p.Success ? "OK" : "NG")} - {p.Message}");
                }
                void OnAllDone(TimeSpan elapsed, int success, int total)
                {
                    _logLines.Add($"[Completed] {success}/{total} 成功 | Total {elapsed.TotalSeconds:F2}s");
                    WriteLogsToFile();
                }
                ImportAPI.PackageImported += OnPkgImported;
                ImportAPI.AllImportsCompleted += OnAllDone;

                // 事前警告: エクストラカテゴリに "unity拡張ツール" を含むものがあるか
                bool hasUnityExtTool = false;
                var entryToMeta = new Dictionary<int, MetaInfo>();
                foreach (var e in entries)
                {
                    MetaInfo meta = null;
                    if (e.MetaPath != null && File.Exists(e.MetaPath))
                    {
                        try { meta = JsonUtility.FromJson<MetaInfo>(File.ReadAllText(e.MetaPath)); }
                        catch { meta = null; }
                    }
                    entryToMeta[e.Index] = meta;
                    if (meta?.extraCategories != null && meta.extraCategories.Any(c => c != null && c.Contains("unity拡張ツール")))
                        hasUnityExtTool = true;
                }
                if (hasUnityExtTool)
                {
                    bool proceed = EditorUtility.DisplayDialog(
                        "警告",
                        "unityエディタ拡張ツールが正常にインストールされない可能性がありますがよろしいですか？",
                        "OK",
                        "キャンセル"
                    );
                    if (!proceed)
                    {
                        // 購読解除して終了
                        ImportAPI.PackageImported -= OnPkgImported;
                        ImportAPI.AllImportsCompleted -= OnAllDone;
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                        return;
                    }
                }

                var toImport = new List<ImportItem>();
                foreach (var e in entries)
                {
                    for (int zi = 0; zi < e.ZipPaths.Count; zi++)
                    {
                        string zipPath = e.ZipPaths[zi];
                        var names = e.PackageNames[zi];

                        // ダンス/ポーズカテゴリかつ unitypackage がない ZIP は .anim を展開
                        var meta = entryToMeta[e.Index];
                        bool isDanceOrPose = meta?.extraCategories != null && meta.extraCategories.Any(c => c != null && (c.Contains("ダンス") || c.Contains("ポーズ")));
                        if (isDanceOrPose && (names == null || names.Count == 0))
                        {
                            string shop = SanitizePathSegment(meta?.shopName ?? "UnknownShop");
                            string product = SanitizePathSegment(meta?.productName ?? "UnknownProduct");
                            string destRel = Path.Combine(shop, product);
                            int extracted = ExtractAnimFilesFromZip(zipPath, destRel);
                            _logLines.Add($"[Extracted] {Path.GetFileName(zipPath)} -> Assets/{destRel} : {extracted} .anim");
                            continue;
                        }

                        // 通常: unitypackage をインポート対象に追加
                        for (int pj = 0; pj < names.Count; pj++)
                        {
                            toImport.Add(new ImportItem
                            {
                                ZipPath = zipPath,
                                PackageFileName = names[pj]
                            });
                        }
                    }
                }
                if (toImport.Count > 0)
                {
                    // Material を優先してインポート
                    var ordered = toImport
                        .OrderByDescending(it => it.PackageFileName != null && it.PackageFileName.ToLower().Contains("material"))
                        .ThenBy(it => it.PackageFileName)
                        .ToList();
                    ImportAPI.PackageImporter(ordered);
                }
                else
                {
                    // インポート対象が無い場合でもログをファイルへ
                    _logLines.Add("[Completed] 0/0 成功 | Total 0.00s");
                    WriteLogsToFile();
                }

                // 実行後にイベント購読解除
                ImportAPI.PackageImported -= OnPkgImported;
                ImportAPI.AllImportsCompleted -= OnAllDone;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Logs", EditorStyles.boldLabel);
            foreach (var line in _logLines)
            {
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
}

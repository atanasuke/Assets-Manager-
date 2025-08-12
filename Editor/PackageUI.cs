// File: Assets/Editor/PackageUI.cs
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class PackageUI
{
    // 旧シグネチャ互換オーバーロード
    public static void DrawPanel(
        List<PackageAPIEntry> packages,
        ref Vector2 scrollPos,
        string searchQuery,
        System.Action<PackageAPIEntry> onSelect,
        float panelWidth
    )
    {
        DrawPanel(packages, ref scrollPos, searchQuery, onSelect, panelWidth, false, null);
    }

    /// <summary>
    /// 左側のパッケージ一覧パネルを描画します。
    /// </summary>
    /// <param name="packages">PackageAPI.GetPackages() の結果リスト</param>
    /// <param name="scrollPos">スクロール位置（ref で保持してください）</param>
    /// <param name="searchQuery">検索フィルタ文字列</param>
    /// <param name="onSelect">サムネイルクリック時のコールバック</param>
    public static void DrawPanel(
        List<PackageAPIEntry> packages,
        ref Vector2 scrollPos,
        string searchQuery,
        Action<PackageAPIEntry> onSelect,
        float panelWidth,
        bool multiSelectMode,
        System.Collections.Generic.HashSet<int> multiSelected
    )
    {
        // ① ボタンのサイズを先に取得
        int btncount = 17 - SettingsAPI.GetPackageSize();


        // ③ 何列並べられるか計算
        int btnSize = Mathf.Max(1, Mathf.FloorToInt(panelWidth / btncount));
        int cols = Mathf.Max(1, Mathf.FloorToInt(panelWidth / btnSize));

        // ④ スクロール開始
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(panelWidth));
        EditorGUILayout.BeginHorizontal();

        int count = 0;
        foreach (var pkg in packages)
        {
            // 検索フィルタ
            if (!string.IsNullOrEmpty(searchQuery) &&
                !pkg.FolderName.ToLower().Contains(searchQuery.ToLower()))
                continue;

            // 改行判定：ボタン数が cols に達したら改行
            if (count++ % cols == 0 && count > 1)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
            if (pkg.Thumbnail == null) continue;

            // ⑤ ボタン表示（余白を含めて btnSize×btnSize で表示）
            // 緑枠のためのスタイル
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            if (multiSelectMode && multiSelected != null && multiSelected.Contains(pkg.Index))
            {
                btnStyle.normal.background = pkg.Thumbnail;
                btnStyle.active.background = pkg.Thumbnail;
                btnStyle.hover.background  = pkg.Thumbnail;
                btnStyle.margin = new RectOffset(2,2,2,2);
                btnStyle.border = new RectOffset(4,4,4,4);

                // 枠を描画するため一旦プレースホルダーボタンで領域を確保し手動描画
                Rect r = GUILayoutUtility.GetRect(btnSize-(24/btncount), btnSize-(24/btncount));
                // 背景としてサムネイル描画
                GUI.DrawTexture(r, pkg.Thumbnail, ScaleMode.ScaleToFit);
                // 緑枠
                Color prev = GUI.color;
                Handles.BeginGUI();
                Handles.color = Color.green;
                Handles.DrawAAPolyLine(3f, new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin));
                Handles.DrawAAPolyLine(3f, new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax));
                Handles.DrawAAPolyLine(3f, new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax));
                Handles.DrawAAPolyLine(3f, new Vector3(r.xMin, r.yMax), new Vector3(r.xMin, r.yMin));
                Handles.EndGUI();
                GUI.color = prev;

                // クリック検出
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    onSelect?.Invoke(pkg);
                    Event.current.Use();
                }
            }
            else if (GUILayout.Button(
                    pkg.Thumbnail,
                    GUILayout.Width(btnSize-(24/btncount)),
                    GUILayout.Height(btnSize-(24/btncount))
                ))
            {
                onSelect?.Invoke(pkg);
                if (!multiSelectMode)
                {
                    PackageAPI.SetSelectedIndex(pkg.Index);
                    InfoUI.ReloadPackageInfo(pkg.Index);
                }

                // 設定で有効かつマルチ選択モードでない場合のみ自動インポート
                if (!multiSelectMode && SettingsAPI.GetImportOnClick())
                {
                    var toImport = new List<ImportItem>();
                    for (int zi = 0; zi < pkg.ZipPaths.Count; zi++)
                    {
                        string zipPath = pkg.ZipPaths[zi];
                        var names = pkg.PackageNames[zi];
                        for (int pj = 0; pj < names.Count; pj++)
                        {
                            toImport.Add(new ImportItem
                            {
                                ZipPath = zipPath,
                                PackageFileName = names[pj]
                            });
                        }
                    }

                    if (toImport.Count > 0)
                    {
                        var ordered = toImport
                            .OrderByDescending(it => (it.PackageFileName ?? "").ToLower().Contains("material"))
                            .ThenBy(it => it.PackageFileName)
                            .ToList();
                        ImportAPI.PackageImporter(ordered);
                    }
                }
            }

        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }
}

// File: Assets/Editor/MainMenu.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class MainMenu : EditorWindow
{
    private Vector2 _packageScroll;
    private Vector2 _infoScroll;
    private string  _searchQuery = "";
    private List<PackageAPIEntry> _packages = new List<PackageAPIEntry>();
    private bool _multiSelectMode = false;
    private HashSet<int> _multiSelectedIndices = new HashSet<int>();

    [MenuItem("Window/Package Importer")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<MainMenu>("Package Importer");
        wnd.minSize = new Vector2(600, 400);
        wnd.ReloadPackages();
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space();
        // 厳密幅で3カラムを構成（隙間を作らない）
        float total = position.width;
        float sortWidth = SortUI.IsOpen() ? Mathf.Clamp(total * 0.25f, 180f, total * 0.5f) : 0f;
        float infoWidth = Mathf.Clamp(total * 0.35f, 260f, total * 0.6f);
        float packageWidth = Mathf.Max(220f, total - sortWidth - infoWidth);

        EditorGUILayout.BeginHorizontal();
            if (SortUI.IsOpen())
            {
                GUILayout.BeginVertical(GUILayout.Width(sortWidth));
                SortUI.DrawSidePanel(sortWidth);
                GUILayout.EndVertical();
            }
            GUILayout.BeginVertical(GUILayout.Width(packageWidth));
            DrawPackagePanel(packageWidth);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(infoWidth));
            DrawInfoPanel(infoWidth);
            GUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            // 検索フィールド
            _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarTextField, GUILayout.Width(200));
            // ソートパネル ボタン
            SortUI.DrawToolbarButton();

            // 複数選択モード トグル
            bool newMulti = GUILayout.Toggle(_multiSelectMode, "Multi Select", EditorStyles.toolbarButton, GUILayout.Width(100));
            if (newMulti != _multiSelectMode)
            {
                _multiSelectMode = newMulti;
                _multiSelectedIndices.Clear();
                if (!_multiSelectMode)
                {
                    // シングル選択に戻ったら Info を再ロード
                    var sel = PackageAPI.GetSelectedIndex();
                    if (sel >= 0)
                        InfoUI.ReloadPackageInfo(sel);
                }
            }

            GUILayout.FlexibleSpace();

            // 設定ボタン
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
            {
                SettingsWindow.ShowSettings();
                PackageAPI.LoadPackages();
            }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPackagePanel(float panelWidth)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth));
        // 新フロー（順序入替）：PackageAPI → Import状態 → サブカテゴリ → エクストラ → 並び替え
        var all = PackageAPI.GetPackages();
        // 1) インポート状態フィルタ（全体に対して）
        var byImport = SortAPI.ApplyImportStateFilter(all, null);
        var pkgs = PackageAPI.GetPackages(byImport);
        // 2) サブ/エクストラ（インポート状態適用後の集合に対して）
        List<int> subExtraFiltered = SortAPI.GetFilteredIndices(pkgs);
        if (SortAPI.HasActiveCategoryFilter())
        {
            if (subExtraFiltered.Count == 0)
                pkgs = PackageAPI.GetPackages((IEnumerable<int>)null);
            else
                pkgs = PackageAPI.GetPackages(subExtraFiltered);
        }
        // 3. 並び順確定
        var indices = SortAPI.GetSortedIndices(pkgs);
        if (indices != null && indices.Count == pkgs.Count)
        {
            var map = new Dictionary<int, PackageAPIEntry>();
            foreach (var p in pkgs) map[p.Index] = p;
            var reordered = new List<PackageAPIEntry>(pkgs.Count);
            foreach (var i in indices) if (map.TryGetValue(i, out var e)) reordered.Add(e);
            pkgs = reordered;
        }
        PackageUI.DrawPanel(
            pkgs,
        ref _packageScroll,
        _searchQuery,
            entry =>
            {
                if (_multiSelectMode)
                {
                    // マルチ選択ではクリックは選択トグル
                    if (_multiSelectedIndices.Contains(entry.Index))
                        _multiSelectedIndices.Remove(entry.Index);
                    else
                        _multiSelectedIndices.Add(entry.Index);
                }
                else
                {
                    InfoUI.ReloadPackageInfo(entry.Index);
                    PackageAPI.SetSelectedIndex(entry.Index);
                }
            },
            panelWidth,
            _multiSelectMode,
            _multiSelectedIndices
    );
    EditorGUILayout.EndVertical();
}


    private void DrawInfoPanel(float panelWidth)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth));
        if (_multiSelectMode)
        {
            InfoUI.DrawMultiSelectPanel(_multiSelectedIndices, ref _infoScroll);
        }
        else
        {
            InfoUI.DrawInfoPanel(ref _infoScroll);
        }
        EditorGUILayout.EndVertical();
    }

    private void ReloadPackages()
    {
        PackageAPI.LoadPackages();
    }
}

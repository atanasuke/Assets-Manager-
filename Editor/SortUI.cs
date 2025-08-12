// File: Assets/Editor/SortUI.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class SortUI
{
    private static bool _panelOpen = false;
    private static Vector2 _scroll;
    private static readonly string[] _display = new[]
    {
        "商品名 (A→Z)",
        "ショップ名 (A→Z)",
        "商品公開日 (新しい順)",
        "ダウンロードした日 (新しい順)"
    };

    public static void DrawToolbarButton()
    {
        if (GUILayout.Button("Sort", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            _panelOpen = !_panelOpen;
        }
    }

    public static bool IsOpen() => _panelOpen;

    // 左から押し出すパネル
    public static void DrawSidePanel(float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Sort", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("並び替え", EditorStyles.miniBoldLabel);
        var current = SortAPI.GetCurrent();
        float minW = Mathf.Clamp(width - 40f, 180f, 420f);
        EditorGUILayout.BeginHorizontal();
        int newIndex = EditorGUILayout.Popup((int)current, _display, GUILayout.MinWidth(minW));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        if (newIndex != (int)current)
        {
            SortAPI.SetCurrent((SortKey)newIndex);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("絞り込み", EditorStyles.boldLabel);
        var pkgs = PackageAPI.GetPackages();
        // インポート状態（先頭に表示）
        SortAPI.GetSubCategoryOptionsWithCounts(pkgs, out var tmpSubLabels, out var tmpSubValues);
        var baseFiltered = SortAPI.GetFilteredIndices(pkgs);
        SortAPI.GetImportFilterOptionsWithCounts(pkgs, baseFiltered, out var importLabels, out var importValues);
        int importIndex = (int)SortAPI.GetImportFilter();
        EditorGUILayout.BeginHorizontal();
        int newImportIndex = EditorGUILayout.Popup(new GUIContent("インポート状態"), importIndex, importLabels, GUILayout.MinWidth(minW));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        if (newImportIndex != importIndex)
        {
            SortAPI.SetImportFilter(importValues[newImportIndex]);
        }
        // サブカテゴリ
        SortAPI.GetSubCategoryOptionsWithCounts(pkgs, out var subLabels, out var subValues);
        int subIndex = 0;
        string selectedSub = SortAPI.GetSelectedSubCategory();
        if (!string.IsNullOrEmpty(selectedSub))
        {
            for (int i = 0; i < subValues.Length; i++)
                if (subValues[i] == selectedSub) { subIndex = i; break; }
        }
        EditorGUILayout.BeginHorizontal();
        int newSubIndex = EditorGUILayout.Popup(new GUIContent("サブカテゴリ"), subIndex, subLabels, GUILayout.MinWidth(minW));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        if (newSubIndex != subIndex)
        {
            SortAPI.SetSelectedSubCategory(subValues[newSubIndex]);
        }

        // エクストラカテゴリ
        SortAPI.GetExtraCategoryOptionsWithCounts(pkgs, SortAPI.GetSelectedSubCategory(), out var exLabels, out var exValues);
        int exIndex = 0;
        string selectedEx = SortAPI.GetSelectedExtraCategory();
        if (!string.IsNullOrEmpty(selectedEx))
        {
            for (int i = 0; i < exValues.Length; i++)
                if (exValues[i] == selectedEx) { exIndex = i; break; }
        }
        EditorGUILayout.BeginHorizontal();
        int newExIndex = EditorGUILayout.Popup(new GUIContent("エクストラ（指定なし可）"), exIndex, exLabels, GUILayout.MinWidth(minW));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        if (newExIndex != exIndex)
        {
            SortAPI.SetSelectedExtraCategory(exValues[newExIndex]);
        }

        // 以降、不要な重複は削除済み（インポート状態は最上段で表示）
        GUILayout.EndVertical();
    }
}



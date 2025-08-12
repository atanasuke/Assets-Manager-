// File: Assets/Editor/DebugWindow.cs
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

public class DebugWindow : EditorWindow
{
    private Vector2 _scroll;

    public static void ShowWindow()
    {
        var wnd = GetWindow<DebugWindow>("Package Debug");
        wnd.minSize = new Vector2(500, 300);
        wnd.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Debug Information", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // Main window info
        var main = Resources.FindObjectsOfTypeAll<MainMenu>();
        if (main.Length > 0)
        {
            var m = main[0];
            EditorGUILayout.LabelField("Main Window", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Position", m.position.ToString());
            EditorGUILayout.LabelField("Sort Panel Open", SortUI.IsOpen().ToString());
            float sortWidth = SortUI.IsOpen() ? Mathf.Clamp(m.position.width * 0.25f, m.position.width * 0.18f, m.position.width * 0.45f) : 0f;
            EditorGUILayout.LabelField("Sort Panel Width(calc)", sortWidth.ToString("F2"));
            float infoWidth = m.position.width * 0.35f;
            float packageWidth = Mathf.Max(260f, m.position.width - sortWidth - infoWidth);
            EditorGUILayout.LabelField("Info Panel Width(calc)", infoWidth.ToString("F2"));
            EditorGUILayout.LabelField("Package Panel Width(calc)", packageWidth.ToString("F2"));
            EditorGUILayout.Space();
        }

        // Current sort/filter
        EditorGUILayout.LabelField("Sort/Filter", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("SortKey", SortAPI.GetCurrent().ToString());
        EditorGUILayout.LabelField("ImportFilter", SortAPI.GetImportFilter().ToString());
        EditorGUILayout.LabelField("SubCategory", SortAPI.GetSelectedSubCategory() ?? "(null)");
        EditorGUILayout.LabelField("ExtraCategory", SortAPI.GetSelectedExtraCategory() ?? "(null)");
        EditorGUILayout.Space();

        // Package list shown in PackageUI after filters
        var all = PackageAPI.GetPackages();
        var byImport = SortAPI.ApplyImportStateFilter(all, null);
        var afterImport = PackageAPI.GetPackages(byImport);
        var subExtra = SortAPI.GetFilteredIndices(afterImport);
        List<PackageAPIEntry> finalList;
        if (SortAPI.HasActiveCategoryFilter())
        {
            finalList = PackageAPI.GetPackages(subExtra);
        }
        else
        {
            finalList = afterImport;
        }
        var orderedIdx = SortAPI.GetSortedIndices(finalList);
        if (orderedIdx != null && orderedIdx.Count == finalList.Count)
        {
            var map = new Dictionary<int, PackageAPIEntry>();
            foreach (var p in finalList) map[p.Index] = p;
            var reordered = new List<PackageAPIEntry>(finalList.Count);
            foreach (var i in orderedIdx) if (map.TryGetValue(i, out var e)) reordered.Add(e);
            finalList = reordered;
        }

        EditorGUILayout.LabelField("PackageUI Entries", EditorStyles.boldLabel);
        foreach (var e in finalList)
        {
            EditorGUILayout.LabelField($"- #{e.Index} {e.FolderName} | Imported={ImportStatusAPI.IsImported(e.FolderPath)} | Meta={(string.IsNullOrEmpty(e.MetaPath) ? "Missing" : "OK")}");
        }

        // Selected index
        EditorGUILayout.Space();
        int selected = PackageAPI.GetSelectedIndex();
        EditorGUILayout.LabelField("Selected Index", selected.ToString());

        EditorGUILayout.EndScrollView();
    }
}



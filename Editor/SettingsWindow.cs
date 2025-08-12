// File: Assets/Editor/SettingsWindow.cs
using UnityEngine;
using UnityEditor;

public class SettingsWindow : EditorWindow
{
    private string _rootFolder;
    private bool   _importOnClick;

    private int _packagesize;

    /// <summary>
    /// 他スクリプトから呼べるエントリポイント
    /// </summary>
    public static void ShowSettings()
    {
        var wnd = GetWindow<SettingsWindow>("Package Import Settings");
        wnd.minSize = new Vector2(400, 120);
        wnd.LoadCurrentSettings();
        wnd.Show();
    }

    private void LoadCurrentSettings()
    {
        // SettingsAPI から最新の値を読み込む
        _rootFolder = SettingsAPI.GetRootFolder();
        _importOnClick = SettingsAPI.GetImportOnClick();
        _packagesize = SettingsAPI.GetPackageSize();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Package Importer Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // ルートフォルダ
        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Root Folder:", GUILayout.Width(100));
            _rootFolder = EditorGUILayout.TextField(_rootFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var sel = EditorUtility.OpenFolderPanel(
                    "Select Package Root",
                    _rootFolder,
                    ""
                );
                if (!string.IsNullOrEmpty(sel))
                    _rootFolder = sel;
            }
        EditorGUILayout.EndHorizontal();

        // クリックで即インポート
        _importOnClick = EditorGUILayout.Toggle(
            new GUIContent("Import On Click", "サムネイルをクリックした時に即インポートするか"),
            _importOnClick
        );

        _packagesize = EditorGUILayout.IntSlider(
            new GUIContent("パッケージのサイズ", "どれくらいのサイズでパッケージを表示するか"),
            _packagesize,
            1,
            16

        );

        EditorGUILayout.Space();

        // 保存ボタン
        if (GUILayout.Button("Save Settings", GUILayout.Height(28)))
        {
            SettingsAPI.SetRootFolder(_rootFolder);
            SettingsAPI.SetImportOnClick(_importOnClick);
            SettingsAPI.SetPackageSize(_packagesize);
            Close();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("デバッグ画面を開く", GUILayout.Height(24)))
        {
            DebugWindow.ShowWindow();
        }
    }
}

using UnityEditor;
using UnityEngine;

public class LinkLabelExample : EditorWindow
{
    [MenuItem("Window/Link Label Example")]
    public static void ShowWindow()
    {
        GetWindow<LinkLabelExample>("Link Label Example");
    }

    private void OnGUI()
    {
        GUILayout.Space(20);

        // スタイル設定（中央寄せ＋青文字＋下線）
        GUIStyle linkStyle = new GUIStyle(EditorStyles.label);
        linkStyle.normal.textColor = new Color(0.2f, 0.4f, 1f); // 青
        linkStyle.hover.textColor = Color.cyan;
        linkStyle.alignment = TextAnchor.MiddleCenter;
        linkStyle.fontStyle = FontStyle.Bold;
        
        string url = "https://unity.com/";

        // リンク風ラベルを描画
        Rect labelRect = GUILayoutUtility.GetRect(new GUIContent(url), linkStyle);
        EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

        GUI.Label(labelRect, url, linkStyle);

        // クリック検出
        if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
        {
            // クリップボードにコピー
            EditorGUIUtility.systemCopyBuffer = url;

            // ブラウザで開く
            Application.OpenURL(url);

            // イベント消費
            Event.current.Use();
        }
    }
}

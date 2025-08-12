// File: Assets/Editor/LinkLabelUtility.cs
using UnityEditor;
using UnityEngine;

public static class LinkLabelUtility
{
    /// <summary>
    /// クリック可能なリンクラベルを描画します。
    /// アンダーラインは通常時のみ表示し、ホバー中は消えます。
    /// クリックされると URL をブラウザで開き、クリップボードにコピーします。
    /// </summary>
    /// <param name="text">表示するテキスト</param>
    /// <param name="url">クリック時に開く URL</param>
    /// <param name="width">ラベル幅。省略時は自動</param>
    /// <param name="height">ラベル高さ。省略時は自動</param>
    /// <returns>クリックされたら true</returns>
    public static bool DrawLink(string text, string url, float width = 0, float height = 0)
    {
        // スタイルを作成
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = new Color(0.2f, 0.4f, 1f);
        style.hover.textColor = Color.cyan;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        // サイズ指定があればオプションに追加
        GUILayoutOption[] options = width > 0 || height > 0
            ? new GUILayoutOption[] {
                width  > 0 ? GUILayout.Width(width)   : GUILayout.ExpandWidth(true),
                height > 0 ? GUILayout.Height(height) : GUILayout.ExpandHeight(false)
              }
            : new GUILayoutOption[0];

        // テキストの GUIContent
        GUIContent content = new GUIContent(text);

        // ラベルの描画領域を取得
        Rect rect = GUILayoutUtility.GetRect(content, style, options);

        // リンク時のカーソル
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        // 実際のラベル描画
        GUI.Label(rect, content, style);

        // ホバー判定
        bool isHover = rect.Contains(Event.current.mousePosition);

        // アンダーラインを描画（ホバー中は消す）
        if (!isHover)
        {
            var underlineY = rect.yMax - 1;
            var lineRect = new Rect(rect.x, underlineY, rect.width, 1);
            EditorGUI.DrawRect(lineRect, style.normal.textColor);
        }

        // クリック検出
        if (Event.current.type == EventType.MouseDown && isHover)
        {
            // クリップボードにコピー
            EditorGUIUtility.systemCopyBuffer = url;
            // ブラウザで開く
            Application.OpenURL(url);
            Event.current.Use();
            return true;
        }

        return false;
    }
}

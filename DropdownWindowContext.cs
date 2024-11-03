using UnityEngine;
using UnityEditor;

public class DropdownWindowContext : PopupWindowContent
{
    string[] stringList;

    public DropdownWindowContext(string[] stringList)
    {
        this.stringList = stringList;
    }

    public override Vector2 GetWindowSize()
    {
        var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return new Vector2(200, this.stringList.Length * lineHeight);
    }

    public override void OnGUI(Rect rect)
    {
        if (stringList != null)
        {
            foreach (string str in stringList)
            {
                EditorGUILayout.LabelField(str);
            }
        }
    }

    public override void OnOpen()
    {
    }

    public override void OnClose()
    {
    }
}
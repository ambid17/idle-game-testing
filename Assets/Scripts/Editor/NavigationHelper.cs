using UnityEditor;
using UnityEngine;

public class NavigationHelper : MonoBehaviour
{
    [MenuItem("Caos Creations/Upgrade Settings _F2")]
    public static void SelectScriptableObjectFolder()
    {
        string folderPath = "Assets//ScriptableObjects/Upgrades/UpgradeDatabase.asset";
        var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(folderPath);
        EditorGUIUtility.PingObject(obj);
    }

    [MenuItem("Caos Creations/Open Save Folder _F5")]
    public static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}


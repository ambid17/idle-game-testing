using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EditorUtils
{
    // One-shot bulk setup for UI.UIButtonSound: adds it to every Selectable (except Scrollbars) in
    // the UI prefabs and the open scenes that doesn't already have one. Safe to re-run.
    // Selectables that belong to a nested prefab instance are skipped - they get the component
    // from their own prefab asset instead, which avoids duplicate components via overrides.
    public static class UIButtonSoundTools
    {
        private const string PrefabFolder = "Assets/Prefabs";

        [MenuItem("Tools/Audio/Add UIButtonSound To All Selectables")]
        public static void AddToAllSelectables()
        {
            int prefabCount = 0;
            int added = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                int addedHere = AddToSelectablesUnder(root.transform);
                if (addedHere > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabCount++;
                    added += addedHere;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                int addedHere = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    addedHere += AddToSelectablesUnder(go.transform);
                }
                if (addedHere > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    added += addedHere;
                }
            }

            Debug.Log($"UIButtonSoundTools: added {added} UIButtonSound component(s) across {prefabCount} prefab(s) and the open scene(s). Save the scene to keep the scene changes.");
        }

        private static int AddToSelectablesUnder(Transform root)
        {
            int added = 0;
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable is Scrollbar) continue;
                if (selectable.GetComponent<UIButtonSound>() != null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(selectable.gameObject)) continue;

                Undo.AddComponent<UIButtonSound>(selectable.gameObject);
                added++;
            }
            return added;
        }
    }
}

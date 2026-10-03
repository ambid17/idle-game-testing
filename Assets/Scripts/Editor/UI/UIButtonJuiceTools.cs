using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EditorUtils
{
    // One-shot bulk setup for UI.UIButtonJuice, same rules as UIButtonSoundTools: adds it to every
    // Button in the UI prefabs and the open scenes that doesn't already have one. Safe to re-run.
    // Buttons inside a nested prefab instance are skipped - they get it from their own prefab.
    public static class UIButtonJuiceTools
    {
        private const string PrefabFolder = "Assets/Prefabs";

        [MenuItem("Tools/UI/Add Button Juice To All Buttons")]
        public static void AddToAllButtons()
        {
            int prefabCount = 0;
            int added = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                int addedHere = AddToButtonsUnder(root.transform);
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
                    addedHere += AddToButtonsUnder(go.transform);
                }
                if (addedHere > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    added += addedHere;
                }
            }

            Debug.Log($"UIButtonJuiceTools: added {added} UIButtonJuice component(s) across {prefabCount} prefab(s) and the open scene(s). Save the scene to keep the scene changes.");
        }

        private static int AddToButtonsUnder(Transform root)
        {
            int added = 0;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<UIButtonJuice>() != null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(button.gameObject)) continue;

                Undo.AddComponent<UIButtonJuice>(button.gameObject);
                added++;
            }
            return added;
        }
    }
}

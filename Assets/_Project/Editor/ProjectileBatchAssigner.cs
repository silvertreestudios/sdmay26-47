using TacticsGame.Actions;
using TacticsGame.Combat;
using UnityEditor;
using UnityEngine;

namespace TacticsGame.EditorTools
{
    /// <summary>
    /// A utility window to quickly assign projectiles to RangedActions.
    /// </summary>
    public class ProjectileBatchAssigner : EditorWindow
    {
        private const string DEFAULT_PROJECTILE_PATH =
            "Assets/_Project/Prefabs/Dont ask/arrow_A.prefab";

        [MenuItem("Tactics/Batch Assign Projectiles")]
        public static void AssignProjectiles()
        {
            // Load the Arrow Prefab
            GameObject arrowPrefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(
                DEFAULT_PROJECTILE_PATH
            );

            if (arrowPrefabGO == null)
            {
                Debug.LogError(
                    $"[Batch Assign] Failed! Could not find prefab at: {DEFAULT_PROJECTILE_PATH}"
                );
                EditorUtility.DisplayDialog(
                    "Error",
                    $"Could not find prefab at {DEFAULT_PROJECTILE_PATH}. Check the path!",
                    "OK"
                );
                return;
            }

            // Ensure the prefab actually has the Projectile component
            Projectile projectileComp = arrowPrefabGO.GetComponent<Projectile>();
            if (projectileComp == null)
            {
                projectileComp = arrowPrefabGO.AddComponent<Projectile>();
                EditorUtility.SetDirty(arrowPrefabGO);
                Debug.Log("[Batch Assign] Added missing Projectile script to the arrow prefab.");
            }

            // Find all RangedAction instances in the project (Prefabs and Scene objects)
            // Note: FindObjectsOfTypeAll includes assets (prefabs) if they are loaded.
            RangedAction[] allRangedActions = Resources.FindObjectsOfTypeAll<RangedAction>();
            int assignedCount = 0;

            foreach (var action in allRangedActions)
            {
                // Use SerializedObject to ensure the change is recorded by Unity's undo/save system
                SerializedObject so = new SerializedObject(action);
                SerializedProperty prop = so.FindProperty("projectilePrefab");

                if (prop != null)
                {
                    prop.objectReferenceValue = projectileComp;
                    so.ApplyModifiedProperties();

                    // Mark dirty so Unity knows to save the prefab/scene change
                    EditorUtility.SetDirty(action);
                    assignedCount++;
                }
            }

            // Finalize
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Batch Assign] Successfully assigned the projectile to {assignedCount} RangedAction components."
            );
            EditorUtility.DisplayDialog(
                "Success",
                $"Assigned projectile to {assignedCount} units/prefabs!",
                "Awesome"
            );
        }
    }
}

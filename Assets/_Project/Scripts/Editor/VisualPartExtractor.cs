#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TacticsGame.Characters.Visuals;
using UnityEditor;
using UnityEngine;

namespace TacticsGame.Editor
{
    public class VisualPartExtractor : EditorWindow
    {
        private string outputFolderPath = "Assets/_Project/Data/VisualParts";
        private Material defaultMaterial;

        [MenuItem("Tactics Core/Extract Visual Parts from FBX")]
        public static void ShowWindow()
        {
            GetWindow<VisualPartExtractor>("Visual Extractor");
        }

        private void OnGUI()
        {
            GUILayout.Label("Extract VisualPartSOs from selected FBXs", EditorStyles.boldLabel);
            GUILayout.Label(
                "1. Select one or more FBX files in the Project window.\n2. Click Extract.",
                EditorStyles.wordWrappedLabel
            );
            GUILayout.Space(10);

            outputFolderPath = EditorGUILayout.TextField("Output Folder", outputFolderPath);
            defaultMaterial = (Material)
                EditorGUILayout.ObjectField(
                    "Default Material",
                    defaultMaterial,
                    typeof(Material),
                    false
                );

            GUILayout.Space(10);
            if (GUILayout.Button("Extract Selected FBX(s)", GUILayout.Height(30)))
            {
                ExtractFromSelected();
            }
        }

        private void ExtractFromSelected()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
            {
                Debug.LogWarning(
                    "No objects selected. Please select an FBX file in the Project window."
                );
                return;
            }

            if (!Directory.Exists(outputFolderPath))
            {
                Directory.CreateDirectory(outputFolderPath);
                AssetDatabase.Refresh();
            }

            int extractedCount = 0;

            foreach (var obj in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (assetPath.ToLower().EndsWith(".fbx"))
                {
                    extractedCount += ExtractMeshesFromFBX(assetPath);
                }
            }

            if (extractedCount > 0)
            {
                Debug.Log(
                    $"<color=green>Successfully extracted {extractedCount} Visual Parts!</color>"
                );
            }
        }

        private int ExtractMeshesFromFBX(string fbxPath)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            GameObject rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
            Material autoMaterial = FindMaterialForFBX(fbxPath);
            Material finalMaterial = autoMaterial != null ? autoMaterial : defaultMaterial;
            HashSet<string> processedAssetNames = new HashSet<string>();
            int count = 0;

            foreach (var asset in allAssets)
            {
                if (asset is Mesh mesh)
                {
                    count += ExtractMeshCandidate(
                        fbxName,
                        mesh.name,
                        mesh,
                        finalMaterial,
                        processedAssetNames
                    );
                }
            }

            if (rootPrefab != null)
            {
                foreach (var renderer in rootPrefab.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = GetRendererMesh(renderer);
                    if (mesh == null)
                        continue;

                    count += ExtractMeshCandidate(
                        fbxName,
                        renderer.gameObject.name,
                        mesh,
                        finalMaterial,
                        processedAssetNames
                    );
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return count;
        }

        private int ExtractMeshCandidate(
            string fbxName,
            string sourceName,
            Mesh mesh,
            Material finalMaterial,
            HashSet<string> processedAssetNames
        )
        {
            if (mesh == null)
                return 0;

            string nameForSlot = string.IsNullOrEmpty(sourceName) ? mesh.name : sourceName;

            // Skip actual rig/root meshes without catching the "Rig" inside "Right".
            if (IsRigOrRootMesh(nameForSlot, mesh.name, fbxName))
            {
                Debug.Log(
                    $"[Visual Extractor] Skipped '{nameForSlot}' from '{fbxName}' because it looks like a rig/root mesh."
                );
                return 0;
            }

            string assetName = $"{fbxName}_{nameForSlot}";
            if (!processedAssetNames.Add(assetName))
                return 0;

            string savePath = $"{outputFolderPath}/{assetName}.asset";
            VisualSlot guessedSlot = GuessSlotFromName(nameForSlot);

            // Skip if it already exists so we don't overwrite user tweaks.
            if (File.Exists(savePath))
            {
                VisualPartSO existingPart = AssetDatabase.LoadAssetAtPath<VisualPartSO>(savePath);
                if (existingPart != null && existingPart.Slot != guessedSlot)
                {
                    Debug.Log(
                        $"[Visual Extractor] Existing visual part '{assetName}' remains unchanged. Guessed slot is {guessedSlot}, existing slot is {existingPart.Slot}."
                    );
                }
                else
                {
                    Debug.Log($"[Visual Extractor] Skipped existing visual part '{assetName}'.");
                }
                return 0;
            }

            VisualPartSO part = ScriptableObject.CreateInstance<VisualPartSO>();
            part.PartID = assetName.ToLower();
            part.DisplayName = nameForSlot.Replace("_", " ");
            part.SharedMesh = mesh;
            part.Slot = guessedSlot;

            if (finalMaterial != null)
            {
                part.Materials = new Material[] { finalMaterial };
            }

            Debug.Log(
                $"[Visual Extractor] Extracting '{nameForSlot}' from '{fbxName}' as slot {part.Slot}."
            );

            // Guess if it's a static attachment based on KayKit naming conventions.
            if (
                part.Slot == VisualSlot.Helmet
                || nameForSlot.ToLower().Contains("hat")
                || nameForSlot.ToLower().Contains("helmet")
                || nameForSlot.ToLower().Contains("hood")
            )
            {
                part.IsStaticMesh = true;
            }

            AssetDatabase.CreateAsset(part, savePath);
            return 1;
        }

        private Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                return skinnedMeshRenderer.sharedMesh;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private bool IsRigOrRootMesh(string sourceName, string meshName, string fbxName)
        {
            if (meshName == fbxName || sourceName == fbxName)
                return true;

            string[] sourceTokens = TokenizeName(sourceName);
            string[] meshTokens = TokenizeName(meshName);
            return sourceTokens.Contains("rig") || meshTokens.Contains("rig");
        }

        private Material FindMaterialForFBX(string fbxPath)
        {
            string dir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

            // Try prefix matching (e.g., Barbarian_Barbarian -> barbarian)
            string prefix = fbxName.Split('_')[0].ToLower();

            // Potential naming patterns to check
            string[] searchNames = new string[]
            {
                prefix, // e.g. "barbarian"
                $"{prefix}_texture", // e.g. "barbarian_texture"
                $"{prefix}_mat" // e.g. "barbarian_mat"
            };

            foreach (string expectedMatName in searchNames)
            {
                // Search for the material in the same folder first
                string[] matGuids = AssetDatabase.FindAssets(
                    $"{expectedMatName} t:Material",
                    new[] { dir }
                );
                if (matGuids.Length > 0)
                {
                    return AssetDatabase.LoadAssetAtPath<Material>(
                        AssetDatabase.GUIDToAssetPath(matGuids[0])
                    );
                }

                // If not found in the same folder, search the whole project
                matGuids = AssetDatabase.FindAssets($"{expectedMatName} t:Material");
                if (matGuids.Length > 0)
                {
                    return AssetDatabase.LoadAssetAtPath<Material>(
                        AssetDatabase.GUIDToAssetPath(matGuids[0])
                    );
                }
            }

            Debug.LogError(
                $"[Visual Extractor] ERROR: Could not find any material matching '{prefix}' for FBX '{fbxName}'. Please assign manually or rename your material to match the FBX prefix."
            );
            return null;
        }

        private VisualSlot GuessSlotFromName(string name)
        {
            string lower = name.ToLower();
            string compact = NormalizeForSlotMatch(name);
            string[] tokens = TokenizeName(name);

            if (lower.Contains("head"))
                return VisualSlot.Head;
            if (lower.Contains("body") || lower.Contains("torso") || lower.Contains("chest"))
                return VisualSlot.Body;
            if (LooksLikeDirectionalPart(compact, tokens, "arm", "left", "l"))
                return VisualSlot.ArmLeft;
            if (LooksLikeDirectionalPart(compact, tokens, "arm", "right", "r"))
                return VisualSlot.ArmRight;
            if (LooksLikeDirectionalPart(compact, tokens, "leg", "left", "l"))
                return VisualSlot.LegLeft;
            if (LooksLikeDirectionalPart(compact, tokens, "leg", "right", "r"))
                return VisualSlot.LegRight;
            if (lower.Contains("cape") || lower.Contains("cloak"))
                return VisualSlot.Cape;
            if (lower.Contains("helmet") || lower.Contains("hat") || lower.Contains("hood"))
                return VisualSlot.Helmet;
            if (lower.Contains("eye"))
                return VisualSlot.Eyes;
            if (lower.Contains("jaw"))
                return VisualSlot.Jaw;
            if (lower.Contains("mask"))
                return VisualSlot.Mask;

            return VisualSlot.Body; // default fallback
        }

        private string NormalizeForSlotMatch(string name)
        {
            return Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", string.Empty);
        }

        private string[] TokenizeName(string name)
        {
            string spacedCamelCase = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
            return Regex
                .Split(spacedCamelCase.ToLowerInvariant(), @"[^a-z0-9]+")
                .Where(token => !string.IsNullOrEmpty(token))
                .ToArray();
        }

        private bool LooksLikeDirectionalPart(
            string compact,
            string[] tokens,
            string part,
            string side,
            string sideInitial
        )
        {
            if (!compact.Contains(part))
                return false;

            if (compact.Contains($"{side}{part}") || compact.Contains($"{part}{side}"))
                return true;

            if (
                compact.Contains($"{sideInitial}{part}") || compact.Contains($"{part}{sideInitial}")
            )
                return true;

            bool hasPartToken = tokens.Contains(part);
            bool hasSideToken = tokens.Contains(side) || tokens.Contains(sideInitial);
            return hasPartToken && hasSideToken;
        }
    }
}
#endif

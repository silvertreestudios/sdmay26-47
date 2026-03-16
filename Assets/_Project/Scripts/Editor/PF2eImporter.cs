#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using PathfinderTactics.Data.PF2e;
using ActionCost = PathfinderTactics.Data.PF2e.ActionCost;
using PathfinderTactics.Combat;
using PathfinderTactics.Characters;

// AI generated import tool for parsing through the Pathfinder 2e foundry vtt JSON files.
// TODO: Maybe find a way to import them into the game.
namespace PathfinderTactics.Editor
{
    public class PF2eImporter : EditorWindow
    {
        // Update this path if the repo moves
        private string packsDirectory =
            "C:/Users/owais/OneDrive/Desktop/Unity Projects/PathfinderTactics/pf2e-13-dev/packs/pf2e";
        private string outputDirectory = "Assets/GameData/PF2e/Spells";
        private string databasePath = "Assets/GameData/PF2e/PF2eDatabase.asset";

        [MenuItem("PF2e/Import Compendium Scanner")]
        public static void ShowWindow()
        {
            GetWindow<PF2eImporter>("PF2e Compendium Importer");
        }

        private void OnGUI()
        {
            GUILayout.Label("PF2e Content Importer", EditorStyles.boldLabel);
            packsDirectory = EditorGUILayout.TextField("PF2e Repo Packs Path:", packsDirectory);
            databasePath = EditorGUILayout.TextField("Database Asset Path:", databasePath);

            GUILayout.Space(10);
            GUILayout.Label("Import Content", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Import Spells"))
                ImportSpells();
            if (GUILayout.Button("2. Import Actions"))
                ImportActions();
            if (GUILayout.Button("3. Import Classes"))
                ImportClasses();
            if (GUILayout.Button("4. Import Ancestries"))
                ImportAncestries();
            if (GUILayout.Button("5. Import Backgrounds"))
                ImportBackgrounds();

            GUILayout.Space(10);
            if (GUILayout.Button("Import ALL"))
            {
                ImportSpells();
                ImportActions();
                ImportClasses();
                ImportAncestries();
                ImportBackgrounds();
            }
        }

        private void ImportSpells()
        {
            string spellDir = Path.Combine(packsDirectory, "spells/spells");
            if (!Directory.Exists(spellDir))
            {
                Debug.LogError($"[Importer] Spell directory not found: {spellDir}");
                return;
            }

            string[] files = Directory.GetFiles(spellDir, "*.json", SearchOption.AllDirectories);
            string outputDir = "Assets/GameData/PF2e/Spells";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // Load or Create master database
            PF2eDatabase db = LoadOrCreateDatabase();

            db.AllSpells.Clear(); // Ensure we don't duplicate on re-imports

            AssetDatabase.StartAssetEditing();
            int count = 0;

            try
            {
                foreach (var file in files)
                {
                    if (file.EndsWith("_folders.json"))
                        continue; // Skip metadata files

                    try
                    {
                        string json = File.ReadAllText(file);
                        JObject data = JObject.Parse(json);

                        string id = data["_id"]?.ToString();
                        string name = data["name"]?.ToString();
                        string type = data["type"]?.ToString();

                        if (type != "spell" || string.IsNullOrEmpty(id))
                            continue;

                        string safeName = SanitizeFileName(name);
                        string assetPath = $"{outputDirectory}/{safeName}_{id}.asset";

                        SpellSO spellSO = AssetDatabase.LoadAssetAtPath<SpellSO>(assetPath);
                        bool isNew = false;

                        if (spellSO == null)
                        {
                            spellSO = ScriptableObject.CreateInstance<SpellSO>();
                            isNew = true;
                        }

                        // --- Parse Core Data ---
                        spellSO.Id = id;
                        spellSO.ElementName = name;

                        var system = data["system"] as JObject;
                        if (system == null)
                            continue;

                        var slugToken = system["slug"];
                        spellSO.Slug =
                            (slugToken != null && slugToken.Type == JTokenType.String)
                                ? slugToken.ToString()
                                : SanitizeFileName(name).ToLower().Replace("_", "-");

                        var levelObj = system["level"] as JObject;
                        spellSO.Level = levelObj != null ? ParseInt(levelObj["value"]) : 0;

                        var descObj = system["description"] as JObject;
                        spellSO.Description =
                            descObj != null
                                ? ConvertHtmlToTMP(descObj["value"]?.ToString() ?? "")
                                : "";

                        // Extract Traits & Traditions
                        var traitsObj = system["traits"] as JObject;
                        if (traitsObj != null)
                        {
                            spellSO.Traits = ExtractStringArray(traitsObj["value"]);
                            spellSO.Traditions = ExtractStringArray(traitsObj["traditions"]);
                        }
                        else
                        {
                            spellSO.Traits = new List<string>();
                            spellSO.Traditions = new List<string>();
                        }

                        // Extract SourceBook
                        var pubObj = system["publication"] as JObject;
                        spellSO.SourceBook =
                            pubObj != null ? pubObj["title"]?.ToString() ?? "" : "";

                        // Extract Action Cost
                        var timeObj = system["time"] as JObject;
                        string costStr =
                            timeObj != null ? timeObj["value"]?.ToString()?.ToLower() ?? "" : "";
                        spellSO.Cost = ParseActionCost(costStr);

                        // Extract Range
                        var rangeObj = system["range"] as JObject;
                        string rangeStr =
                            rangeObj != null ? rangeObj["value"]?.ToString()?.ToLower() ?? "" : "";
                        spellSO.Range = ParseRange(rangeStr);

                        // Extract Duration and Sustain
                        var durObj = system["duration"] as JObject;
                        if (durObj != null)
                        {
                            spellSO.Duration = durObj["value"]?.ToString() ?? "";
                            var sustainTok = durObj["sustained"];
                            spellSO.IsSustained =
                                sustainTok != null
                                && sustainTok.Type == JTokenType.Boolean
                                && sustainTok.ToObject<bool>();
                        }

                        // Extract Area
                        var areaObj = system["area"] as JObject;
                        if (areaObj != null)
                        {
                            spellSO.Area = new AreaDefinition
                            {
                                Shape = ParseAreaShape(areaObj["type"]?.ToString()),
                                Radius = ParseInt(areaObj["value"]),
                            };
                        }
                        else
                        {
                            spellSO.Area = new AreaDefinition();
                        }

                        // Extract Target
                        var targetObj = system["target"] as JObject;
                        string targetStr =
                            targetObj != null
                                ? targetObj["value"]?.ToString()?.ToLower() ?? ""
                                : "";
                        if (spellSO.Area.Shape != AreaShape.None)
                            spellSO.Target = TargetType.Area;
                        else if (
                            targetStr.Contains("creature")
                            || targetStr.Contains("enemy")
                            || targetStr.Contains("ally")
                            || targetStr.Contains("allies")
                            || targetStr.Contains("enemies")
                        )
                        {
                            if (targetStr.Contains("ally") || targetStr.Contains("allies"))
                                spellSO.Target = TargetType.Ally;
                            else if (targetStr.Contains("enemy") || targetStr.Contains("enemies"))
                                spellSO.Target = TargetType.Enemy;
                            else
                                spellSO.Target = TargetType.Creature;
                        }
                        else if (targetStr.Contains("self"))
                            spellSO.Target = TargetType.Self;
                        else if (targetStr.Contains("object"))
                            spellSO.Target = TargetType.Object;
                        else
                            spellSO.Target = TargetType.Tile; // Default to tile instead of Self if it's complex

                        // Extract Damage Formula
                        var damageObj = system["damage"] as JObject;
                        if (damageObj != null && damageObj.HasValues)
                        {
                            string totalFormula = "";
                            string primaryElement = "";

                            foreach (var prop in damageObj.Properties())
                            {
                                string f = prop.Value["formula"]?.ToString() ?? "";
                                string t = prop.Value["type"]?.ToString() ?? "";

                                if (string.IsNullOrEmpty(totalFormula))
                                    totalFormula = f;
                                else if (!string.IsNullOrEmpty(f))
                                    totalFormula += $" + {f}";

                                if (string.IsNullOrEmpty(primaryElement))
                                    primaryElement = t;
                            }

                            spellSO.BaseDamage = ParseDiceFormula(totalFormula);
                            spellSO.ElementType = ParseDamageType(primaryElement);
                        }

                        // Extract Saving Throw
                        var defenseObj = system["defense"] as JObject;
                        if (defenseObj != null)
                        {
                            var saveInfo = defenseObj["save"] as JObject;
                            if (saveInfo != null)
                            {
                                var basicToken = saveInfo["basic"];
                                spellSO.IsBasicSave =
                                    (basicToken != null && basicToken.Type == JTokenType.Boolean)
                                        ? basicToken.ToObject<bool>()
                                        : false;

                                var statToken = saveInfo["statistic"];
                                spellSO.SaveType =
                                    statToken != null
                                        ? ParseSavingThrow(statToken.ToString())
                                        : SavingThrowType.None;
                            }
                        }

                        // Extract Heightening
                        var heightenObj = system["heightening"] as JObject;
                        if (heightenObj != null && heightenObj.HasValues)
                        {
                            var typeToken = heightenObj["type"];
                            if (typeToken != null && typeToken.ToString() == "interval")
                            {
                                int interval = ParseInt(heightenObj["interval"]);
                                spellSO.HeightenRules = "+" + interval;

                                var hDmgObj = heightenObj["damage"] as JObject;
                                if (hDmgObj != null && hDmgObj.HasValues)
                                {
                                    var hDmg = hDmgObj.Properties().FirstOrDefault()?.Value;
                                    if (hDmg != null)
                                    {
                                        string hFormula =
                                            hDmg.Type == JTokenType.String
                                                ? hDmg.ToString()
                                                : hDmg["formula"]?.ToString() ?? "";
                                        spellSO.HeightenDamageScaling = ParseDiceFormula(hFormula);
                                    }
                                }
                            }
                        }

                        // Extract Icon
                        string imgPath = data["img"]?.ToString() ?? "";
                        if (imgPath.StartsWith("systems/pf2e/icons"))
                        {
                            string subFolder = imgPath.Substring("systems/pf2e/icons/".Length);
                            string sourceImgPath = Path.GetFullPath(
                                Path.Combine(packsDirectory, "../../static/icons", subFolder)
                            );

                            string destImgDir = "Assets/GameData/PF2e/Icons";
                            if (!Directory.Exists(destImgDir))
                                Directory.CreateDirectory(destImgDir);

                            string destImgPath = Path.Combine(
                                    destImgDir,
                                    Path.GetFileName(sourceImgPath)
                                )
                                .Replace("\\", "/");

                            if (File.Exists(sourceImgPath))
                            {
                                if (!File.Exists(destImgPath))
                                {
                                    File.Copy(sourceImgPath, destImgPath);
                                    AssetDatabase.ImportAsset(
                                        destImgPath,
                                        ImportAssetOptions.ForceUpdate
                                    );
                                }

                                spellSO.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(destImgPath);
                                if (spellSO.Icon == null)
                                {
                                    // Force TextureImporter to Sprite
                                    TextureImporter importer =
                                        AssetImporter.GetAtPath(destImgPath) as TextureImporter;
                                    if (
                                        importer != null
                                        && importer.textureType != TextureImporterType.Sprite
                                    )
                                    {
                                        importer.textureType = TextureImporterType.Sprite;
                                        importer.SaveAndReimport();
                                        spellSO.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(
                                            destImgPath
                                        );
                                    }
                                }
                            }
                        }

                        // AI Tags Auto-population
                        spellSO.AITags.Clear();
                        if (spellSO.BaseDamage.DiceCount > 0 || spellSO.Traits.Contains("damage"))
                            spellSO.AITags.Add("damage");
                        if (
                            spellSO.Traits.Contains("healing")
                            || spellSO.Traits.Contains("positive")
                            || spellSO.Description.ToLower().Contains("restore")
                        )
                            spellSO.AITags.Add("heal");
                        if (spellSO.Area.Shape != AreaShape.None)
                            spellSO.AITags.Add("aoe");
                        if (
                            spellSO.Description.ToLower().Contains("frightened")
                            || spellSO.Description.ToLower().Contains("slowed")
                        )
                            spellSO.AITags.Add("debuff");

                        spellSO.CompendiumSource = "pf2e.spells";
                        spellSO.BuildDerivedFields();

                        // --- Save Asset ---
                        if (isNew)
                        {
                            AssetDatabase.CreateAsset(spellSO, assetPath);
                            if (!db.AllSpells.Contains(spellSO))
                            {
                                db.AllSpells.Add(spellSO);
                            }
                        }
                        else
                        {
                            EditorUtility.SetDirty(spellSO);
                        }

                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(
                            $"[Importer] Failed to parse spell file {Path.GetFileName(file)}: {e.Message}"
                        );
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"<color=green>[Importer] Successfully compiled {count} PF2e spells into the PF2eDatabase!</color>"
            );
        }

        // =========================================================================
        // IMPORT ACTIONS
        // =========================================================================
        private void ImportActions()
        {
            string actionsDir = Path.Combine(packsDirectory, "actions");
            if (!Directory.Exists(actionsDir))
            {
                Debug.LogError($"[Importer] Actions dir not found: {actionsDir}");
                return;
            }

            string[] files = Directory.GetFiles(actionsDir, "*.json", SearchOption.AllDirectories);
            string outputDir = "Assets/GameData/PF2e/Actions";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            PF2eDatabase db = LoadOrCreateDatabase();
            db.AllActions.Clear();

            AssetDatabase.StartAssetEditing();
            int count = 0;
            try
            {
                foreach (var file in files)
                {
                    if (file.EndsWith("_folders.json"))
                        continue;
                    try
                    {
                        string json = File.ReadAllText(file);
                        JObject data = JObject.Parse(json);

                        string id = data["_id"]?.ToString();
                        string name = data["name"]?.ToString();
                        string type = data["type"]?.ToString();
                        if (type != "action" || string.IsNullOrEmpty(id))
                            continue;

                        string safeName = SanitizeFileName(name);
                        string assetPath = $"{outputDir}/{safeName}_{id}.asset";

                        ActionSO actionSO = AssetDatabase.LoadAssetAtPath<ActionSO>(assetPath);
                        bool isNew = actionSO == null;
                        if (isNew)
                            actionSO = ScriptableObject.CreateInstance<ActionSO>();

                        var system = data["system"] as JObject;
                        if (system == null)
                            continue;

                        actionSO.Id = id;
                        actionSO.ElementName = name;

                        var slugToken = system["slug"];
                        actionSO.Slug =
                            (slugToken != null && slugToken.Type == JTokenType.String)
                                ? slugToken.ToString()
                                : safeName.ToLower().Replace("_", "-");

                        var descObj = system["description"] as JObject;
                        actionSO.Description =
                            descObj != null
                                ? ConvertHtmlToTMP(descObj["value"]?.ToString() ?? "")
                                : "";

                        var pubObj = system["publication"] as JObject;
                        actionSO.SourceBook =
                            pubObj != null ? pubObj["title"]?.ToString() ?? "" : "";

                        var traitsObj = system["traits"] as JObject;
                        actionSO.Traits =
                            traitsObj != null
                                ? ExtractStringArray(traitsObj["value"])
                                : new List<string>();

                        var actTypeObj = system["actionType"] as JObject;
                        actionSO.ActionType =
                            actTypeObj != null ? actTypeObj["value"]?.ToString() ?? "" : "";

                        var actionsObj = system["actions"] as JObject;
                        actionSO.ActionCount =
                            actionsObj != null ? ParseInt(actionsObj["value"]) : 0;

                        actionSO.Category = system["category"]?.ToString() ?? "";

                        // Trigger, Requirements, Frequency
                        var triggerObj = system["trigger"] as JObject;
                        actionSO.Trigger =
                            triggerObj != null
                                ? ConvertHtmlToTMP(triggerObj["value"]?.ToString() ?? "")
                                : "";

                        var reqObj = system["requirements"] as JObject;
                        actionSO.Requirements =
                            reqObj != null
                                ? ConvertHtmlToTMP(reqObj["value"]?.ToString() ?? "")
                                : "";

                        var freqObj = system["frequency"] as JObject;
                        if (freqObj != null)
                        {
                            int freqMax = ParseInt(freqObj["max"]);
                            string freqPer = freqObj["per"]?.ToString() ?? "";
                            actionSO.Frequency = freqMax > 0 ? $"{freqMax}/{freqPer}" : "";
                        }

                        actionSO.CompendiumSource = "pf2e.actions";
                        actionSO.BuildDerivedFields();

                        if (isNew)
                        {
                            AssetDatabase.CreateAsset(actionSO, assetPath);
                            db.AllActions.Add(actionSO);
                        }
                        else
                        {
                            EditorUtility.SetDirty(actionSO);
                            if (!db.AllActions.Contains(actionSO))
                                db.AllActions.Add(actionSO);
                        }
                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(
                            $"[Importer] Failed to parse action {Path.GetFileName(file)}: {e.Message}"
                        );
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"<color=green>[Importer] Successfully compiled {count} PF2e actions!</color>"
            );
        }

        // =========================================================================
        // IMPORT CLASSES
        // =========================================================================
        private void ImportClasses()
        {
            string classDir = Path.Combine(packsDirectory, "classes");
            if (!Directory.Exists(classDir))
            {
                Debug.LogError($"[Importer] Classes dir not found: {classDir}");
                return;
            }

            string[] files = Directory.GetFiles(classDir, "*.json", SearchOption.AllDirectories);
            string outputDir = "Assets/GameData/PF2e/Classes";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            PF2eDatabase db = LoadOrCreateDatabase();
            db.AllClasses.Clear();

            AssetDatabase.StartAssetEditing();
            int count = 0;
            try
            {
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        JObject data = JObject.Parse(json);

                        string id = data["_id"]?.ToString();
                        string name = data["name"]?.ToString();
                        string type = data["type"]?.ToString();
                        if (type != "class" || string.IsNullOrEmpty(id))
                            continue;

                        string safeName = SanitizeFileName(name);
                        string assetPath = $"{outputDir}/{safeName}_{id}.asset";

                        ClassSO classSO = AssetDatabase.LoadAssetAtPath<ClassSO>(assetPath);
                        bool isNew = classSO == null;
                        if (isNew)
                            classSO = ScriptableObject.CreateInstance<ClassSO>();

                        var system = data["system"] as JObject;
                        if (system == null)
                            continue;

                        classSO.Id = id;
                        classSO.ElementName = name;
                        classSO.Slug = name.ToLower().Replace(" ", "-");

                        var descObj = system["description"] as JObject;
                        classSO.Description =
                            descObj != null
                                ? ConvertHtmlToTMP(descObj["value"]?.ToString() ?? "")
                                : "";

                        var pubObj = system["publication"] as JObject;
                        classSO.SourceBook =
                            pubObj != null ? pubObj["title"]?.ToString() ?? "" : "";

                        var traitsObj = system["traits"] as JObject;
                        classSO.Traits =
                            traitsObj != null
                                ? ExtractStringArray(traitsObj["value"])
                                : new List<string>();

                        classSO.HP = ParseInt(system["hp"]);
                        classSO.Perception = ParseInt(system["perception"]);
                        classSO.Spellcasting = ParseInt(system["spellcasting"]);

                        // Key Abilities
                        var keyAbObj = system["keyAbility"] as JObject;
                        if (keyAbObj != null)
                        {
                            classSO.KeyAbilities.Clear();
                            foreach (string ab in ExtractStringArray(keyAbObj["value"]))
                                classSO.KeyAbilities.Add(ParseAbilityType(ab));
                        }

                        // Saving Throws
                        var savesObj = system["savingThrows"] as JObject;
                        if (savesObj != null)
                        {
                            classSO.Fortitude = ParseInt(savesObj["fortitude"]);
                            classSO.Reflex = ParseInt(savesObj["reflex"]);
                            classSO.Will = ParseInt(savesObj["will"]);
                        }

                        // Attacks
                        var attacksObj = system["attacks"] as JObject;
                        if (attacksObj != null)
                        {
                            classSO.SimpleWeapons = ParseInt(attacksObj["simple"]);
                            classSO.MartialWeapons = ParseInt(attacksObj["martial"]);
                            classSO.AdvancedWeapons = ParseInt(attacksObj["advanced"]);
                            classSO.UnarmedAttacks = ParseInt(attacksObj["unarmed"]);
                        }

                        // Defenses
                        var defensesObj = system["defenses"] as JObject;
                        if (defensesObj != null)
                        {
                            classSO.Unarmored = ParseInt(defensesObj["unarmored"]);
                            classSO.LightArmor = ParseInt(defensesObj["light"]);
                            classSO.MediumArmor = ParseInt(defensesObj["medium"]);
                            classSO.HeavyArmor = ParseInt(defensesObj["heavy"]);
                        }

                        // Skills
                        var skillsObj = system["trainedSkills"] as JObject;
                        if (skillsObj != null)
                        {
                            classSO.TrainedSkills = ExtractStringArray(skillsObj["value"]);
                            classSO.AdditionalSkillCount = ParseInt(skillsObj["additional"]);
                        }

                        // Feat Progression
                        var afLevels = system["ancestryFeatLevels"] as JObject;
                        if (afLevels != null)
                            classSO.AncestryFeatLevels = ExtractIntArray(afLevels["value"]);
                        var cfLevels = system["classFeatLevels"] as JObject;
                        if (cfLevels != null)
                            classSO.ClassFeatLevels = ExtractIntArray(cfLevels["value"]);
                        var sfLevels = system["skillFeatLevels"] as JObject;
                        if (sfLevels != null)
                            classSO.SkillFeatLevels = ExtractIntArray(sfLevels["value"]);
                        var gfLevels = system["generalFeatLevels"] as JObject;
                        if (gfLevels != null)
                            classSO.GeneralFeatLevels = ExtractIntArray(gfLevels["value"]);
                        var siLevels = system["skillIncreaseLevels"] as JObject;
                        if (siLevels != null)
                            classSO.SkillIncreaseLevels = ExtractIntArray(siLevels["value"]);

                        // Granted Features (system.items)
                        var itemsObj = system["items"] as JObject;
                        if (itemsObj != null)
                        {
                            classSO.GrantedFeatures.Clear();
                            foreach (var prop in itemsObj.Properties())
                            {
                                var entry = prop.Value as JObject;
                                if (entry == null)
                                    continue;
                                classSO.GrantedFeatures.Add(
                                    new ClassFeatureEntry
                                    {
                                        Name = entry["name"]?.ToString() ?? "",
                                        Level = ParseInt(entry["level"]),
                                        UUID = entry["uuid"]?.ToString() ?? "",
                                    }
                                );
                            }
                        }

                        classSO.CompendiumSource = "pf2e.classes";
                        classSO.BuildDerivedFields();

                        if (isNew)
                        {
                            AssetDatabase.CreateAsset(classSO, assetPath);
                            db.AllClasses.Add(classSO);
                        }
                        else
                        {
                            EditorUtility.SetDirty(classSO);
                            if (!db.AllClasses.Contains(classSO))
                                db.AllClasses.Add(classSO);
                        }
                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(
                            $"[Importer] Failed to parse class {Path.GetFileName(file)}: {e.Message}"
                        );
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"<color=green>[Importer] Successfully compiled {count} PF2e classes!</color>"
            );
        }

        // =========================================================================
        // IMPORT ANCESTRIES
        // =========================================================================
        private void ImportAncestries()
        {
            string ancestryDir = Path.Combine(packsDirectory, "ancestries");
            if (!Directory.Exists(ancestryDir))
            {
                Debug.LogError($"[Importer] Ancestries dir not found: {ancestryDir}");
                return;
            }

            string[] files = Directory.GetFiles(ancestryDir, "*.json", SearchOption.AllDirectories);
            string outputDir = "Assets/GameData/PF2e/Ancestries";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            PF2eDatabase db = LoadOrCreateDatabase();
            db.AllAncestries.Clear();

            AssetDatabase.StartAssetEditing();
            int count = 0;
            try
            {
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        JObject data = JObject.Parse(json);

                        string id = data["_id"]?.ToString();
                        string name = data["name"]?.ToString();
                        string type = data["type"]?.ToString();
                        if (type != "ancestry" || string.IsNullOrEmpty(id))
                            continue;

                        string safeName = SanitizeFileName(name);
                        string assetPath = $"{outputDir}/{safeName}_{id}.asset";

                        AncestrySO ancestrySO = AssetDatabase.LoadAssetAtPath<AncestrySO>(
                            assetPath
                        );
                        bool isNew = ancestrySO == null;
                        if (isNew)
                            ancestrySO = ScriptableObject.CreateInstance<AncestrySO>();

                        var system = data["system"] as JObject;
                        if (system == null)
                            continue;

                        ancestrySO.Id = id;
                        ancestrySO.ElementName = name;
                        ancestrySO.Slug = name.ToLower().Replace(" ", "-");

                        var descObj = system["description"] as JObject;
                        ancestrySO.Description =
                            descObj != null
                                ? ConvertHtmlToTMP(descObj["value"]?.ToString() ?? "")
                                : "";

                        var pubObj = system["publication"] as JObject;
                        ancestrySO.SourceBook =
                            pubObj != null ? pubObj["title"]?.ToString() ?? "" : "";

                        var traitsObj = system["traits"] as JObject;
                        ancestrySO.Traits =
                            traitsObj != null
                                ? ExtractStringArray(traitsObj["value"])
                                : new List<string>();

                        ancestrySO.HP = ParseInt(system["hp"]);
                        ancestrySO.Speed = ParseInt(system["speed"]);
                        ancestrySO.Reach = ParseInt(system["reach"]);
                        ancestrySO.Size = ParseCreatureSize(system["size"]?.ToString());
                        ancestrySO.Vision = system["vision"]?.ToString() ?? "normal";

                        // Languages
                        var langObj = system["languages"] as JObject;
                        if (langObj != null)
                        {
                            ancestrySO.Languages = ExtractStringArray(langObj["value"]);
                        }
                        var addLangObj = system["additionalLanguages"] as JObject;
                        if (addLangObj != null)
                        {
                            ancestrySO.AdditionalLanguageCount = ParseInt(addLangObj["count"]);
                        }

                        // Boosts
                        var boostsObj = system["boosts"] as JObject;
                        if (boostsObj != null)
                        {
                            ancestrySO.Boosts.Clear();
                            foreach (var prop in boostsObj.Properties())
                            {
                                var boostEntry = prop.Value as JObject;
                                if (boostEntry == null)
                                    continue;
                                var abilities = ExtractStringArray(boostEntry["value"]);
                                var entry = new AbilityBoostEntry
                                {
                                    Options = abilities.Select(a => ParseAbilityType(a)).ToList(),
                                };
                                ancestrySO.Boosts.Add(entry);
                            }
                        }

                        // Flaws
                        var flawsObj = system["flaws"] as JObject;
                        if (flawsObj != null)
                        {
                            ancestrySO.Flaws.Clear();
                            foreach (var prop in flawsObj.Properties())
                            {
                                var flawEntry = prop.Value as JObject;
                                if (flawEntry == null)
                                    continue;
                                var abilities = ExtractStringArray(flawEntry["value"]);
                                if (abilities.Count > 0)
                                {
                                    var entry = new AbilityBoostEntry
                                    {
                                        Options = abilities
                                            .Select(a => ParseAbilityType(a))
                                            .ToList(),
                                    };
                                    ancestrySO.Flaws.Add(entry);
                                }
                            }
                        }

                        ancestrySO.CompendiumSource = "pf2e.ancestries";
                        ancestrySO.BuildDerivedFields();

                        if (isNew)
                        {
                            AssetDatabase.CreateAsset(ancestrySO, assetPath);
                            db.AllAncestries.Add(ancestrySO);
                        }
                        else
                        {
                            EditorUtility.SetDirty(ancestrySO);
                            if (!db.AllAncestries.Contains(ancestrySO))
                                db.AllAncestries.Add(ancestrySO);
                        }
                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(
                            $"[Importer] Failed to parse ancestry {Path.GetFileName(file)}: {e.Message}"
                        );
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"<color=green>[Importer] Successfully compiled {count} PF2e ancestries!</color>"
            );
        }

        // =========================================================================
        // IMPORT BACKGROUNDS
        // =========================================================================
        private void ImportBackgrounds()
        {
            string bgDir = Path.Combine(packsDirectory, "backgrounds");
            if (!Directory.Exists(bgDir))
            {
                Debug.LogError($"[Importer] Backgrounds dir not found: {bgDir}");
                return;
            }

            string[] files = Directory.GetFiles(bgDir, "*.json", SearchOption.AllDirectories);
            string outputDir = "Assets/GameData/PF2e/Backgrounds";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            PF2eDatabase db = LoadOrCreateDatabase();
            db.AllBackgrounds.Clear();

            AssetDatabase.StartAssetEditing();
            int count = 0;
            try
            {
                foreach (var file in files)
                {
                    if (file.EndsWith("_folders.json"))
                        continue;
                    try
                    {
                        string json = File.ReadAllText(file);
                        JObject data = JObject.Parse(json);

                        string id = data["_id"]?.ToString();
                        string name = data["name"]?.ToString();
                        string type = data["type"]?.ToString();
                        if (type != "background" || string.IsNullOrEmpty(id))
                            continue;

                        string safeName = SanitizeFileName(name);
                        string assetPath = $"{outputDir}/{safeName}_{id}.asset";

                        BackgroundSO bgSO = AssetDatabase.LoadAssetAtPath<BackgroundSO>(assetPath);
                        bool isNew = bgSO == null;
                        if (isNew)
                            bgSO = ScriptableObject.CreateInstance<BackgroundSO>();

                        var system = data["system"] as JObject;
                        if (system == null)
                            continue;

                        bgSO.Id = id;
                        bgSO.ElementName = name;
                        bgSO.Slug = name.ToLower().Replace(" ", "-");

                        var descObj = system["description"] as JObject;
                        bgSO.Description =
                            descObj != null
                                ? ConvertHtmlToTMP(descObj["value"]?.ToString() ?? "")
                                : "";

                        var pubObj = system["publication"] as JObject;
                        bgSO.SourceBook = pubObj != null ? pubObj["title"]?.ToString() ?? "" : "";

                        var traitsObj = system["traits"] as JObject;
                        bgSO.Traits =
                            traitsObj != null
                                ? ExtractStringArray(traitsObj["value"])
                                : new List<string>();

                        // Boosts
                        var boostsObj = system["boosts"] as JObject;
                        if (boostsObj != null)
                        {
                            bgSO.Boosts.Clear();
                            foreach (var prop in boostsObj.Properties())
                            {
                                var boostEntry = prop.Value as JObject;
                                if (boostEntry == null)
                                    continue;
                                var abilities = ExtractStringArray(boostEntry["value"]);
                                var entry = new AbilityBoostEntry
                                {
                                    Options = abilities.Select(a => ParseAbilityType(a)).ToList(),
                                };
                                bgSO.Boosts.Add(entry);
                            }
                        }

                        // Trained Skills
                        var skillsObj = system["trainedSkills"] as JObject;
                        if (skillsObj != null)
                        {
                            bgSO.TrainedSkills = ExtractStringArray(skillsObj["value"]);
                            bgSO.LoreSkills = ExtractStringArray(skillsObj["lore"]);
                        }

                        // Granted Feats (system.items)
                        var itemsObj = system["items"] as JObject;
                        if (itemsObj != null)
                        {
                            bgSO.GrantedFeatIds.Clear();
                            foreach (var prop in itemsObj.Properties())
                            {
                                var entry = prop.Value as JObject;
                                if (entry != null)
                                    bgSO.GrantedFeatIds.Add(entry["uuid"]?.ToString() ?? "");
                            }
                        }

                        bgSO.CompendiumSource = "pf2e.backgrounds";
                        bgSO.BuildDerivedFields();

                        if (isNew)
                        {
                            AssetDatabase.CreateAsset(bgSO, assetPath);
                            db.AllBackgrounds.Add(bgSO);
                        }
                        else
                        {
                            EditorUtility.SetDirty(bgSO);
                            if (!db.AllBackgrounds.Contains(bgSO))
                                db.AllBackgrounds.Add(bgSO);
                        }
                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(
                            $"[Importer] Failed to parse background {Path.GetFileName(file)}: {e.Message}"
                        );
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"<color=green>[Importer] Successfully compiled {count} PF2e backgrounds!</color>"
            );
        }

        // =========================================================================
        // DATABASE HELPER
        // =========================================================================
        private PF2eDatabase LoadOrCreateDatabase()
        {
            PF2eDatabase db = AssetDatabase.LoadAssetAtPath<PF2eDatabase>(databasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<PF2eDatabase>();
                AssetDatabase.CreateAsset(db, databasePath);
            }
            return db;
        }

        // --- Helper Methods ---

        private DiceFormula ParseDiceFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
                return new DiceFormula(0, 0, 0);

            // Simple parse for "6d6", "2d8 + 4"
            var match = Regex.Match(formula, @"(\d+)d(\d+)\s*([+-]\s*\d+)?");
            if (match.Success)
            {
                int count = int.Parse(match.Groups[1].Value);
                int size = int.Parse(match.Groups[2].Value);
                int bonus = 0;

                if (match.Groups[3].Success)
                {
                    string bStr = match.Groups[3].Value.Replace(" ", "");
                    int.TryParse(bStr, out bonus);
                }

                return new DiceFormula(count, size, bonus);
            }
            return new DiceFormula(0, 0, 0);
        }

        private ActionCost ParseActionCost(string cost)
        {
            if (string.IsNullOrEmpty(cost))
                return ActionCost.Variable;

            if (cost.Contains("1") && cost.Contains("2"))
                return ActionCost.Variable;
            if (cost.Contains("1") && cost.Contains("3"))
                return ActionCost.Variable;

            if (cost.Contains("1"))
                return ActionCost.One;
            if (cost.Contains("2"))
                return ActionCost.Two;
            if (cost.Contains("3"))
                return ActionCost.Three;
            if (cost.Contains("free"))
                return ActionCost.Free;
            if (cost.Contains("reaction"))
                return ActionCost.Reaction;

            return ActionCost.Variable;
        }

        private int ParseRange(string rangeStr)
        {
            if (string.IsNullOrEmpty(rangeStr))
                return 0;
            if (rangeStr.Contains("touch"))
                return 0;

            var match = Regex.Match(rangeStr, @"(\d+)");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return 0;
        }

        private SavingThrowType ParseSavingThrow(string stat)
        {
            if (string.IsNullOrEmpty(stat))
                return SavingThrowType.None;
            return stat.ToLower() switch
            {
                "fortitude" => SavingThrowType.Fortitude,
                "reflex" => SavingThrowType.Reflex,
                "will" => SavingThrowType.Will,
                _ => SavingThrowType.None,
            };
        }

        private DamageType ParseDamageType(string type)
        {
            if (string.IsNullOrEmpty(type))
                return DamageType.Bludgeoning;
            return type.ToLower() switch
            {
                "piercing" => DamageType.Piercing,
                "slashing" => DamageType.Slashing,
                "bludgeoning" => DamageType.Bludgeoning,
                "fire" => DamageType.Fire,
                "cold" => DamageType.Cold,
                "acid" => DamageType.Acid,
                "electricity" => DamageType.Electricity,
                "poison" => DamageType.Poison,
                "bleed" => DamageType.Bleed,
                "mental" => DamageType.Mental,
                _ => DamageType.Bludgeoning,
            };
        }

        private AreaShape ParseAreaShape(string shape)
        {
            if (string.IsNullOrEmpty(shape))
                return AreaShape.None;
            return shape.ToLower() switch
            {
                "burst" => AreaShape.Burst,
                "cone" => AreaShape.Cone,
                "line" => AreaShape.Line,
                "emanation" => AreaShape.Emanation,
                _ => AreaShape.None,
            };
        }

        private List<string> ExtractStringArray(JToken token)
        {
            if (token == null || !token.HasValues)
                return new List<string>();
            return token.ToObject<List<string>>();
        }

        private int ParseInt(JToken token)
        {
            if (token == null)
                return 0;
            int.TryParse(token.ToString(), out int val);
            return val;
        }

        private AbilityType ParseAbilityType(string ab)
        {
            if (string.IsNullOrEmpty(ab))
                return AbilityType.Free;
            return ab.ToLower() switch
            {
                "str" => AbilityType.Str,
                "dex" => AbilityType.Dex,
                "con" => AbilityType.Con,
                "int" => AbilityType.Int,
                "wis" => AbilityType.Wis,
                "cha" => AbilityType.Cha,
                _ => AbilityType.Free,
            };
        }

        private CreatureSize ParseCreatureSize(string size)
        {
            if (string.IsNullOrEmpty(size))
                return CreatureSize.Medium;
            return size.ToLower() switch
            {
                "tiny" => CreatureSize.Tiny,
                "sm" => CreatureSize.Small,
                "med" => CreatureSize.Medium,
                "lg" => CreatureSize.Large,
                "huge" => CreatureSize.Huge,
                "grg" => CreatureSize.Gargantuan,
                _ => CreatureSize.Medium,
            };
        }

        private List<int> ExtractIntArray(JToken token)
        {
            if (token == null || !token.HasValues)
                return new List<int>();
            return token.ToObject<List<int>>();
        }

        private string SanitizeFileName(string name)
        {
            string invalid =
                new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid)
                name = name.Replace(c.ToString(), "");
            return name.Replace(":", "").Replace(" ", "_");
        }

        private string ConvertHtmlToTMP(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";
            // Convert to TextMeshPro friendly tags
            string res = html.Replace("<p>", "").Replace("</p>", "\n\n");
            res = res.Replace("<strong>", "<b>").Replace("</strong>", "</b>");
            res = res.Replace("<em>", "<i>").Replace("</em>", "</i>");
            res = Regex.Replace(res, "<hr.*?>", "\n---\n");
            return res.Trim();
        }
    }
}
#endif

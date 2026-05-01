#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TacticsGame.Data.TacticsCore;
using UnityEditor;
using UnityEngine;

namespace TacticsGame.EditorTools
{
    public class TacticsCoreDataImporter : EditorWindow
    {
        private const string DefaultOutputRoot = "Assets/GameData/TacticsCore";

        private string sourcePacksRoot = DetectDefaultPacksRoot();
        private string outputRoot = DefaultOutputRoot;

        private readonly ImportReport report = new ImportReport();
        private readonly Dictionary<string, FeatureDataSO> featuresById =
            new Dictionary<string, FeatureDataSO>();
        private readonly Dictionary<string, FeatureDataSO> featuresByName =
            new Dictionary<string, FeatureDataSO>();
        private readonly Dictionary<string, AncestryDataSO> ancestriesBySlug =
            new Dictionary<string, AncestryDataSO>();

        [MenuItem("Tactics Core/Import Character Data")]
        public static void ShowWindow()
        {
            GetWindow<TacticsCoreDataImporter>("Tactics Data Importer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Tactics Character Data Importer", EditorStyles.boldLabel);
            sourcePacksRoot = EditorGUILayout.TextField("Source Packs Root", sourcePacksRoot);
            outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);

            GUILayout.Space(8);

            if (GUILayout.Button("Import Character Creation Data"))
                ImportAll();

            if (GUILayout.Button("Import Features Only"))
                ImportFeaturesOnly();
        }

        private void ImportAll()
        {
            report.Reset();
            if (!Directory.Exists(sourcePacksRoot))
            {
                Debug.LogError(
                    $"[Tactics Importer] Source packs root not found: {sourcePacksRoot}"
                );
                return;
            }

            EnsureDirectory(outputRoot);

            AssetDatabase.StartAssetEditing();
            try
            {
                ImportFeatures();
                ImportAncestries();
                ImportHeritages();
                ImportBackgrounds();
                ImportClasses();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.BuildSummary());
        }

        private void ImportFeaturesOnly()
        {
            report.Reset();
            if (!Directory.Exists(sourcePacksRoot))
            {
                Debug.LogError(
                    $"[Tactics Importer] Source packs root not found: {sourcePacksRoot}"
                );
                return;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                ImportFeatures();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.BuildSummary());
        }

        private void ImportFeatures()
        {
            featuresById.Clear();
            featuresByName.Clear();

            foreach (string file in EnumerateJsonFiles("classfeatures"))
            {
                ImportJsonFile(
                    file,
                    "Features",
                    "feature",
                    data =>
                    {
                        JObject system = GetObject(data, "system");
                        if (system == null)
                            return null;

                        FeatureDataSO asset = LoadOrCreate<FeatureDataSO>(
                            "Features",
                            "feature",
                            GetString(data, "_id")
                        );

                        PopulateCommon(asset, data, file, "classfeatures");
                        asset.Category = ParseFeatureCategory(GetString(system, "category"));
                        asset.Level = GetInt(GetObject(system, "level"), "value");
                        asset.GrantedFeatures = ParseGrants(GetObject(system, "items"));
                        asset.GrantedFeatures.AddRange(
                            ParseRuleGrantedItems(GetArray(system, "rules"))
                        );
                        asset.ProficiencyUpgrades = ParseProficiencyUpgrades(
                            GetObject(system, "subfeatures")
                        );
                        asset.FocusPointDelta = ParseFocusDelta(GetArray(system, "rules"));

                        MarkAsset(asset);
                        RegisterFeature(asset);
                        report.Features++;
                        return asset;
                    }
                );
            }
        }

        private void ImportAncestries()
        {
            ancestriesBySlug.Clear();

            foreach (string file in EnumerateJsonFiles("ancestries"))
            {
                ImportJsonFile(
                    file,
                    "Ancestries",
                    "ancestry",
                    data =>
                    {
                        if (GetString(data, "type") != "ancestry")
                            return null;

                        JObject system = GetObject(data, "system");
                        if (system == null)
                            return null;

                        AncestryDataSO asset = LoadOrCreate<AncestryDataSO>(
                            "Ancestries",
                            "ancestry",
                            GetString(data, "_id")
                        );

                        PopulateCommon(asset, data, file, "ancestries");
                        asset.HitPoints = GetInt(system, "hp");
                        asset.Speed = GetInt(system, "speed");
                        asset.Reach = GetInt(system, "reach");
                        asset.Size = ParseSize(GetString(system, "size"));
                        (asset.Sense, asset.CustomSense) = ParseSense(GetString(system, "vision"));
                        asset.AttributeBoosts = ParseAttributeSets(GetObject(system, "boosts"));
                        asset.AttributeFlaws = ParseAttributeSets(GetObject(system, "flaws"));
                        asset.StartingLanguages = ParseLanguages(
                            GetObject(system, "languages"),
                            "value"
                        );
                        asset.AdditionalLanguageOptions = ParseLanguages(
                            GetObject(system, "additionalLanguages"),
                            "value"
                        );
                        asset.AdditionalLanguageCount = GetInt(
                            GetObject(system, "additionalLanguages"),
                            "count"
                        );
                        asset.GrantedFeatures = ParseGrants(GetObject(system, "items"));
                        ResolveFeatureGrants(asset.GrantedFeatures);

                        MarkAsset(asset);
                        if (!string.IsNullOrWhiteSpace(asset.Slug))
                            ancestriesBySlug[asset.Slug] = asset;
                        report.Ancestries++;
                        return asset;
                    }
                );
            }
        }

        private void ImportHeritages()
        {
            foreach (string file in EnumerateJsonFiles("heritages"))
            {
                if (
                    Path.GetFileName(file)
                        .Equals("_folders.json", StringComparison.OrdinalIgnoreCase)
                )
                    continue;

                ImportJsonFile(
                    file,
                    "Heritages",
                    "heritage",
                    data =>
                    {
                        if (GetString(data, "type") != "heritage")
                            return null;

                        JObject system = GetObject(data, "system");
                        if (system == null)
                            return null;

                        HeritageDataSO asset = LoadOrCreate<HeritageDataSO>(
                            "Heritages",
                            "heritage",
                            GetString(data, "_id")
                        );

                        PopulateCommon(asset, data, file, "heritages");
                        JObject ancestry = GetObject(system, "ancestry");
                        asset.ParentAncestry = new PackReference
                        {
                            SourceUuid = GetString(ancestry, "uuid"),
                            Slug = GetString(ancestry, "slug"),
                            DisplayName = GetString(ancestry, "name"),
                        };

                        if (
                            !string.IsNullOrWhiteSpace(asset.ParentAncestry.Slug)
                            && ancestriesBySlug.TryGetValue(
                                asset.ParentAncestry.Slug,
                                out AncestryDataSO parent
                            )
                        )
                        {
                            asset.ParentAncestryAsset = parent;
                            if (!parent.Heritages.Contains(asset))
                            {
                                parent.Heritages.Add(asset);
                                EditorUtility.SetDirty(parent);
                            }
                        }

                        asset.GrantedFeatures = ParseRuleGrantedItems(GetArray(system, "rules"));
                        ResolveFeatureGrants(asset.GrantedFeatures);

                        MarkAsset(asset);
                        report.Heritages++;
                        return asset;
                    }
                );
            }
        }

        private void ImportBackgrounds()
        {
            foreach (string file in EnumerateJsonFiles("backgrounds"))
            {
                ImportJsonFile(
                    file,
                    "Backgrounds",
                    "background",
                    data =>
                    {
                        if (GetString(data, "type") != "background")
                            return null;

                        JObject system = GetObject(data, "system");
                        if (system == null)
                            return null;

                        BackgroundDataSO asset = LoadOrCreate<BackgroundDataSO>(
                            "Backgrounds",
                            "background",
                            GetString(data, "_id")
                        );

                        PopulateCommon(asset, data, file, "backgrounds");
                        asset.AttributeBoosts = ParseAttributeSets(GetObject(system, "boosts"));
                        asset.TrainedSkills = ParseSkillTraining(
                            GetObject(system, "trainedSkills")
                        );
                        asset.LoreSkills = GetStringList(
                            GetObject(system, "trainedSkills"),
                            "lore"
                        );
                        asset.GrantedFeats = ParseGrants(GetObject(system, "items"));
                        ResolveFeatureGrants(asset.GrantedFeats);

                        MarkAsset(asset);
                        report.Backgrounds++;
                        return asset;
                    }
                );
            }
        }

        private void ImportClasses()
        {
            foreach (string file in EnumerateJsonFiles("classes"))
            {
                ImportJsonFile(
                    file,
                    "Classes",
                    "class",
                    data =>
                    {
                        if (GetString(data, "type") != "class")
                            return null;

                        JObject system = GetObject(data, "system");
                        if (system == null)
                            return null;

                        TacticsClassSO asset = LoadOrCreate<TacticsClassSO>(
                            "Classes",
                            "class",
                            GetString(data, "_id")
                        );

                        PopulateCommon(asset, data, file, "classes");
                        asset.HitPointsPerLevel = GetInt(system, "hp");
                        asset.KeyAttributes = GetStringList(
                                GetObject(system, "keyAbility"),
                                "value"
                            )
                            .Select(ParseAttribute)
                            .Distinct()
                            .ToList();
                        asset.Perception = ParseRank(GetInt(system, "perception"));
                        asset.Spellcasting = ParseRank(GetInt(system, "spellcasting"));
                        asset.HasSpellcasting = asset.Spellcasting > ProficiencyRank.Untrained;
                        asset.SpellDifficulty = asset.Spellcasting;
                        asset.ClassDifficulty = ParseRank(GetInt(system, "classDC"));
                        asset.SavingThrows = ParseSaves(GetObject(system, "savingThrows"));
                        asset.ArmorProficiencies = ParseArmor(GetObject(system, "defenses"));
                        asset.WeaponProficiencies = ParseWeapons(GetObject(system, "attacks"));
                        asset.TrainedSkills = ParseSkillTraining(
                            GetObject(system, "trainedSkills")
                        );
                        asset.AdditionalSkillCount = GetInt(
                            GetObject(system, "trainedSkills"),
                            "additional"
                        );
                        asset.AncestryFeatureLevels = GetIntList(
                            GetObject(system, "ancestryFeatLevels"),
                            "value"
                        );
                        asset.ClassFeatureLevels = GetIntList(
                            GetObject(system, "classFeatLevels"),
                            "value"
                        );
                        asset.SkillFeatureLevels = GetIntList(
                            GetObject(system, "skillFeatLevels"),
                            "value"
                        );
                        asset.GeneralFeatureLevels = GetIntList(
                            GetObject(system, "generalFeatLevels"),
                            "value"
                        );
                        asset.SkillIncreaseLevels = GetIntList(
                            GetObject(system, "skillIncreaseLevels"),
                            "value"
                        );
                        asset.GrantedFeatures = ParseGrants(GetObject(system, "items"));
                        ResolveFeatureGrants(asset.GrantedFeatures);
                        asset.LevelOneFeatures = asset
                            .GrantedFeatures.Where(grant => grant.Level <= 1)
                            .ToList();
                        asset.StartingFocusPoints = EstimateStartingFocusPoints(
                            asset.GrantedFeatures
                        );

                        MarkAsset(asset);
                        report.Classes++;
                        return asset;
                    }
                );
            }
        }

        private IEnumerable<string> EnumerateJsonFiles(string packFolder)
        {
            string directory = Path.Combine(sourcePacksRoot, packFolder);
            if (!Directory.Exists(directory))
            {
                report.Warnings.Add($"Missing source directory: {directory}");
                return Enumerable.Empty<string>();
            }

            return Directory
                .GetFiles(directory, "*.json", SearchOption.AllDirectories)
                .Where(file =>
                    !Path.GetFileName(file)
                        .Equals("_folders.json", StringComparison.OrdinalIgnoreCase)
                );
        }

        private void ImportJsonFile(
            string file,
            string outputFolder,
            string filePrefix,
            Func<JObject, ScriptableObject> importer
        )
        {
            try
            {
                JObject data = JObject.Parse(File.ReadAllText(file));
                ScriptableObject asset = importer(data);
                if (asset == null)
                    report.Skipped++;
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        private T LoadOrCreate<T>(string folder, string prefix, string sourceId)
            where T : ScriptableObject
        {
            EnsureDirectory(Path.Combine(outputRoot, folder).Replace("\\", "/"));
            string safeId = SanitizeFileName(
                string.IsNullOrWhiteSpace(sourceId) ? Guid.NewGuid().ToString("N") : sourceId
            );
            string assetPath = $"{outputRoot}/{folder}/{prefix}_{safeId}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
                return asset;

            if (File.Exists(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private void PopulateCommon(
            TacticsGameElementSO asset,
            JObject data,
            string file,
            string sourcePack
        )
        {
            JObject system = GetObject(data, "system");
            JObject description = GetObject(system, "description");
            JObject publication = GetObject(system, "publication");
            JObject traits = GetObject(system, "traits");

            asset.SourceId = GetString(data, "_id");
            asset.DisplayName = GetString(data, "name");
            asset.Slug = GetString(system, "slug");
            if (string.IsNullOrWhiteSpace(asset.Slug))
                asset.Slug = Slugify(asset.DisplayName);
            asset.IconPath = GetString(data, "img");
            asset.Description = StripMarkup(GetString(description, "value"));
            asset.SourceTitle = GetString(publication, "title");
            asset.SourcePack = sourcePack;
            asset.Rarity = GetString(traits, "rarity");
            asset.Traits = GetStringList(traits, "value");
            asset.RawRules = ParseRawRules(GetArray(system, "rules"));
            asset.SourceJsonHash = ComputeHash(File.ReadAllText(file));
            asset.BuildDerivedFields();
        }

        private List<AttributeChoiceSet> ParseAttributeSets(JObject source)
        {
            var result = new List<AttributeChoiceSet>();
            if (source == null)
                return result;

            foreach (JProperty property in source.Properties().OrderBy(prop => prop.Name))
            {
                List<AttributeType> options = GetStringList(property.Value as JObject, "value")
                    .Select(ParseAttribute)
                    .Where(attribute => attribute != AttributeType.Any)
                    .Distinct()
                    .ToList();

                if (options.Count == 0 || options.Count >= 6)
                    options = new List<AttributeType> { AttributeType.Any };

                result.Add(new AttributeChoiceSet { Options = options });
            }

            return result;
        }

        private List<LanguageEntry> ParseLanguages(JObject source, string key)
        {
            return GetStringList(source, key)
                .Select(value => new LanguageEntry
                {
                    Id = value,
                    DisplayName = ToDisplayName(value),
                })
                .ToList();
        }

        private List<SkillTrainingEntry> ParseSkillTraining(JObject source)
        {
            var result = new List<SkillTrainingEntry>();
            foreach (string skill in GetStringList(source, "value"))
            {
                result.Add(
                    new SkillTrainingEntry
                    {
                        Skill = ParseSkill(skill),
                        CustomSkillId = skill,
                        Rank = ProficiencyRank.Trained,
                    }
                );
            }

            foreach (string lore in GetStringList(source, "lore"))
            {
                result.Add(
                    new SkillTrainingEntry
                    {
                        Skill = SkillType.Lore,
                        CustomSkillId = "lore",
                        LoreName = lore,
                        Rank = ProficiencyRank.Trained,
                    }
                );
            }

            return result;
        }

        private List<FeatureGrant> ParseGrants(JObject source)
        {
            var result = new List<FeatureGrant>();
            if (source == null)
                return result;

            foreach (JProperty property in source.Properties())
            {
                JObject entry = property.Value as JObject;
                if (entry == null)
                    continue;

                result.Add(
                    new FeatureGrant
                    {
                        SourceKey = property.Name,
                        SourceUuid = GetString(entry, "uuid"),
                        DisplayName = GetString(entry, "name"),
                        Level = GetInt(entry, "level"),
                    }
                );
            }

            return result;
        }

        private List<FeatureGrant> ParseRuleGrantedItems(JArray rules)
        {
            var result = new List<FeatureGrant>();
            if (rules == null)
                return result;

            foreach (JObject rule in rules.OfType<JObject>())
            {
                if (!GetString(rule, "key").Equals("GrantItem", StringComparison.OrdinalIgnoreCase))
                    continue;

                string uuid = GetString(rule, "uuid");
                result.Add(
                    new FeatureGrant { SourceUuid = uuid, DisplayName = ExtractNameFromUuid(uuid) }
                );
            }

            return result;
        }

        private void ResolveFeatureGrants(List<FeatureGrant> grants)
        {
            foreach (FeatureGrant grant in grants)
            {
                string key = NormalizeName(grant.DisplayName);
                if (
                    !string.IsNullOrWhiteSpace(key)
                    && featuresByName.TryGetValue(key, out FeatureDataSO feature)
                )
                {
                    grant.ResolvedFeature = feature;
                    grant.SourceId = feature.SourceId;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(grant.SourceUuid))
                    report.UnresolvedReferences.Add(grant.SourceUuid);
            }
        }

        private List<ProficiencyUpgrade> ParseProficiencyUpgrades(JObject subfeatures)
        {
            var result = new List<ProficiencyUpgrade>();
            JObject proficiencies = GetObject(subfeatures, "proficiencies");
            if (proficiencies == null)
                return result;

            foreach (JProperty property in proficiencies.Properties())
            {
                JObject entry = property.Value as JObject;
                result.Add(
                    new ProficiencyUpgrade
                    {
                        Target = property.Name,
                        Rank = ParseRank(GetInt(entry, "rank")),
                    }
                );
            }

            return result;
        }

        private int ParseFocusDelta(JArray rules)
        {
            if (rules == null)
                return 0;

            int focusDelta = 0;
            foreach (JObject rule in rules.OfType<JObject>())
            {
                string path = GetString(rule, "path");
                if (!path.Contains("resources.focus.max"))
                    continue;

                focusDelta += GetInt(rule, "value");
            }

            return focusDelta;
        }

        private int EstimateStartingFocusPoints(List<FeatureGrant> grants)
        {
            return grants.Any(grant =>
                grant.ResolvedFeature != null && grant.ResolvedFeature.FocusPointDelta > 0
            )
                ? 1
                : 0;
        }

        private List<SaveProficiencyEntry> ParseSaves(JObject source)
        {
            return new List<SaveProficiencyEntry>
            {
                new SaveProficiencyEntry
                {
                    Save = DefenseSaveType.Fortitude,
                    Rank = ParseRank(GetInt(source, "fortitude")),
                },
                new SaveProficiencyEntry
                {
                    Save = DefenseSaveType.Reflex,
                    Rank = ParseRank(GetInt(source, "reflex")),
                },
                new SaveProficiencyEntry
                {
                    Save = DefenseSaveType.Will,
                    Rank = ParseRank(GetInt(source, "will")),
                },
            };
        }

        private List<ArmorProficiencyEntry> ParseArmor(JObject source)
        {
            return new List<ArmorProficiencyEntry>
            {
                new ArmorProficiencyEntry
                {
                    Group = ArmorGroup.Unarmored,
                    Rank = ParseRank(GetInt(source, "unarmored")),
                },
                new ArmorProficiencyEntry
                {
                    Group = ArmorGroup.Light,
                    Rank = ParseRank(GetInt(source, "light")),
                },
                new ArmorProficiencyEntry
                {
                    Group = ArmorGroup.Medium,
                    Rank = ParseRank(GetInt(source, "medium")),
                },
                new ArmorProficiencyEntry
                {
                    Group = ArmorGroup.Heavy,
                    Rank = ParseRank(GetInt(source, "heavy")),
                },
            };
        }

        private List<WeaponProficiencyEntry> ParseWeapons(JObject source)
        {
            var result = new List<WeaponProficiencyEntry>
            {
                new WeaponProficiencyEntry
                {
                    Group = WeaponGroup.Unarmed,
                    Rank = ParseRank(GetInt(source, "unarmed")),
                },
                new WeaponProficiencyEntry
                {
                    Group = WeaponGroup.Simple,
                    Rank = ParseRank(GetInt(source, "simple")),
                },
                new WeaponProficiencyEntry
                {
                    Group = WeaponGroup.Martial,
                    Rank = ParseRank(GetInt(source, "martial")),
                },
                new WeaponProficiencyEntry
                {
                    Group = WeaponGroup.Advanced,
                    Rank = ParseRank(GetInt(source, "advanced")),
                },
            };

            JObject other = GetObject(source, "other");
            string otherName = GetString(other, "name");
            if (!string.IsNullOrWhiteSpace(otherName))
            {
                result.Add(
                    new WeaponProficiencyEntry
                    {
                        Group = WeaponGroup.Special,
                        CustomGroupName = otherName,
                        Rank = ParseRank(GetInt(other, "rank")),
                    }
                );
            }

            return result;
        }

        private List<RawRuleEntry> ParseRawRules(JArray rules)
        {
            var result = new List<RawRuleEntry>();
            if (rules == null)
                return result;

            foreach (JObject rule in rules.OfType<JObject>())
            {
                result.Add(
                    new RawRuleEntry
                    {
                        Key = GetString(rule, "key"),
                        Path = GetString(rule, "path"),
                        Mode = GetString(rule, "mode"),
                        Value = GetToken(rule, "value")?.ToString(Formatting.None) ?? "",
                        Uuid = GetString(rule, "uuid"),
                        Json = rule.ToString(Formatting.None),
                    }
                );
            }

            return result;
        }

        private void RegisterFeature(FeatureDataSO feature)
        {
            if (!string.IsNullOrWhiteSpace(feature.SourceId))
                featuresById[feature.SourceId] = feature;

            string key = NormalizeName(feature.DisplayName);
            if (!string.IsNullOrWhiteSpace(key))
                featuresByName[key] = feature;
        }

        private FeatureCategory ParseFeatureCategory(string value)
        {
            value = value?.ToLowerInvariant() ?? "";
            if (value.Contains("class"))
                return FeatureCategory.ClassFeature;
            if (value.Contains("skill"))
                return FeatureCategory.SkillFeat;
            if (value.Contains("general"))
                return FeatureCategory.GeneralFeat;
            return FeatureCategory.Unknown;
        }

        private AttributeType ParseAttribute(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "str":
                case "strength":
                    return AttributeType.Strength;
                case "dex":
                case "dexterity":
                    return AttributeType.Dexterity;
                case "con":
                case "constitution":
                    return AttributeType.Constitution;
                case "int":
                case "intelligence":
                    return AttributeType.Intelligence;
                case "wis":
                case "wisdom":
                    return AttributeType.Wisdom;
                case "cha":
                case "charisma":
                    return AttributeType.Charisma;
                default:
                    return AttributeType.Any;
            }
        }

        private SkillType ParseSkill(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "acrobatics":
                    return SkillType.Acrobatics;
                case "arcana":
                    return SkillType.Arcana;
                case "athletics":
                    return SkillType.Athletics;
                case "crafting":
                    return SkillType.Crafting;
                case "deception":
                    return SkillType.Deception;
                case "diplomacy":
                    return SkillType.Diplomacy;
                case "intimidation":
                    return SkillType.Intimidation;
                case "medicine":
                    return SkillType.Medicine;
                case "nature":
                    return SkillType.Nature;
                case "occultism":
                    return SkillType.Occultism;
                case "performance":
                    return SkillType.Performance;
                case "religion":
                    return SkillType.Religion;
                case "society":
                    return SkillType.Society;
                case "stealth":
                    return SkillType.Stealth;
                case "survival":
                    return SkillType.Survival;
                case "thievery":
                    return SkillType.Thievery;
                default:
                    return SkillType.Custom;
            }
        }

        private TacticsGame.Data.TacticsCore.CreatureSize ParseSize(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "tiny":
                case "tny":
                    return TacticsGame.Data.TacticsCore.CreatureSize.Tiny;
                case "sm":
                case "small":
                    return TacticsGame.Data.TacticsCore.CreatureSize.Small;
                case "lg":
                case "large":
                    return TacticsGame.Data.TacticsCore.CreatureSize.Large;
                case "huge":
                    return TacticsGame.Data.TacticsCore.CreatureSize.Huge;
                case "grg":
                case "gargantuan":
                    return TacticsGame.Data.TacticsCore.CreatureSize.Gargantuan;
                default:
                    return TacticsGame.Data.TacticsCore.CreatureSize.Medium;
            }
        }

        private (SenseType Type, string Custom) ParseSense(string value)
        {
            string normalized = (value ?? "").ToLowerInvariant();
            if (normalized.Contains("dark"))
                return (SenseType.Darkvision, value);
            if (normalized.Contains("low"))
                return (SenseType.LowLight, value);
            if (string.IsNullOrWhiteSpace(value) || normalized == "normal")
                return (SenseType.Normal, "");
            return (SenseType.Other, value);
        }

        private ProficiencyRank ParseRank(int value)
        {
            if (value <= 0)
                return ProficiencyRank.Untrained;
            if (value == 1)
                return ProficiencyRank.Trained;
            if (value == 2)
                return ProficiencyRank.Expert;
            if (value == 3)
                return ProficiencyRank.Master;
            return ProficiencyRank.Legendary;
        }

        private JObject GetObject(JToken token, string key)
        {
            return token?[key] as JObject;
        }

        private JArray GetArray(JToken token, string key)
        {
            return token?[key] as JArray;
        }

        private JToken GetToken(JObject obj, string key)
        {
            return obj != null && obj.TryGetValue(key, out JToken token) ? token : null;
        }

        private string GetString(JToken token, string key)
        {
            JToken value = token?[key];
            return value == null || value.Type == JTokenType.Null ? "" : value.ToString();
        }

        private int GetInt(JToken token, string key)
        {
            JToken value = token?[key];
            if (value == null || value.Type == JTokenType.Null)
                return 0;
            if (value.Type == JTokenType.Integer)
                return value.Value<int>();
            return int.TryParse(value.ToString(), out int parsed) ? parsed : 0;
        }

        private List<string> GetStringList(JToken token, string key)
        {
            JToken value = token?[key];
            if (value is JArray array)
                return array
                    .Where(item => item != null && item.Type != JTokenType.Null)
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();

            if (value == null || value.Type == JTokenType.Null)
                return new List<string>();

            string single = value.ToString();
            return string.IsNullOrWhiteSpace(single)
                ? new List<string>()
                : new List<string> { single };
        }

        private List<int> GetIntList(JToken token, string key)
        {
            JToken value = token?[key];
            if (value is JArray array)
                return array
                    .Select(item => int.TryParse(item.ToString(), out int parsed) ? parsed : 0)
                    .ToList();

            return new List<int>();
        }

        private string StripMarkup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            string withoutTags = Regex.Replace(text, "<.*?>", " ");
            withoutTags = Regex.Replace(withoutTags, @"@\w+\[[^\]]+\]\{([^}]*)\}", "$1");
            return Regex.Replace(withoutTags, @"\s+", " ").Trim();
        }

        private string ComputeHash(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private string SanitizeFileName(string value)
        {
            string sanitized = Regex.Replace(value ?? "", @"[^A-Za-z0-9_-]", "_");
            return string.IsNullOrWhiteSpace(sanitized) ? "asset" : sanitized;
        }

        private string Slugify(string value)
        {
            return Regex.Replace((value ?? "").ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        }

        private string NormalizeName(string value)
        {
            return Regex.Replace(value ?? "", @"[^a-zA-Z0-9]+", "").ToLowerInvariant();
        }

        private string ToDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return Regex.Replace(value.Replace("-", " "), @"\b\w", m => m.Value.ToUpperInvariant());
        }

        private string ExtractNameFromUuid(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                return "";

            int index = uuid.LastIndexOf("Item.", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? uuid.Substring(index + 5) : uuid.Split('.').LastOrDefault() ?? "";
        }

        private void MarkAsset(TacticsGameElementSO asset)
        {
            asset.BuildDerivedFields();
            EditorUtility.SetDirty(asset);
        }

        private static string DetectDefaultPacksRoot()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
                return "";

            foreach (string candidate in Directory.GetDirectories(desktop, "*-master"))
            {
                string packs = Path.Combine(candidate, "packs");
                if (
                    Directory.Exists(Path.Combine(packs, "ancestries"))
                    && Directory.Exists(Path.Combine(packs, "classes"))
                    && Directory.Exists(Path.Combine(packs, "backgrounds"))
                )
                {
                    return packs;
                }
            }

            return "";
        }

        private class ImportReport
        {
            public int Features;
            public int Ancestries;
            public int Heritages;
            public int Backgrounds;
            public int Classes;
            public int Skipped;
            public readonly List<string> Warnings = new List<string>();
            public readonly HashSet<string> UnresolvedReferences = new HashSet<string>();

            public void Reset()
            {
                Features = 0;
                Ancestries = 0;
                Heritages = 0;
                Backgrounds = 0;
                Classes = 0;
                Skipped = 0;
                Warnings.Clear();
                UnresolvedReferences.Clear();
            }

            public string BuildSummary()
            {
                var builder = new StringBuilder();
                builder.AppendLine("[Tactics Importer] Character creation data import complete.");
                builder.AppendLine($"Features: {Features}");
                builder.AppendLine($"Ancestries: {Ancestries}");
                builder.AppendLine($"Heritages: {Heritages}");
                builder.AppendLine($"Backgrounds: {Backgrounds}");
                builder.AppendLine($"Classes: {Classes}");
                builder.AppendLine($"Skipped: {Skipped}");
                builder.AppendLine($"Unresolved references: {UnresolvedReferences.Count}");
                builder.AppendLine($"Warnings: {Warnings.Count}");

                foreach (string warning in Warnings.Take(20))
                    builder.AppendLine($"- {warning}");

                foreach (string unresolved in UnresolvedReferences.Take(20))
                    builder.AppendLine($"- Unresolved: {unresolved}");

                return builder.ToString();
            }
        }
    }
}
#endif

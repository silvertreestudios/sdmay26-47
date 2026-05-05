#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data.TacticsRuleset;
using TacticsGame.Spells;
using TacticsGame.Spells.Effects;
using UnityEditor;
using UnityEngine;

namespace TacticsGame.Editor
{
    public class SpellFactoryWindow : EditorWindow
    {
        private struct SpellRecipe
        {
            public string Name;
            public int Level;
            public DamageType Element;
            public DamageType SecondaryElement;
            public AreaShape Shape;
            public int Radius;
            public int Range;
            public DiceFormula Damage;
            public DiceFormula SecondaryDamage;
            public SavingThrowType SaveType;
            public SpellDelivery Delivery;
            public ActionCost Cost;
            public string HeightenRules;
            public DiceFormula HeightenDamage;

            public SpellRecipe(
                string name,
                int level,
                DamageType element,
                AreaShape shape,
                int radius,
                int range,
                DiceFormula damage,
                SavingThrowType save,
                SpellDelivery delivery,
                ActionCost cost = ActionCost.Two,
                DiceFormula secondaryDamage = default,
                DamageType secondaryElement = DamageType.Untyped
            )
            {
                Name = name;
                Level = level;
                Element = element;
                SecondaryElement = secondaryElement;
                Shape = shape;
                Radius = radius;
                Range = range;
                Damage = damage;
                SecondaryDamage = secondaryDamage;
                SaveType = save;
                Delivery = delivery;
                Cost = cost;
                HeightenRules = "";
                HeightenDamage = default;
            }
        }

        private static List<SpellRecipe> _starterPack = new List<SpellRecipe>
        {
            new SpellRecipe(
                "Breathe Fire",
                1,
                DamageType.Fire,
                AreaShape.Cone,
                3,
                0,
                new DiceFormula(2, 6),
                SavingThrowType.Reflex,
                SpellDelivery.Instant,
                ActionCost.Two
            ),
            new SpellRecipe(
                "Thunderstrike",
                1,
                DamageType.Electricity,
                AreaShape.None,
                0,
                120,
                new DiceFormula(1, 12),
                SavingThrowType.Reflex,
                SpellDelivery.Instant,
                ActionCost.Two,
                new DiceFormula(1, 4),
                DamageType.Sonic
            ),
            new SpellRecipe(
                "Noise Blast",
                2,
                DamageType.Sonic,
                AreaShape.Burst,
                2,
                30,
                new DiceFormula(2, 10),
                SavingThrowType.Fortitude,
                SpellDelivery.Instant,
                ActionCost.Two
            ),
            new SpellRecipe(
                "Acid Grip",
                2,
                DamageType.Acid,
                AreaShape.None,
                0,
                120,
                new DiceFormula(2, 8),
                SavingThrowType.Reflex,
                SpellDelivery.Instant,
                ActionCost.Two
            ),
            new SpellRecipe(
                "Lightning Bolt",
                3,
                DamageType.Electricity,
                AreaShape.Line,
                24,
                0,
                new DiceFormula(4, 12),
                SavingThrowType.Reflex,
                SpellDelivery.Instant,
                ActionCost.Two
            ),
            new SpellRecipe(
                "Vampiric Feast",
                3,
                DamageType.Negative,
                AreaShape.None,
                0,
                5,
                new DiceFormula(6, 6),
                SavingThrowType.Fortitude,
                SpellDelivery.Instant,
                ActionCost.Two
            ),
            new SpellRecipe(
                "Divine Wrath",
                4,
                DamageType.Spirit,
                AreaShape.Burst,
                4,
                120,
                new DiceFormula(4, 10),
                SavingThrowType.Fortitude,
                SpellDelivery.Instant,
                ActionCost.Two
            ),
            new SpellRecipe(
                "Howling Blizzard",
                5,
                DamageType.Cold,
                AreaShape.Cone,
                12,
                0,
                new DiceFormula(10, 6),
                SavingThrowType.Reflex,
                SpellDelivery.Instant,
                ActionCost.Two
            ),
            new SpellRecipe(
                "Howling Blizzard (3-Action)",
                5,
                DamageType.Cold,
                AreaShape.Burst,
                6,
                500,
                new DiceFormula(10, 6),
                SavingThrowType.Reflex,
                SpellDelivery.Instant,
                ActionCost.Three
            ),
            new SpellRecipe(
                "Segmentation Fault",
                10,
                DamageType.Mental,
                AreaShape.None,
                0,
                500,
                new DiceFormula(404, 1),
                SavingThrowType.None,
                SpellDelivery.Instant,
                ActionCost.One
            ),
            new SpellRecipe(
                "sudo rm -rf --no-preserve-root /",
                10,
                DamageType.Force,
                AreaShape.Burst,
                50,
                0,
                new DiceFormula(99, 20, 999),
                SavingThrowType.None,
                SpellDelivery.Instant,
                ActionCost.Three
            ),
        };

        [MenuItem("Tools/Pathfinder/Spell Factory")]
        public static void ShowWindow()
        {
            GetWindow<SpellFactoryWindow>("Spell Factory");
        }

        private void OnGUI()
        {
            GUILayout.Label("PF2e Spell Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool generates a pack of 8 PF2e spells with automated VFX and logic chains.",
                MessageType.Info
            );

            if (GUILayout.Button("Generate PF2e Starter Pack", GUILayout.Height(40)))
            {
                GenerateStarterPack();
            }

            GUILayout.Space(20);
            GUILayout.Label("Individual Spells", EditorStyles.boldLabel);
            foreach (var recipe in _starterPack)
            {
                if (GUILayout.Button($"Generate {recipe.Name}"))
                {
                    GenerateSpell(recipe);
                }
            }
        }

        private void GenerateStarterPack()
        {
            int count = 0;
            foreach (var recipe in _starterPack)
            {
                GenerateSpell(recipe);
                count++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Generated {count} PF2e spells and VFX.", "OK");
        }

        private void GenerateSpell(SpellRecipe recipe)
        {
            string safeName = SanitizeName(recipe.Name);
            string baseFolder = "Assets/_Project/Prefabs/Spells";
            string spellFolder = $"{baseFolder}/{safeName}";

            if (!AssetDatabase.IsValidFolder(spellFolder))
            {
                if (!AssetDatabase.IsValidFolder(baseFolder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                        AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
                    AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Spells");
                }
                AssetDatabase.CreateFolder(baseFolder, safeName);
            }

            // 1. Generate VFX Prefabs
            GameObject castVFX = CreateVFXPrefab(
                recipe.Name,
                "Cast",
                recipe.Element,
                AreaShape.None,
                spellFolder
            );
            GameObject projVFX = null;
            if (recipe.Delivery == SpellDelivery.Projectile)
            {
                projVFX = CreateVFXPrefab(
                    recipe.Name,
                    "Proj",
                    recipe.Element,
                    AreaShape.None,
                    spellFolder
                );
            }
            GameObject hitVFX = CreateVFXPrefab(
                recipe.Name,
                "Hit",
                recipe.Element,
                recipe.Shape,
                spellFolder
            );

            // 2. Generate Effect SOs
            List<SpellEffectSO> effectList = new List<SpellEffectSO>();

            // Save Effect
            if (recipe.SaveType != SavingThrowType.None)
            {
                var saveEffect = CreateInstance<SavingThrowEffectSO>();
                saveEffect.SaveType = recipe.SaveType;
                saveEffect.name = $"{safeName}_Save";
                AssetDatabase.CreateAsset(saveEffect, $"{spellFolder}/{saveEffect.name}.asset");
                effectList.Add(saveEffect);
            }
            else if (
                recipe.Range > 0
                || recipe.Name.Contains("Shocking")
                || recipe.Name.Contains("Acid")
            )
            {
                // If no save and it's a ranged/touch spell, it likely needs an attack roll
                if (!recipe.Name.Contains("Missile"))
                {
                    var attackEffect = CreateInstance<SpellAttackEffectSO>();
                    attackEffect.name = $"{safeName}_Attack";
                    AssetDatabase.CreateAsset(
                        attackEffect,
                        $"{spellFolder}/{attackEffect.name}.asset"
                    );
                    effectList.Add(attackEffect);
                }
            }

            // Area Effect (Handles both Single Target and AoE)
            var areaEffect = CreateInstance<AreaEffectSO>();
            areaEffect.name = $"{safeName}_Area";
            AssetDatabase.CreateAsset(areaEffect, $"{spellFolder}/{areaEffect.name}.asset");
            effectList.Add(areaEffect);

            // Damage Effect
            var damageEffect = CreateInstance<DamageEffectSO>();
            damageEffect.IsBasicSave = (recipe.SaveType != SavingThrowType.None);
            damageEffect.IsSpellAttack = (
                recipe.SaveType == SavingThrowType.None && recipe.Range > 0
            );
            damageEffect.name = $"{safeName}_Damage";
            AssetDatabase.CreateAsset(damageEffect, $"{spellFolder}/{damageEffect.name}.asset");
            effectList.Add(damageEffect);

            // Secondary Damage (e.g. Thunderstrike Sonic)
            if (recipe.SecondaryDamage.DiceCount > 0)
            {
                var secondaryDamageEffect = CreateInstance<DamageEffectSO>();
                secondaryDamageEffect.IsBasicSave = damageEffect.IsBasicSave;
                secondaryDamageEffect.IsSpellAttack = damageEffect.IsSpellAttack;
                secondaryDamageEffect.OverrideDamage = recipe.SecondaryDamage;
                secondaryDamageEffect.OverrideDamageType = recipe.SecondaryElement;
                secondaryDamageEffect.name = $"{safeName}_SecondaryDamage";
                AssetDatabase.CreateAsset(
                    secondaryDamageEffect,
                    $"{spellFolder}/{secondaryDamageEffect.name}.asset"
                );
                effectList.Add(secondaryDamageEffect);
            }

            // Special Case: Acid Grip fully implemented
            if (recipe.Name.Contains("Acid Grip"))
            {
                // Interactive Drag
                var forcedMove = CreateInstance<ForcedMovementEffectSO>();
                forcedMove.Type = ForcedMovementEffectSO.MovementType.PullTowardsCaster;
                forcedMove.DistanceInFeet = 5; // Success = 5ft
                forcedMove.OnlyOnFailure = false; // Always happens on Success or worse
                forcedMove.IsInteractive = true;
                forcedMove.name = $"{safeName}_ForcedMove";
                AssetDatabase.CreateAsset(forcedMove, $"{spellFolder}/{forcedMove.name}.asset");
                effectList.Add(forcedMove);

                // Persistent Damage (1d6)
                var persistent = CreateInstance<PersistentDamageEffectSO>();
                persistent.Type = DamageType.Acid;
                persistent.DiceCount = 1;
                persistent.DiceFaces = 6;
                persistent.OnlyOnFailure = true;
                persistent.name = $"{safeName}_PersistentAcid";
                AssetDatabase.CreateAsset(persistent, $"{spellFolder}/{persistent.name}.asset");
                effectList.Add(persistent);
            }

            // Special Case: Divine Wrath conditions
            if (recipe.Name.Contains("Divine Wrath"))
            {
                var sickenedEffect = CreateInstance<ConditionEffectSO>();
                sickenedEffect.Condition = ConditionType.Sickened;
                sickenedEffect.ValueOnBadOutcome = 1;
                sickenedEffect.ValueOnWorstOutcome = 2; // Crit fail is sickened 2
                sickenedEffect.IsSaveSpell = true;
                sickenedEffect.name = $"{safeName}_Sickened";
                AssetDatabase.CreateAsset(
                    sickenedEffect,
                    $"{spellFolder}/{sickenedEffect.name}.asset"
                );
                effectList.Add(sickenedEffect);
            }

            // Special Case: Howling Blizzard push and difficult terrain
            if (recipe.Name.Contains("Howling Blizzard"))
            {
                // Push effect
                var forcedMove = CreateInstance<ForcedMovementEffectSO>();
                forcedMove.Type = ForcedMovementEffectSO.MovementType.PushFromCaster;
                forcedMove.DistanceInFeet = 5;
                forcedMove.OnlyOnFailure = true;
                forcedMove.name = $"{safeName}_Push";
                AssetDatabase.CreateAsset(forcedMove, $"{spellFolder}/{forcedMove.name}.asset");
                effectList.Add(forcedMove);

                // Terrain effect (snowdrifts)
                var terrainEffect = CreateInstance<TerrainModificationEffectSO>();
                terrainEffect.MovementCostOverride = 2; // Difficult terrain
                terrainEffect.AddToExistingCost = false;
                terrainEffect.HasDuration = true; // PF2e: "until the start of your next turn"
                terrainEffect.name = $"{safeName}_Snowdrifts";
                AssetDatabase.CreateAsset(
                    terrainEffect,
                    $"{spellFolder}/{terrainEffect.name}.asset"
                );
                effectList.Add(terrainEffect);
            }

            // Create Spell SO
            var spellSO = CreateInstance<SpellSO>();
            spellSO.name = recipe.Name;
            spellSO.ElementName = recipe.Name;
            spellSO.Slug = recipe.Name.ToLower().Replace(" ", "-");
            spellSO.Id = System.Guid.NewGuid().ToString();

            spellSO.Level = recipe.Level;
            spellSO.Cost = recipe.Cost;
            spellSO.ElementType = recipe.Element;
            spellSO.Targeting =
                recipe.Shape == AreaShape.None
                    ? SpellTargetingType.SingleTarget
                    : SpellTargetingType.Area;
            spellSO.Target = recipe.Shape == AreaShape.None ? TargetType.Creature : TargetType.Area;
            spellSO.Range = recipe.Range;
            spellSO.Area = new AreaDefinition { Shape = recipe.Shape, Radius = recipe.Radius };
            spellSO.BaseDamage = recipe.Damage;
            spellSO.DeliveryType = recipe.Delivery;
            spellSO.SaveType = recipe.SaveType;
            spellSO.SpellAttackRoll = (recipe.SaveType == SavingThrowType.None && recipe.Range > 0);

            spellSO.CastVFXPrefab = castVFX;
            spellSO.ProjectileVFXPrefab = projVFX;
            spellSO.HitVFXPrefab = hitVFX;

            foreach (var effect in effectList)
            {
                spellSO.Effects.Add(effect);
            }

            AssetDatabase.CreateAsset(spellSO, $"{spellFolder}/{safeName}.asset");
            Debug.Log($"Generated spell: {recipe.Name}");
        }

        private string SanitizeName(string name)
        {
            // Remove illegal characters for Unity asset paths
            string safe = name.Replace(" ", "_");
            safe = safe.Replace("/", "_");
            safe = safe.Replace("\\", "_");
            safe = safe.Replace(":", "_");
            safe = safe.Replace("*", "_");
            safe = safe.Replace("?", "_");
            safe = safe.Replace("\"", "_");
            safe = safe.Replace("<", "_");
            safe = safe.Replace(">", "_");
            safe = safe.Replace("|", "_");
            return safe;
        }

        private GameObject CreateVFXPrefab(
            string spellName,
            string type,
            DamageType element,
            AreaShape shape,
            string folder
        )
        {
            string safeSpellName = SanitizeName(spellName);
            GameObject go = new GameObject($"VFX_{type}_{safeSpellName}");
            var ps = go.AddComponent<ParticleSystem>();

            // Basic PS Setup
            var main = ps.main;
            main.startColor = GetColorForElement(element);
            main.startSize = 0.5f;
            main.startLifetime = 1.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            if (type == "Proj")
            {
                main.loop = true;
                // No stop action needed as SpellProjectile script handles destruction
            }
            else
            {
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.Destroy; // Auto-destroy when finished
            }

            var emission = ps.emission;
            if (type == "Hit")
            {
                emission.rateOverTime = 0;
                emission.SetBursts(
                    new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 50) }
                );
            }
            else
            {
                emission.rateOverTime = 20;
            }

            var shapeModule = ps.shape;
            if (type == "Hit")
            {
                switch (shape)
                {
                    case AreaShape.Burst:
                    case AreaShape.Emanation:
                        shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                        break;
                    case AreaShape.Cone:
                        shapeModule.shapeType = ParticleSystemShapeType.Cone;
                        shapeModule.angle = 45;
                        break;
                    case AreaShape.Line:
                        shapeModule.shapeType = ParticleSystemShapeType.Box;
                        shapeModule.scale = new Vector3(1, 1, 10);
                        break;
                    default:
                        shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                        break;
                }
            }

            // Apply a default material if possible
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Default-Particle.mat"
            );

            string prefabPath = $"{folder}/{go.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            DestroyImmediate(go);
            return prefab;
        }

        private Color GetColorForElement(DamageType element)
        {
            switch (element)
            {
                case DamageType.Fire:
                    return new Color(1, 0.4f, 0); // Orange
                case DamageType.Cold:
                    return new Color(0, 0.8f, 1); // Light Blue
                case DamageType.Acid:
                    return new Color(0.2f, 1, 0); // Lime Green
                case DamageType.Electricity:
                    return Color.yellow;
                case DamageType.Sonic:
                    return Color.white;
                case DamageType.Force:
                    return new Color(1, 0, 1); // Magenta
                case DamageType.Negative:
                    return new Color(0.2f, 0, 0.4f); // Purple/Black
                case DamageType.Mental:
                    return new Color(1, 0.6f, 0.8f); // Pink
                default:
                    return Color.grey;
            }
        }
    }
}
#endif

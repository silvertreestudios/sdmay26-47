using System.Collections.Generic;
using TacticsGame.Actions;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Spells
{
    public class AoEVisualizer : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("A simple Prefab containing a Quad or Cube with a semi-transparent material.")]
        [SerializeField]
        private GameObject highlightPrefab;

        [Header("Grid Alignment")]
        [SerializeField]
        private float yOffset = 0.05f;

        private List<GameObject> activeHighlights = new List<GameObject>();
        private Queue<GameObject> highlightPool = new Queue<GameObject>();

        private void Awake()
        {
            ServiceLocator.Register(this);
            Debug.Log(
                "<color=lime>[AoE Visualizer]</color> Registered AoEVisualizer service in ServiceLocator."
            );
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AoEVisualizer>();
        }

        public void UpdateAoEPreview(Vector3Int cursorPos, CastSpellAction spellAction)
        {
            if (spellAction == null || spellAction.GetCurrentSpell() == null)
            {
                Clear();
                return;
            }

            var spell = spellAction.GetCurrentSpell();
            if (spell.Area.Shape == Data.TacticsRuleset.AreaShape.None)
            {
                Clear();
                return;
            }

            List<Vector3Int> voxels = new List<Vector3Int>();

            Vector3Int casterPos =
                ServiceLocator.Get<UnitActionSystem>().SelectedUnit != null
                    ? ServiceLocator.Get<UnitActionSystem>().SelectedUnit.CurrentLayeredPosition
                    : cursorPos;

            Debug.Log(
                $"<color=cyan>[AoE Visualizer]</color> UpdateAoEPreview "
                    + $"cursorPos={cursorPos} | casterPos={casterPos} | shape={spell.Area.Shape} | radius={spell.Area.Radius}"
            );

            switch (spell.Area.Shape)
            {
                case Data.TacticsRuleset.AreaShape.Burst:
                    voxels = Combat.Spells.AoESolver.GetBurstVoxels(
                        cursorPos,
                        spell.Area.Radius * 5
                    );
                    break;
                case Data.TacticsRuleset.AreaShape.Emanation:
                    voxels = Combat.Spells.AoESolver.GetEmanationVoxels(
                        casterPos,
                        spell.Area.Radius * 5
                    );
                    break;
                case Data.TacticsRuleset.AreaShape.Cone:
                    voxels = Combat.Spells.AoESolver.GetConeVoxels(
                        casterPos,
                        spell.Area.Radius * 5,
                        cursorPos
                    );
                    break;
                case Data.TacticsRuleset.AreaShape.Line:
                    voxels = Combat.Spells.AoESolver.GetLineVoxels(
                        casterPos,
                        spell.Area.Radius * 5,
                        cursorPos
                    );
                    break;
            }

            string voxelList = string.Join(", ", voxels);
            Debug.Log(
                $"<color=cyan>[AoE Visualizer]</color> Generated {voxels.Count} voxels: [{voxelList}]"
            );

            UpdateAoEFootprint(voxels);
        }

        public void HidePreview()
        {
            Clear();
        }

        public void UpdateAoEFootprint(List<Vector3Int> activeVoxels)
        {
            if (highlightPrefab == null)
            {
                Debug.LogError(
                    "<color=red>[AoE Visualizer]</color> Highlight Prefab is missing! Assign a Quad Prefab in the Inspector."
                );
                return;
            }

            foreach (var obj in activeHighlights)
            {
                obj.SetActive(false);
                highlightPool.Enqueue(obj);
            }
            activeHighlights.Clear();

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3 scale = new Vector3(grid.CellSize, grid.CellSize, grid.CellSize);
            Quaternion flatRotation = Quaternion.Euler(90, 0, 0);

            foreach (var voxel in activeVoxels)
            {
                // Only render highlights for tiles that actually exist in the world
                // if (grid.GetNode(voxel) == null)
                //     continue;

                GameObject highlight;
                if (highlightPool.Count > 0)
                {
                    highlight = highlightPool.Dequeue();
                }
                else
                {
                    highlight = Instantiate(highlightPrefab, transform);
                }

                Vector3 worldPos = grid.GetWorldPosition(voxel) + new Vector3(0, yOffset, 0);
                highlight.transform.position = worldPos;
                highlight.transform.rotation = flatRotation;
                highlight.transform.localScale = scale;

                highlight.SetActive(true);
                activeHighlights.Add(highlight);
            }
        }

        public void Clear()
        {
            foreach (var obj in activeHighlights)
            {
                obj.SetActive(false);
                highlightPool.Enqueue(obj);
            }
            activeHighlights.Clear();
        }
    }
}

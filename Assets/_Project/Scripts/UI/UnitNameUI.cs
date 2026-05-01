using TacticsGame.Characters;
using TacticsGame.Core;
using TMPro;
using UnityEngine;

namespace TacticsGame.UI
{
    /// <summary>
    /// Displays the name of the currently selected unit.
    /// </summary>
    public class UnitNameUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI nameText;

        [Header("Aesthetics")]
        [SerializeField]
        private Color playerColor = new Color32(7, 111, 50, 255); // #076F32

        [SerializeField]
        private Color enemyColor = new Color32(165, 0, 3, 255); // #A50003

        [SerializeField]
        private Color neutralColor = new Color32(128, 128, 128, 255); // Gray

        [SerializeField]
        private Color rightColor = Color.white;

        private Color leftColor;

        private void Start()
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnSelectedUnitChanged += UnitActionSystem_OnSelectedUnitChanged;
                UpdateVisuals(uas.SelectedUnit);
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnSelectedUnitChanged -= UnitActionSystem_OnSelectedUnitChanged;
            }
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                UpdateVisuals(uas.SelectedUnit);
            }
        }

        private void UpdateVisuals(Unit selectedUnit)
        {
            if (nameText == null)
                return;

            if (selectedUnit == null)
            {
                nameText.text = "";
                return;
            }

            // Set the left color based on faction
            switch (selectedUnit.GetFaction())
            {
                case Faction.Player:
                    leftColor = playerColor;
                    break;
                case Faction.Enemy:
                    leftColor = enemyColor;
                    break;
                default:
                    leftColor = neutralColor;
                    break;
            }

            var stats = selectedUnit.GetStats();
            nameText.text = stats != null ? stats.GetUnitName() : selectedUnit.name;

            ApplyGradient();
        }

        private void ApplyGradient()
        {
            nameText.ForceMeshUpdate();
            TMP_TextInfo textInfo = nameText.textInfo;

            float minX = nameText.bounds.min.x;
            float maxX = nameText.bounds.max.x;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible)
                    continue;

                int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
                int vertexIndex = textInfo.characterInfo[i].vertexIndex;

                Color32[] colors = textInfo.meshInfo[matIndex].colors32;
                Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;

                for (int j = 0; j < 4; j++)
                {
                    float x = vertices[vertexIndex + j].x;
                    float t = Mathf.InverseLerp(minX, maxX, x);
                    colors[vertexIndex + j] = Color.Lerp(leftColor, rightColor, t);
                }
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.colors32 = textInfo.meshInfo[i].colors32;
                nameText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }
    }
}

using UnityEngine;

namespace TacticsGame.Grid
{
    public class AuraTile : MonoBehaviour
    {
        private MeshRenderer meshRenderer;
        private Material instanceMaterial;

        private void Awake()
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                instanceMaterial = new Material(meshRenderer.sharedMaterial);
                meshRenderer.material = instanceMaterial;
            }
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null)
                Destroy(instanceMaterial);
        }

        public void SetColor(Color color)
        {
            if (instanceMaterial != null)
                instanceMaterial.color = color;
        }
    }
}

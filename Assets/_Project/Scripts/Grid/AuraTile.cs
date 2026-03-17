using UnityEngine;

namespace PathfinderTactics.Grid
{
    public class AuraTile : MonoBehaviour
    {
        private MeshRenderer meshRenderer;

        private void Awake()
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        public void SetColor(Color color)
        {
            if (meshRenderer != null)
            {
                Material mat = new Material(meshRenderer.material);
                mat.color = color;
                meshRenderer.material = mat;
            }
        }
    }
}

using UnityEngine;

/// <summary>
/// Attaches a PNG to a humanoid unit's head bone.
/// Generates a procedurally curved mesh to allow the mask to "wrap" around the face.
/// </summary>
[ExecuteAlways]
public class FaceMaskComponent : MonoBehaviour
{
    [Header("Mask Appearance")]
    public Sprite maskSprite;
    public Color maskColor = Color.white;

    [Range(-1f, 1f)]
    [Tooltip(
        "How much the mask bends to wrap around the face. Positive values wrap around, negative values bulge out."
    )]
    public float curvature = 0.2f;

    [Header("Positioning (Relative to Head)")]
    public Vector3 localPositionOffset = new Vector3(0f, 0.12f, 0.15f);
    public Vector3 localRotationOffset = Vector3.zero;
    public Vector3 maskScale = new Vector3(0.25f, 0.25f, 0.25f);

    [Header("Technical Settings")]
    [Range(2, 20)]
    public int meshSegments = 10;
    public Transform headBone;

    private GameObject maskObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh generatedMesh;
    private Material maskMaterial;

    private void Start()
    {
        InitializeMask();
    }

    private void OnEnable()
    {
        InitializeMask();
    }

    private void Update()
    {
        if (maskObject == null || meshFilter == null)
        {
            InitializeMask();
        }
        else
        {
            UpdateMaskVisuals();
            UpdateCurvedMesh();
        }
    }

    public void InitializeMask()
    {
        if (headBone == null)
        {
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }

            // Fallback: Search hierarchy for "Head" if humanoid check fails or isn't ready
            if (headBone == null)
            {
                headBone = FindRecursive(transform, "Head");
            }

            if (headBone == null)
            {
                return;
            }
        }

        if (maskObject == null)
        {
            Transform existing = headBone.Find("AttachedFaceMask");
            if (existing != null)
            {
                maskObject = existing.gameObject;
            }
            else
            {
                maskObject = new GameObject("AttachedFaceMask");
                maskObject.transform.SetParent(headBone);
            }
        }

        meshFilter = maskObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = maskObject.AddComponent<MeshFilter>();

        meshRenderer = maskObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = maskObject.AddComponent<MeshRenderer>();

        meshRenderer.enabled = true;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // Create a basic transparent material if none exists
        if (maskMaterial == null)
        {
            // Try to find URP shaders first, then fallback to standard
            string[] shaderNames =
            {
                "Sprites/Default",
                "Universal Render Pipeline/2D/Sprite-Unlit",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Unlit/Transparent",
            };

            Shader shader = null;
            foreach (var sName in shaderNames)
            {
                shader = Shader.Find(sName);
                if (shader != null)
                    break;
            }

            if (shader == null)
            {
                Debug.LogError(
                    "[FaceMask] Could not find any suitable shader for the mask material!"
                );
                return;
            }

            maskMaterial = new Material(shader);
            maskMaterial.hideFlags = HideFlags.DontSave;

            // FORCE URP TRANSPARENCY SETTINGS
            if (shader.name.Contains("Universal Render Pipeline"))
            {
                maskMaterial.SetFloat("_Surface", 1); // 1 = Transparent
                maskMaterial.SetFloat("_Blend", 0); // 0 = Alpha Blend
                maskMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                maskMaterial.SetInt(
                    "_DstBlend",
                    (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                );
                maskMaterial.SetInt("_ZWrite", 0);
                maskMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                maskMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (shader.name == "Sprites/Default" || shader.name == "Unlit/Transparent")
            {
                maskMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        // Ensure the mask is on the same layer as the head bone so the camera sees it
        maskObject.layer = headBone.gameObject.layer;

        meshRenderer.sharedMaterial = maskMaterial;
        UpdateMaskVisuals();
        UpdateCurvedMesh();
    }

    private void UpdateMaskVisuals()
    {
        if (maskObject == null)
            return;

        maskObject.transform.localPosition = localPositionOffset;
        maskObject.transform.localRotation = Quaternion.Euler(localRotationOffset);
        maskObject.transform.localScale = maskScale;

        if (maskMaterial != null)
        {
            if (maskSprite != null)
            {
                // Set both legacy and URP texture properties
                if (maskMaterial.HasProperty("_BaseMap"))
                    maskMaterial.SetTexture("_BaseMap", maskSprite.texture);

                maskMaterial.mainTexture = maskSprite.texture;
                maskMaterial.color = maskColor;
            }
        }
    }

    private void UpdateCurvedMesh()
    {
        if (meshFilter == null)
            return;

        // Only regenerate if properties changed (simple check)
        int vertexCount = (meshSegments + 1) * 2;
        if (generatedMesh == null || generatedMesh.vertexCount != vertexCount)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "CurvedMaskMesh";
            generatedMesh.hideFlags = HideFlags.DontSave;
            meshFilter.mesh = generatedMesh;
        }

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[meshSegments * 6];

        float width = 1f;
        float height = 1f;

        for (int i = 0; i <= meshSegments; i++)
        {
            float t = (float)i / meshSegments;
            float x = (t - 0.5f) * width;

            // Calculate Z offset based on curvature (parabolic curve)
            // As x goes from -0.5 to 0.5, z goes from -curvature to 0 back to -curvature
            float z = -Mathf.Pow(x * 2f, 2f) * curvature;

            // Bottom vertex
            vertices[i * 2] = new Vector3(x, -0.5f * height, z);
            uv[i * 2] = new Vector2(t, 0f);

            // Top vertex
            vertices[i * 2 + 1] = new Vector3(x, 0.5f * height, z);
            uv[i * 2 + 1] = new Vector2(t, 1f);

            if (i < meshSegments)
            {
                int baseIdx = i * 6;
                int vertIdx = i * 2;
                triangles[baseIdx] = vertIdx;
                triangles[baseIdx + 1] = vertIdx + 1;
                triangles[baseIdx + 2] = vertIdx + 2;
                triangles[baseIdx + 3] = vertIdx + 2;
                triangles[baseIdx + 4] = vertIdx + 1;
                triangles[baseIdx + 5] = vertIdx + 3;
            }
        }

        generatedMesh.vertices = vertices;
        generatedMesh.uv = uv;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
    }

    public void EquipMask(Sprite newSprite)
    {
        maskSprite = newSprite;
        UpdateMaskVisuals();
    }

    public void ForceRefresh()
    {
        InitializeMask();
        UpdateMaskVisuals();
        UpdateCurvedMesh();
    }

    private Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name.Contains(name, System.StringComparison.OrdinalIgnoreCase))
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindRecursive(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (maskObject != null)
        {
            if (Application.isPlaying)
                Destroy(maskObject);
            else
                DestroyImmediate(maskObject);
        }
        if (generatedMesh != null)
        {
            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);
        }
    }

    private void OnDisable()
    {
        // Removed Destroy from here to prevent masks vanishing during story transitions
    }
}

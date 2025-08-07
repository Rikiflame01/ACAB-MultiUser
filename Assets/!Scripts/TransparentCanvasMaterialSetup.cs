using UnityEngine;

/// <summary>
/// Helper script to setup a transparent material for the NetworkCanvas
/// This script can be used to create or configure materials for transparent painting
/// </summary>
public class TransparentCanvasMaterialSetup : MonoBehaviour
{
    [Header("Material Setup")]
    [SerializeField] private Material targetMaterial;
    [SerializeField] private bool autoSetupOnStart = true;

    private void Start()
    {
        if (autoSetupOnStart && targetMaterial != null)
        {
            SetupTransparentMaterial(targetMaterial);
        }
    }

    [ContextMenu("Setup Transparent Material")]
    public void SetupTransparentMaterialFromEditor()
    {
        if (targetMaterial != null)
        {
            SetupTransparentMaterial(targetMaterial);
        }
        else
        {
            Debug.LogWarning("No target material assigned!");
        }
    }

    public static void SetupTransparentMaterial(Material material)
    {
        if (material == null) return;

        // Set rendering mode to transparent
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3); // Transparent mode
        }

        // Configure blend modes for transparency
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0); // Disable Z-write for transparency
        }

        // Set render queue for transparency
        material.renderQueue = 3000; // Transparent queue

        // Enable appropriate keywords for transparency
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // Ensure the material uses alpha
        if (material.HasProperty("_Color"))
        {
            Color currentColor = material.color;
            currentColor.a = 1f; // Ensure full alpha for painted areas
            material.color = currentColor;
        }
    }

    /// <summary>
    /// Creates a new transparent material from a standard material
    /// </summary>
    public static Material CreateTransparentMaterial(Material baseMaterial = null)
    {
        Material newMaterial;
        
        if (baseMaterial != null)
        {
            newMaterial = new Material(baseMaterial);
        }
        else
        {
            // Create with Standard shader
            newMaterial = new Material(Shader.Find("Standard"));
        }

        SetupTransparentMaterial(newMaterial);
        return newMaterial;
    }
}

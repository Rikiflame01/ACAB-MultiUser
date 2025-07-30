using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

public class CanvasRaycast : MonoBehaviour
{
    private int canvasLayerMask;
    private float debugLineDuration = 2.0f;

    [SerializeField] private Transform raycastOriginPoint;
    [SerializeField] private float debugLineWidth = 0.05f;
    [SerializeField] private InputActionReference selectActionReference;
    public Color markColor = Color.red;
    public float markSize = 20f;

    [Header("Preview Settings")]
    [SerializeField] private bool showPreview = true;
    [SerializeField] private Material previewMaterial;
    [SerializeField] private float previewOpacity = 0.5f;
    [SerializeField] private bool previewFollowsAim = true;
    [SerializeField] private float previewScaleMultiplier = 0.001f; // Adjust this to match your canvas scale
    [SerializeField] private bool useAutomaticScaling = true; // Use automatic pixel-to-world calculation
    [SerializeField] private bool debugScaling = false; // Show debug info for scaling

    private LineRenderer continuousLine;
    private GameObject previewDecal; // Changed from previewCircle to previewDecal
    private MeshRenderer previewRenderer;
    private bool isAimingAtCanvas = false;
    private Vector3 lastHitPoint;
    private Vector3 lastHitNormal;
    private Vector2 lastHitUV; // Store UV coordinates for preview
    private NetworkCanvas currentTargetCanvas; // Store reference to canvas being aimed at

    void Start()
    {
        canvasLayerMask = LayerMask.GetMask("Canvas");

        if (raycastOriginPoint == null) Debug.LogWarning("RaycastOriginPoint not assigned!");
        if (selectActionReference?.action == null) Debug.LogWarning("Select Action Reference invalid!");
        else selectActionReference.action.Enable();

        SetupContinuousLine();
        // Note: Canvas preview is handled directly on the canvas texture
    }

    void OnDestroy()
    {
        if (selectActionReference?.action != null)
            selectActionReference.action.Disable();
    }

    void Update()
    {
        bool isEquipped = selectActionReference?.action?.IsPressed() ?? false;
        continuousLine.enabled = isEquipped;

        // Always cast ray to detect canvas
        CastRayFromObjectPosition();

        // Only paint when actually pressing the button
        if (isEquipped && isAimingAtCanvas)
        {
            // Hide preview while painting to avoid conflicts
            HideCanvasPreview();
            PaintAtLastHitPoint();
        }
    }

    public void CastRayFromPoint(Vector3 raycastOrigin, Vector3 raycastDirection)
    {
        Ray ray = new Ray(raycastOrigin, raycastDirection);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, canvasLayerMask))
        {
            Vector3 end = raycastOrigin + raycastDirection * hit.distance;
            UpdateContinuousLine(raycastOrigin, end, Color.red);
            
            // Store hit information for preview and painting
            lastHitPoint = hit.point;
            lastHitNormal = hit.normal;
            lastHitUV = hit.textureCoord;
            isAimingAtCanvas = true;
            
            // Get the canvas component for preview
            NetworkCanvas networkCanvas = hit.collider.GetComponent<NetworkCanvas>();
            if (networkCanvas != null)
            {
                currentTargetCanvas = networkCanvas;
            }
            
            // Only show preview when NOT currently painting
            bool isPainting = selectActionReference?.action?.IsPressed() ?? false;
            if (!isPainting)
            {
                // Update preview position and visibility
                UpdateCanvasPreview(hit.textureCoord, hit.point, hit.normal);
            }
            
            // Reduced debug spam - only log occasionally
            if (Time.frameCount % 60 == 0) // Log once per second at 60fps
            {
                Debug.Log($"Aiming at: {hit.collider.gameObject.name} at {hit.point}");
            }
        }
        else
        {
            Vector3 end = raycastOrigin + raycastDirection * 1000f;
            UpdateContinuousLine(raycastOrigin, end, Color.white);
            
            // Hide preview when not aiming at canvas
            isAimingAtCanvas = false;
            currentTargetCanvas = null;
            HideCanvasPreview();
            
            // Reduced debug spam
            if (Time.frameCount % 120 == 0) // Log every 2 seconds at 60fps
            {
                Debug.Log("Raycast missed Canvas layer");
            }
        }
    }

    public void CastRayFromObjectPosition()
    {
        if (raycastOriginPoint == null)
        {
            Debug.LogError("RaycastOriginPoint not assigned!");
            return;
        }
        Vector3 origin = raycastOriginPoint.position;
        Vector3 direction = -raycastOriginPoint.up;
        CastRayFromPoint(origin, direction);
    }

    private void SetupContinuousLine()
    {
        GameObject lineObj = new GameObject("ContinuousRayLine") { transform = { parent = transform } };
        continuousLine = lineObj.AddComponent<LineRenderer>();
        continuousLine.startWidth = continuousLine.endWidth = debugLineWidth;
        continuousLine.positionCount = 2;
        continuousLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        continuousLine.startColor = continuousLine.endColor = Color.white;
        continuousLine.material.color = Color.white;
        continuousLine.enabled = false;
        Debug.Log("Continuous line setup complete.");
    }

    private void UpdateContinuousLine(Vector3 start, Vector3 end, Color color)
    {
        continuousLine.SetPosition(0, start);
        continuousLine.SetPosition(1, end);
        continuousLine.startColor = continuousLine.endColor = color;
        continuousLine.material.color = color;
    }

    /// <summary>
    /// Updates the canvas preview directly on the canvas texture
    /// </summary>
    private void UpdateCanvasPreview(Vector2 uv, Vector3 worldPos, Vector3 normal)
    {
        if (!showPreview || currentTargetCanvas == null) return;

        // Get the canvas texture
        Texture2D canvasTexture = currentTargetCanvas.GetCanvasTexture();
        if (canvasTexture == null) return;

        // Store original pixels to restore later
        StoreOriginalPixelsForPreview(uv, canvasTexture);

        // Draw preview on canvas
        DrawPreviewOnCanvas(uv, canvasTexture);

        if (debugScaling && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Preview updated at UV: {uv}, World: {worldPos}");
        }
    }

    /// <summary>
    /// Hides the canvas preview by restoring original pixels
    /// </summary>
    private void HideCanvasPreview()
    {
        if (currentTargetCanvas != null && storedPreviewPixels != null)
        {
            RestoreOriginalPixels();
        }
    }

    // Preview pixel storage for restoration
    private Color[] storedPreviewPixels;
    private Vector2 lastPreviewUV;
    private int lastPreviewSize;

    /// <summary>
    /// Stores the original pixels that will be overwritten by preview
    /// </summary>
    private void StoreOriginalPixelsForPreview(Vector2 uv, Texture2D canvasTexture)
    {
        int centerX = (int)(uv.x * canvasTexture.width);
        int centerY = (int)(uv.y * canvasTexture.height);
        int brushRadius = (int)markSize;

        // If we're at the same location with same size, don't need to store again
        if (Vector2.Distance(uv, lastPreviewUV) < 0.001f && brushRadius == lastPreviewSize)
            return;

        // Restore previous preview area first
        if (storedPreviewPixels != null)
        {
            RestoreOriginalPixels();
        }

        // Calculate preview area
        int diameter = brushRadius * 2;
        List<Color> pixelList = new List<Color>();
        List<Vector2Int> positionList = new List<Vector2Int>();

        // Store pixels in the brush area
        for (int x = -brushRadius; x <= brushRadius; x++)
        {
            for (int y = -brushRadius; y <= brushRadius; y++)
            {
                if (x * x + y * y <= brushRadius * brushRadius)
                {
                    int pixelX = Mathf.Clamp(centerX + x, 0, canvasTexture.width - 1);
                    int pixelY = Mathf.Clamp(centerY + y, 0, canvasTexture.height - 1);
                    
                    pixelList.Add(canvasTexture.GetPixel(pixelX, pixelY));
                    positionList.Add(new Vector2Int(pixelX, pixelY));
                }
            }
        }

        storedPreviewPixels = pixelList.ToArray();
        storedPreviewPositions = positionList.ToArray();
        lastPreviewUV = uv;
        lastPreviewSize = brushRadius;
    }

    private Vector2Int[] storedPreviewPositions;

    /// <summary>
    /// Draws the preview on the canvas texture
    /// </summary>
    private void DrawPreviewOnCanvas(Vector2 uv, Texture2D canvasTexture)
    {
        int centerX = (int)(uv.x * canvasTexture.width);
        int centerY = (int)(uv.y * canvasTexture.height);
        int brushRadius = (int)markSize;

        // Create preview color (semi-transparent version of brush color)
        Color previewColor = markColor;
        previewColor.a = previewOpacity;

        // Draw preview brush
        for (int x = -brushRadius; x <= brushRadius; x++)
        {
            for (int y = -brushRadius; y <= brushRadius; y++)
            {
                if (x * x + y * y <= brushRadius * brushRadius)
                {
                    int pixelX = Mathf.Clamp(centerX + x, 0, canvasTexture.width - 1);
                    int pixelY = Mathf.Clamp(centerY + y, 0, canvasTexture.height - 1);
                    
                    // Get original pixel
                    Color originalPixel = canvasTexture.GetPixel(pixelX, pixelY);
                    
                    // Blend preview color with original
                    Color blendedColor = Color.Lerp(originalPixel, previewColor, previewOpacity);
                    
                    canvasTexture.SetPixel(pixelX, pixelY, blendedColor);
                }
            }
        }

        canvasTexture.Apply();
    }

    /// <summary>
    /// Restores the original pixels, removing the preview
    /// </summary>
    private void RestoreOriginalPixels()
    {
        if (currentTargetCanvas == null || storedPreviewPixels == null || storedPreviewPositions == null)
            return;

        Texture2D canvasTexture = currentTargetCanvas.GetCanvasTexture();
        if (canvasTexture == null) return;

        // Restore all stored pixels
        for (int i = 0; i < storedPreviewPixels.Length; i++)
        {
            Vector2Int pos = storedPreviewPositions[i];
            canvasTexture.SetPixel(pos.x, pos.y, storedPreviewPixels[i]);
        }

        canvasTexture.Apply();
        
        // Clear stored data
        storedPreviewPixels = null;
        storedPreviewPositions = null;
    }

    private void UpdatePreview(Vector3 position, Vector3 normal)
    {
        if (!showPreview || currentTargetCanvas == null) 
        {
            if (!showPreview) Debug.Log("Preview disabled");
            if (currentTargetCanvas == null) Debug.Log("Target canvas is null");
            return;
        }

        Debug.Log($"Updating preview at position: {position}");
        
        // Update the canvas preview using stored UV coordinates
        UpdateCanvasPreview(lastHitUV, position, normal);
        
        Debug.Log($"Canvas preview updated");
    }

    private void HidePreview()
    {
        // Restore original canvas pixels if we have them stored
        RestoreOriginalPixels();
    }

    private void UpdatePreviewAppearance()
    {
        // This method now uses canvas-based preview instead of 3D objects
        // Canvas preview is handled by UpdateCanvasPreview method
        Debug.Log("UpdatePreviewAppearance called - using canvas-based preview");
    }

    private void PaintAtLastHitPoint()
    {
        if (!isAimingAtCanvas) return;

        // Cast ray again to get fresh hit info for painting
        Vector3 origin = raycastOriginPoint.position;
        Vector3 direction = -raycastOriginPoint.up;
        Ray ray = new Ray(origin, direction);
        
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, canvasLayerMask))
        {
            NetworkCanvas networkCanvas = hit.collider.GetComponent<NetworkCanvas>();
            if (networkCanvas != null)
            {
                // Use the unified Paint method that handles both offline and online modes
                networkCanvas.Paint(hit.textureCoord, markColor, (int)markSize);
            }
            else
            {
                Debug.LogWarning("No NetworkCanvas on hit object!");
            }
        }
    }

    /// <summary>
    /// Public methods to update preview settings at runtime
    /// </summary>
    public void SetPreviewColor(Color color)
    {
        markColor = color;
        UpdatePreviewAppearance();
    }

    public void SetPreviewSize(float size)
    {
        markSize = size;
        UpdatePreviewAppearance();
    }

    public void SetPreviewOpacity(float opacity)
    {
        previewOpacity = Mathf.Clamp01(opacity);
        UpdatePreviewAppearance();
    }

    public void TogglePreview(bool enabled)
    {
        showPreview = enabled;
        // Canvas-based preview will be updated automatically when aiming at canvas
        Debug.Log($"Preview toggled: {enabled}");
    }

    /// <summary>
    /// Enable or disable automatic scaling calculation
    /// </summary>
    public void SetAutomaticScaling(bool enabled)
    {
        useAutomaticScaling = enabled;
        UpdatePreviewAppearance();
    }

    /// <summary>
    /// Manually adjust the scale multiplier for fine-tuning
    /// </summary>
    public void SetScaleMultiplier(float multiplier)
    {
        previewScaleMultiplier = multiplier;
        UpdatePreviewAppearance();
    }

    /// <summary>
    /// Enable debug logging for scaling calculations
    /// </summary>
    public void SetDebugScaling(bool enabled)
    {
        debugScaling = enabled;
    }

    /// <summary>
    /// Force recalculate preview scale (useful after changing canvas properties)
    /// </summary>
    public void RecalculatePreviewScale()
    {
        UpdatePreviewAppearance();
    }

    /// <summary>
    /// Get current preview world scale for debugging
    /// </summary>
    public float GetCurrentPreviewScale()
    {
        // Canvas-based preview doesn't use world scale, return brush size instead
        return markSize;
    }

    /// <summary>
    /// Force show preview at a test position (for debugging)
    /// </summary>
    [ContextMenu("Test Show Preview")]
    public void TestShowPreview()
    {
        if (currentTargetCanvas == null)
        {
            Debug.LogError("No target canvas found! Aim at a canvas first.");
            return;
        }

        // Test canvas preview by triggering an update
        if (lastHitUV != Vector2.zero)
        {
            UpdateCanvasPreview(lastHitUV, Vector3.zero, Vector3.up);
            Debug.Log($"Test canvas preview shown at UV: {lastHitUV}");
        }
        else
        {
            Debug.LogWarning("No valid UV coordinates stored. Aim at canvas first.");
        }
    }

    /// <summary>
    /// Check if preview system is properly initialized
    /// </summary>
    [ContextMenu("Debug Preview Status")]
    public void DebugPreviewStatus()
    {
        Debug.Log($"Show Preview: {showPreview}");
        Debug.Log($"Current target canvas: {currentTargetCanvas != null}");
        Debug.Log($"Is aiming at canvas: {isAimingAtCanvas}");
        Debug.Log($"Raycast origin assigned: {raycastOriginPoint != null}");
        Debug.Log($"Last hit UV: {lastHitUV}");
        Debug.Log($"Stored preview pixels: {(storedPreviewPixels != null ? storedPreviewPixels.Length : 0)}");
        Debug.Log($"Stored preview positions: {(storedPreviewPositions != null ? storedPreviewPositions.Length : 0)}");
        
        if (currentTargetCanvas != null)
        {
            Debug.Log($"Canvas texture size: {currentTargetCanvas.GetCanvasTexture().width}x{currentTargetCanvas.GetCanvasTexture().height}");
        }
    }
}
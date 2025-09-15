using UnityEngine;
using Unity.Netcode;
using System.Linq;
using Unity.XR.CoreUtils;

/// <summary>
/// Controls color opacity based on distance from player to nearest canvas.
/// Attach this to the player and it will automatically adjust the painting color opacity
/// based on distance to canvas objects. Only affects the local player's opacity settings.
/// </summary>
public class DistanceBasedOpacityController : NetworkBehaviour
{
    [Header("Distance Settings")]
    [SerializeField, Tooltip("Distance at which opacity reaches its minimum value")]
    private float maxDistance = 10f;
    
    [SerializeField, Tooltip("Distance at which opacity reaches its maximum value")]
    private float minDistance = 1f;
    
    [SerializeField, Tooltip("Minimum opacity value (0.0 to 1.0)")]
    [Range(0f, 1f)]
    private float minOpacity = 0.1f;
    
    [SerializeField, Tooltip("Maximum opacity value (0.0 to 1.0)")]
    [Range(0f, 1f)]
    private float maxOpacity = 1f;
    
    [SerializeField, Tooltip("How smoothly the opacity changes (lower = smoother)")]
    private float smoothingSpeed = 5f;
    
    [Header("Canvas Detection")]
    [SerializeField, Tooltip("Tag to identify canvas objects (default: 'Canvas')")]
    private string canvasTag = "Canvas";
    
    [SerializeField, Tooltip("Layer mask for canvas objects (alternative to tag-based detection)")]
    private LayerMask canvasLayerMask = -1;
    
    [SerializeField, Tooltip("Use layer mask instead of tag for canvas detection")]
    private bool useLayerMask = false;
    
    [SerializeField, Tooltip("Maximum distance to search for canvas objects")]
    private float maxSearchDistance = 50f;
    
    [Header("Color System References")]
    [SerializeField, Tooltip("Reference to the color wheel (auto-found if null)")]
    private ColorWheel colorWheel;
    
    [SerializeField, Tooltip("Reference to the canvas raycast (auto-found if null)")]
    private CanvasRaycast canvasRaycast;
    
    [Header("Player Detection")]
    [SerializeField, Tooltip("Transform to use as player position (auto-detected if null)")]
    private Transform playerTransform;
    
    [SerializeField, Tooltip("Use XR head position if available")]
    private bool useXRHeadPosition = true;
    
    [Header("Debug")]
    [SerializeField, Tooltip("Show debug information in console")]
    private bool enableDebugLogging = false;
    
    [SerializeField, Tooltip("Show visual debug lines to nearest canvas")]
    private bool showDebugLines = false;
    
    [SerializeField, Tooltip("Color for debug lines")]
    private Color debugLineColor = Color.yellow;
    
    // Private variables
    private float currentTargetOpacity = 1f;
    private float currentActualOpacity = 1f;
    private GameObject[] canvasObjects;
    private GameObject nearestCanvas;
    private float nearestCanvasDistance = float.MaxValue;
    private Transform xrHeadTransform;
    
    // Update frequency control
    private float lastUpdateTime = 0f;
    private float updateInterval = 0.1f; // Update 10 times per second to reduce overhead
    
    private void Start()
    {
        // Only run on the local player to avoid affecting other players' opacity
        if (!IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            enabled = false;
            return;
        }
        
        InitializeReferences();
        FindCanvasObjects();
    }
    
    private void Update()
    {
        // Only run on the local player
        if (!IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;
            
        // Rate limit updates to improve performance
        if (Time.time - lastUpdateTime < updateInterval)
            return;
            
        lastUpdateTime = Time.time;
        
        UpdateNearestCanvas();
        UpdateOpacity();
    }
    
    /// <summary>
    /// Initialize component references
    /// </summary>
    private void InitializeReferences()
    {
        // Find player transform
        if (playerTransform == null)
        {
            if (useXRHeadPosition)
            {
                // Try to find XR head position
                var xrOrigin = FindFirstObjectByType<XROrigin>();
                if (xrOrigin != null && xrOrigin.Camera != null)
                {
                    xrHeadTransform = xrOrigin.Camera.transform;
                    playerTransform = xrHeadTransform;
                    if (enableDebugLogging)
                        Debug.Log("DistanceBasedOpacityController: Using XR head position");
                }
            }
            
            // Fallback to this object's transform
            if (playerTransform == null)
            {
                playerTransform = transform;
                if (enableDebugLogging)
                    Debug.Log("DistanceBasedOpacityController: Using attached object transform");
            }
        }
        
        // Find color wheel
        if (colorWheel == null)
        {
            colorWheel = FindFirstObjectByType<ColorWheel>();
            if (colorWheel == null && enableDebugLogging)
            {
                Debug.LogWarning("DistanceBasedOpacityController: No ColorWheel found in scene!");
            }
        }
        
        // Find canvas raycast
        if (canvasRaycast == null)
        {
            canvasRaycast = FindFirstObjectByType<CanvasRaycast>();
            if (canvasRaycast == null && enableDebugLogging)
            {
                Debug.LogWarning("DistanceBasedOpacityController: No CanvasRaycast found in scene!");
            }
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"DistanceBasedOpacityController initialized - ColorWheel: {colorWheel != null}, CanvasRaycast: {canvasRaycast != null}");
            Debug.Log($"Distance-based opacity system ready. CanvasRaycast will query opacity when painting.");
        }
    }
    
    /// <summary>
    /// Find all canvas objects in the scene
    /// </summary>
    private void FindCanvasObjects()
    {
        if (useLayerMask)
        {
            // Find by layer mask
            var allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            canvasObjects = allColliders
                .Where(c => ((1 << c.gameObject.layer) & canvasLayerMask) != 0)
                .Select(c => c.gameObject)
                .ToArray();
        }
        else
        {
            // Find by tag
            canvasObjects = GameObject.FindGameObjectsWithTag(canvasTag);
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"DistanceBasedOpacityController: Found {canvasObjects.Length} canvas objects");
        }
    }
    
    /// <summary>
    /// Update which canvas is nearest to the player
    /// </summary>
    private void UpdateNearestCanvas()
    {
        if (canvasObjects == null || canvasObjects.Length == 0 || playerTransform == null)
        {
            nearestCanvas = null;
            nearestCanvasDistance = float.MaxValue;
            return;
        }
        
        float closestDistance = float.MaxValue;
        GameObject closestCanvas = null;
        
        Vector3 playerPosition = playerTransform.position;
        
        foreach (var canvas in canvasObjects)
        {
            if (canvas == null) continue;
            
            // Get closest point on canvas to player
            Vector3 closestPoint = GetClosestPointOnCanvas(canvas, playerPosition);
            float distance = Vector3.Distance(playerPosition, closestPoint);
            
            // Only consider canvases within search range
            if (distance <= maxSearchDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closestCanvas = canvas;
            }
        }
        
        nearestCanvas = closestCanvas;
        nearestCanvasDistance = closestDistance;
        
        if (enableDebugLogging && nearestCanvas != null)
        {
            Debug.Log($"Nearest canvas: {nearestCanvas.name} at distance: {nearestCanvasDistance:F2}");
        }
    }
    
    /// <summary>
    /// Get the closest point on a canvas to the player position
    /// </summary>
    private Vector3 GetClosestPointOnCanvas(GameObject canvas, Vector3 playerPosition)
    {
        // Try to get renderer bounds for more accurate distance
        var renderer = canvas.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.ClosestPoint(playerPosition);
        }
        
        // Try to get collider bounds (only for supported collider types)
        var collider = canvas.GetComponent<Collider>();
        if (collider != null)
        {
            // Check if it's a supported collider type for ClosestPoint
            if (collider is BoxCollider || collider is SphereCollider || 
                collider is CapsuleCollider || (collider is MeshCollider meshCollider && meshCollider.convex))
            {
                return collider.ClosestPoint(playerPosition);
            }
            else
            {
                // For unsupported colliders, use bounds
                return collider.bounds.ClosestPoint(playerPosition);
            }
        }
        
        // Fallback to canvas transform position
        return canvas.transform.position;
    }
    
    /// <summary>
    /// Update the opacity based on distance to nearest canvas
    /// </summary>
    private void UpdateOpacity()
    {
        // Calculate target opacity based on distance
        if (nearestCanvas == null || nearestCanvasDistance >= maxSearchDistance)
        {
            currentTargetOpacity = minOpacity;
        }
        else
        {
            // Clamp distance to our range
            float clampedDistance = Mathf.Clamp(nearestCanvasDistance, minDistance, maxDistance);
            
            // Calculate opacity (inverse relationship - closer = more opaque)
            float normalizedDistance = (clampedDistance - minDistance) / (maxDistance - minDistance);
            currentTargetOpacity = Mathf.Lerp(maxOpacity, minOpacity, normalizedDistance);
        }
        
        // Smooth the opacity change
        currentActualOpacity = Mathf.Lerp(currentActualOpacity, currentTargetOpacity, Time.deltaTime * smoothingSpeed);
        
        // Apply opacity to color systems
        ApplyOpacityToColorSystems();
        
        // Debug visualization
        if (showDebugLines && nearestCanvas != null)
        {
            Vector3 playerPos = playerTransform.position;
            Vector3 canvasPos = GetClosestPointOnCanvas(nearestCanvas, playerPos);
            Debug.DrawLine(playerPos, canvasPos, debugLineColor, updateInterval * 1.1f);
        }
    }
    
    /// <summary>
    /// Calculate and store the opacity based on distance to nearest canvas.
    /// The CanvasRaycast will query this opacity when painting.
    /// </summary>
    private void ApplyOpacityToColorSystems()
    {
        // No longer directly controlling other systems
        // CanvasRaycast will call GetCurrentOpacity() when painting
        // This keeps the systems decoupled and avoids fighting over control
    }
    
    /// <summary>
    /// Force refresh of canvas objects (useful if canvases are spawned at runtime)
    /// </summary>
    public void RefreshCanvasObjects()
    {
        FindCanvasObjects();
    }
    
    /// <summary>
    /// Set new distance thresholds
    /// </summary>
    public void SetDistanceThresholds(float newMinDistance, float newMaxDistance)
    {
        minDistance = Mathf.Max(0.1f, newMinDistance);
        maxDistance = Mathf.Max(minDistance + 0.1f, newMaxDistance);
    }
    
    /// <summary>
    /// Set new opacity range
    /// </summary>
    public void SetOpacityRange(float newMinOpacity, float newMaxOpacity)
    {
        minOpacity = Mathf.Clamp01(newMinOpacity);
        maxOpacity = Mathf.Clamp01(newMaxOpacity);
    }
    
    /// <summary>
    /// Get current distance to nearest canvas
    /// </summary>
    public float GetNearestCanvasDistance()
    {
        return nearestCanvasDistance;
    }
    
    /// <summary>
    /// Get current calculated opacity
    /// </summary>
    public float GetCurrentOpacity()
    {
        return currentActualOpacity;
    }
    
    /// <summary>
    /// Get the nearest canvas object
    /// </summary>
    public GameObject GetNearestCanvas()
    {
        return nearestCanvas;
    }
    
    /// <summary>
    /// Enable or disable the opacity controller
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
        
        if (!enabled)
        {
            // Restore full opacity when disabled
            currentTargetOpacity = 1f;
            currentActualOpacity = 1f;
            ApplyOpacityToColorSystems();
        }
    }
    
    /// <summary>
    /// Override the automatic opacity with a manual value
    /// </summary>
    public void SetManualOpacity(float opacity)
    {
        currentTargetOpacity = Mathf.Clamp01(opacity);
        currentActualOpacity = currentTargetOpacity;
        ApplyOpacityToColorSystems();
    }
    
    // Network compatibility - ensure this only runs on local player
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Only enable on the local player
        if (!IsOwner)
        {
            enabled = false;
        }
    }
    
    private void OnValidate()
    {
        // Ensure valid ranges
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
        minOpacity = Mathf.Clamp01(minOpacity);
        maxOpacity = Mathf.Clamp01(maxOpacity);
        updateInterval = Mathf.Max(0.01f, updateInterval);
        smoothingSpeed = Mathf.Max(0.1f, smoothingSpeed);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || playerTransform == null) return;
        
        // Draw distance ranges
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerTransform.position, minDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(playerTransform.position, maxDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(playerTransform.position, maxSearchDistance);
        
        // Draw line to nearest canvas
        if (nearestCanvas != null)
        {
            Gizmos.color = debugLineColor;
            Vector3 playerPos = playerTransform.position;
            Vector3 canvasPos = GetClosestPointOnCanvas(nearestCanvas, playerPos);
            Gizmos.DrawLine(playerPos, canvasPos);
            
            // Draw a cube at the nearest point
            Gizmos.DrawCube(canvasPos, Vector3.one * 0.1f);
        }
    }
    
    // Context menu helpers for testing
    [ContextMenu("Force Refresh Canvas Objects")]
    private void ForceRefreshCanvasObjects()
    {
        RefreshCanvasObjects();
    }
    
    [ContextMenu("Log Current Status")]
    private void LogCurrentStatus()
    {
        Debug.Log($"=== Distance-Based Opacity Controller Status ===");
        Debug.Log($"Is Owner: {IsOwner}");
        Debug.Log($"Enabled: {enabled}");
        Debug.Log($"Canvas Objects Found: {canvasObjects?.Length ?? 0}");
        Debug.Log($"Nearest Canvas: {nearestCanvas?.name ?? "None"}");
        Debug.Log($"Distance: {nearestCanvasDistance:F2}");
        Debug.Log($"Target Opacity: {currentTargetOpacity:F2}");
        Debug.Log($"Actual Opacity: {currentActualOpacity:F2}");
        Debug.Log($"ColorWheel Found: {colorWheel != null}");
        Debug.Log($"CanvasRaycast Found: {canvasRaycast != null}");
        Debug.Log($"Player Transform: {playerTransform?.name ?? "None"}");
    }
    
    [ContextMenu("Test Min Opacity")]
    private void TestMinOpacity()
    {
        SetManualOpacity(minOpacity);
        Debug.Log($"Set opacity to minimum: {minOpacity}");
    }
    
    [ContextMenu("Test Max Opacity")]
    private void TestMaxOpacity()
    {
        SetManualOpacity(maxOpacity);
        Debug.Log($"Set opacity to maximum: {maxOpacity}");
    }
    
    [ContextMenu("Resume Automatic Opacity")]
    private void ResumeAutomaticOpacity()
    {
        // Re-enable automatic updates
        SetEnabled(true);
        Debug.Log("Resumed automatic opacity control");
    }
}

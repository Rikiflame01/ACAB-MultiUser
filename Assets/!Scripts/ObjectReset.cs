using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

/// <summary>
/// Script that automatically resets an object's position and rotation when it gets too far 
/// from the player or falls below a certain Y position. 
/// Works with both VR (XR Origin) and desktop (Camera) setups.
/// </summary>
public class ObjectReset : MonoBehaviour
{
    [Header("Reset Conditions")]
    [SerializeField, Tooltip("Maximum distance from player before reset")]
    private float maxDistanceFromPlayer = 10f;
    
    [SerializeField, Tooltip("Minimum Y position before reset")]
    private float minYPosition = -5f;
    
    [Header("Reset Transform")]
    [SerializeField, Tooltip("Transform to reset to. If null, uses the object's initial transform")]
    private Transform resetTransform;
    
    [Header("Player Detection")]
    [SerializeField, Tooltip("Manually assign the player transform. If null, will auto-detect")]
    private Transform playerTransformOverride;
    
    [SerializeField, Tooltip("Enable automatic player detection")]
    private bool useAutoDetection = true;
    
    [SerializeField, Tooltip("Prefer XR Origin for VR, fallback to Camera if not found")]
    private bool preferXROrigin = true;
    
    [Header("Reset Settings")]
    [SerializeField, Tooltip("Check interval in seconds")]
    private float checkInterval = 0.1f;
    
    [SerializeField, Tooltip("Delay before reset (useful for objects that might be thrown)")]
    private float resetDelay = 0.5f;
    
    [SerializeField, Tooltip("Reset velocity and angular velocity (if Rigidbody exists)")]
    private bool resetPhysics = true;
    
    [SerializeField, Tooltip("Enable debug logging")]
    private bool enableDebugLogging = false;
    
    // Private variables
    private Transform playerTransform;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private float nextCheckTime;
    private bool isResetting = false;
    private Rigidbody objectRigidbody;
    
    void Start()
    {
        // Store initial transform values
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
        
        // Get Rigidbody component if it exists
        objectRigidbody = GetComponent<Rigidbody>();
        
        // Find player reference
        FindPlayerReference();
        
        // Set initial check time
        nextCheckTime = Time.time + checkInterval;
        
        if (enableDebugLogging)
        {
            Debug.Log($"ObjectReset initialized on {gameObject.name}. Max distance: {maxDistanceFromPlayer}, Min Y: {minYPosition}");
        }
    }
    
    void Update()
    {
        // Only check at intervals to improve performance
        if (Time.time >= nextCheckTime && !isResetting)
        {
            CheckResetConditions();
            nextCheckTime = Time.time + checkInterval;
        }
    }
    
    /// <summary>
    /// Finds the player reference, prioritizing XR Origin if preferXROrigin is true
    /// </summary>
    private void FindPlayerReference()
    {
        // Use manual override if provided
        if (playerTransformOverride != null)
        {
            playerTransform = playerTransformOverride;
            if (enableDebugLogging)
                Debug.Log($"ObjectReset: Using manual player override: {playerTransformOverride.name}");
            return;
        }
        
        // Try auto-detection if enabled
        if (useAutoDetection && preferXROrigin)
        {
            // Try to find XR Origin by name first (most compatible approach)
            GameObject xrOriginObject = GameObject.Find("XR Origin") ?? 
                                       GameObject.Find("XROrigin") ?? 
                                       GameObject.Find("XR Rig") ??
                                       GameObject.Find("XRRig");
            
            if (xrOriginObject != null)
            {
                XROrigin xrOrigin = xrOriginObject.GetComponent<XROrigin>();
                if (xrOrigin != null)
                {
                    playerTransform = xrOrigin.transform;
                    if (enableDebugLogging)
                        Debug.Log($"ObjectReset: Found XR Origin: {xrOrigin.name}");
                    return;
                }
            }
            
            // Fallback: try to find any XROrigin component
            XROrigin[] xrOrigins = FindObjectsByType<XROrigin>(FindObjectsSortMode.None);
            if (xrOrigins.Length > 0)
            {
                playerTransform = xrOrigins[0].transform;
                if (enableDebugLogging)
                    Debug.Log($"ObjectReset: Found XR Origin: {xrOrigins[0].name}");
                return;
            }
        }
        
        // Fallback to main camera if auto-detection is enabled
        if (useAutoDetection)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerTransform = mainCamera.transform;
                if (enableDebugLogging)
                    Debug.Log($"ObjectReset: Using Main Camera: {mainCamera.name}");
                return;
            }
            
            // Last resort - find any camera
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0)
            {
                playerTransform = cameras[0].transform;
                if (enableDebugLogging)
                    Debug.Log($"ObjectReset: Using fallback camera: {cameras[0].name}");
                return;
            }
        }
        
        Debug.LogError($"ObjectReset on {gameObject.name}: Could not find player reference!");
    }
    
    /// <summary>
    /// Checks if the object needs to be reset based on distance and Y position
    /// </summary>
    private void CheckResetConditions()
    {
        if (playerTransform == null)
        {
            if (enableDebugLogging)
                Debug.LogWarning($"ObjectReset on {gameObject.name}: No player reference found, trying to find again...");
            FindPlayerReference();
            return;
        }
        
        // Check Y position condition
        if (transform.position.y <= minYPosition)
        {
            if (enableDebugLogging)
                Debug.Log($"ObjectReset: {gameObject.name} fell below minimum Y position ({minYPosition}). Resetting...");
            StartReset("Y position");
            return;
        }
        
        // Check distance condition
        float distanceFromPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceFromPlayer > maxDistanceFromPlayer)
        {
            if (enableDebugLogging)
                Debug.Log($"ObjectReset: {gameObject.name} too far from player ({distanceFromPlayer:F1}m > {maxDistanceFromPlayer}m). Resetting...");
            StartReset("distance");
            return;
        }
    }
    
    /// <summary>
    /// Initiates the reset process with a delay
    /// </summary>
    private void StartReset(string reason)
    {
        if (isResetting) return;
        
        isResetting = true;
        
        if (resetDelay > 0)
        {
            Invoke(nameof(PerformReset), resetDelay);
        }
        else
        {
            PerformReset();
        }
        
        if (enableDebugLogging)
            Debug.Log($"ObjectReset: Started reset for {gameObject.name} due to {reason}");
    }
    
    /// <summary>
    /// Performs the actual reset of the object
    /// </summary>
    private void PerformReset()
    {
        // Determine target transform
        Vector3 targetPosition;
        Quaternion targetRotation;
        Vector3 targetScale;
        
        if (resetTransform != null)
        {
            targetPosition = resetTransform.position;
            targetRotation = resetTransform.rotation;
            targetScale = resetTransform.localScale;
        }
        else
        {
            targetPosition = initialPosition;
            targetRotation = initialRotation;
            targetScale = initialScale;
        }
        
        // Reset transform
        transform.position = targetPosition;
        transform.rotation = targetRotation;
        transform.localScale = targetScale;
        
        // Reset physics if enabled and Rigidbody exists
        if (resetPhysics && objectRigidbody != null)
        {
            objectRigidbody.linearVelocity = Vector3.zero;
            objectRigidbody.angularVelocity = Vector3.zero;
        }
        
        isResetting = false;
        
        if (enableDebugLogging)
        {
            string resetLocation = resetTransform != null ? resetTransform.name : "initial position";
            Debug.Log($"ObjectReset: Reset {gameObject.name} to {resetLocation}");
        }
    }
    
    /// <summary>
    /// Manually trigger a reset
    /// </summary>
    public void ManualReset()
    {
        if (enableDebugLogging)
            Debug.Log($"ObjectReset: Manual reset triggered for {gameObject.name}");
        StartReset("manual trigger");
    }
    
    /// <summary>
    /// Set a new reset transform
    /// </summary>
    public void SetResetTransform(Transform newResetTransform)
    {
        resetTransform = newResetTransform;
        if (enableDebugLogging)
            Debug.Log($"ObjectReset: Reset transform updated to {(newResetTransform != null ? newResetTransform.name : "null")}");
    }
    
    /// <summary>
    /// Set the maximum distance from player
    /// </summary>
    public void SetMaxDistance(float distance)
    {
        maxDistanceFromPlayer = Mathf.Max(0.1f, distance);
        if (enableDebugLogging)
            Debug.Log($"ObjectReset: Max distance updated to {maxDistanceFromPlayer}");
    }
    
    /// <summary>
    /// Set the minimum Y position
    /// </summary>
    public void SetMinYPosition(float yPosition)
    {
        minYPosition = yPosition;
        if (enableDebugLogging)
            Debug.Log($"ObjectReset: Min Y position updated to {minYPosition}");
    }
    
    /// <summary>
    /// Set the player transform manually
    /// </summary>
    public void SetPlayerReference(Transform player)
    {
        playerTransformOverride = player;
        FindPlayerReference();
        if (enableDebugLogging)
            Debug.Log($"ObjectReset: Player reference updated to {(player != null ? player.name : "null")}");
    }
    
    /// <summary>
    /// Get the current distance from player
    /// </summary>
    public float GetDistanceFromPlayer()
    {
        if (playerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, playerTransform.position);
    }
    
    /// <summary>
    /// Check if the object is currently being reset
    /// </summary>
    public bool IsResetting => isResetting;
    
    void OnValidate()
    {
        // Clamp values to reasonable ranges
        maxDistanceFromPlayer = Mathf.Max(0.1f, maxDistanceFromPlayer);
        checkInterval = Mathf.Max(0.01f, checkInterval);
        resetDelay = Mathf.Max(0f, resetDelay);
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw reset conditions as gizmos in the editor
        if (playerTransform != null)
        {
            // Draw distance sphere around player
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerTransform.position, maxDistanceFromPlayer);
        }
        
        // Draw minimum Y plane
        Gizmos.color = Color.red;
        Vector3 objectPos = transform.position;
        Vector3 planeCenter = new Vector3(objectPos.x, minYPosition, objectPos.z);
        Gizmos.DrawWireCube(planeCenter, new Vector3(5f, 0.1f, 5f));
        
        // Draw reset position if available
        if (resetTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(resetTransform.position, 0.5f);
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(initialPosition, 0.5f);
        }
    }
}
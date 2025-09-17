using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

/// <summary>
/// Script that positions a target object in front of the player when a button is pressed.
/// Works with both VR (XR Origin) and desktop (Main Camera) setups.
/// </summary>
public class PositionInFrontOfPlayer : MonoBehaviour
{
    [Header("Button Reference")]
    [SerializeField] private Button positionButton;
    
    [Header("Target Object")]
    [SerializeField] private Transform targetObject;
    [Tooltip("If null, will use this transform as the target")]
    
    [Header("Position Settings")]
    [SerializeField] private float distanceFromPlayer = 2.0f;
    [SerializeField] private float heightOffset = 0.0f;
    [Tooltip("Additional height offset from player's eye level")]
    
    [Header("Rotation Settings")]
    [SerializeField] private bool facePlayer = true;
    [Tooltip("Should the object face towards the player?")]
    [SerializeField] private bool keepOriginalRotation = false;
    [Tooltip("Keep the object's current rotation instead of facing player")]
    
    [Header("Player Detection")]
    [SerializeField] private Transform playerTransformOverride;
    [Tooltip("Manually assign the player transform (XR Origin, Camera, etc.). If null, will auto-detect")]
    [SerializeField] private bool useAutoDetection = true;
    [Tooltip("Enable automatic player detection. Disable if you want to use only the manual override")]
    [SerializeField] private bool preferXROrigin = true;
    [Tooltip("When auto-detecting, try to find XR Origin first for VR, fallback to Camera if not found")]
    
    [Header("Animation")]
    [SerializeField] private bool animateMovement = true;
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private Transform playerTransform;
    private Transform cameraTransform;
    private XROrigin xrOrigin;
    
    void Start()
    {
        // Auto-assign button if not set
        if (positionButton == null)
            positionButton = GetComponent<Button>();
            
        // Auto-assign target object if not set
        if (targetObject == null)
            targetObject = transform;
        
        // Setup button click listener
        if (positionButton != null)
        {
            positionButton.onClick.AddListener(PositionObjectInFrontOfPlayer);
        }
        else
        {
            Debug.LogWarning($"PositionInFrontOfPlayer on {gameObject.name}: No button assigned. Call PositionObjectInFrontOfPlayer() manually.");
        }
        
        // Find player reference
        FindPlayerReference();
    }
    
    /// <summary>
    /// Finds the player reference, prioritizing XR Origin if useXROrigin is true
    /// </summary>
    private void FindPlayerReference()
    {
        // Use manual override if provided
        if (playerTransformOverride != null)
        {
            playerTransform = playerTransformOverride;
            cameraTransform = playerTransformOverride;
            Debug.Log($"Using manual player override: {playerTransformOverride.name}");
            return;
        }
        
        // Try auto-detection if enabled
        if (useAutoDetection && preferXROrigin)
        {
            xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin != null)
            {
                playerTransform = xrOrigin.transform;
                // Get the camera from XR Origin
                cameraTransform = xrOrigin.Camera?.transform ?? xrOrigin.transform;
                Debug.Log($"Found XR Origin: {xrOrigin.name}");
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
                cameraTransform = mainCamera.transform;
                Debug.Log($"Using Main Camera: {mainCamera.name}");
                return;
            }
            
            // Last resort - find any camera
            Camera anyCamera = FindFirstObjectByType<Camera>();
            if (anyCamera != null)
            {
                playerTransform = anyCamera.transform;
                cameraTransform = anyCamera.transform;
                Debug.Log($"Using fallback camera: {anyCamera.name}");
                return;
            }
        }
        
        Debug.LogError("PositionInFrontOfPlayer: Could not find player reference (XR Origin or Camera)!");
    }
    
    /// <summary>
    /// Public method to position the object in front of the player
    /// </summary>
    public void PositionObjectInFrontOfPlayer()
    {
        if (playerTransform == null || cameraTransform == null)
        {
            Debug.LogWarning("PositionInFrontOfPlayer: No player reference found. Trying to find player again...");
            FindPlayerReference();
            
            if (playerTransform == null || cameraTransform == null)
            {
                Debug.LogError("PositionInFrontOfPlayer: Still no player reference found!");
                return;
            }
        }
        
        if (targetObject == null)
        {
            Debug.LogError("PositionInFrontOfPlayer: No target object assigned!");
            return;
        }
        
        // Calculate position in front of player
        Vector3 playerPosition = playerTransform.position;
        Vector3 forwardDirection = cameraTransform.forward;
        
        // Calculate target position
        Vector3 targetPosition = playerPosition + (forwardDirection * distanceFromPlayer);
        targetPosition.y = playerPosition.y + heightOffset;
        
        // Set specific rotation - Vector3(0, 74.9999924, 270) in Euler angles
        Quaternion targetRotation = Quaternion.Euler(0f, 74.9999924f, 270f);
        
        // Override with face player rotation if enabled and not keeping original rotation
        if (facePlayer && !keepOriginalRotation)
        {
            Vector3 lookDirection = (playerPosition - targetPosition).normalized;
            targetRotation = Quaternion.LookRotation(lookDirection);
        }
        
        // Apply position and rotation
        if (animateMovement && gameObject.activeInHierarchy)
        {
            StartCoroutine(AnimateToPosition(targetPosition, targetRotation));
        }
        else
        {
            targetObject.position = targetPosition;
            if (facePlayer && !keepOriginalRotation)
            {
                targetObject.rotation = targetRotation;
            }
        }
        
        Debug.Log($"Positioned {targetObject.name} in front of player at distance {distanceFromPlayer}m");
    }
    
    /// <summary>
    /// Animate the object to the target position and rotation
    /// </summary>
    private System.Collections.IEnumerator AnimateToPosition(Vector3 targetPosition, Quaternion targetRotation)
    {
        Vector3 startPosition = targetObject.position;
        Quaternion startRotation = targetObject.rotation;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < animationDuration)
        {
            float t = elapsedTime / animationDuration;
            float curveValue = animationCurve.Evaluate(t);
            
            // Interpolate position
            targetObject.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            
            // Interpolate rotation if facing player
            if (facePlayer && !keepOriginalRotation)
            {
                targetObject.rotation = Quaternion.Lerp(startRotation, targetRotation, curveValue);
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure we end up exactly at the target
        targetObject.position = targetPosition;
        if (facePlayer && !keepOriginalRotation)
        {
            targetObject.rotation = targetRotation;
        }
    }
    
    /// <summary>
    /// Set a new target object to position
    /// </summary>
    public void SetTargetObject(Transform newTarget)
    {
        targetObject = newTarget;
    }
    
    /// <summary>
    /// Set the distance from player
    /// </summary>
    public void SetDistance(float newDistance)
    {
        distanceFromPlayer = Mathf.Max(0.1f, newDistance);
    }
    
    /// <summary>
    /// Set the height offset
    /// </summary>
    public void SetHeightOffset(float offset)
    {
        heightOffset = offset;
    }
    
    /// <summary>
    /// Override the player reference manually
    /// </summary>
    public void SetPlayerReference(Transform player)
    {
        playerTransformOverride = player;
        FindPlayerReference();
    }
    
    void OnValidate()
    {
        // Clamp distance to reasonable values
        distanceFromPlayer = Mathf.Max(0.1f, distanceFromPlayer);
        animationDuration = Mathf.Max(0.01f, animationDuration);
    }
}
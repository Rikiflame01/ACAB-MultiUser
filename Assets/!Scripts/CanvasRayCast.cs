using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class CanvasRaycast : MonoBehaviour
{
    private int canvasLayerMask;
    private float debugLineDuration = 2.0f;

    [SerializeField] private Transform raycastOriginPoint;
    [SerializeField] private float debugLineWidth = 0.05f;
    [SerializeField] private InputActionReference selectActionReference;
    [SerializeField] private InputActionReference leftHandSelectActionReference; // Left hand trigger reference
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

    [Header("Hand Detection")]
    [SerializeField] private bool isPaintingHand = true; // Set to true for right hand, false for left hand
    [SerializeField] private bool autoDetectHand = true; // Automatically detect which hand this is attached to
    [SerializeField] private HandType handType = HandType.Unknown; // Detected or manually set hand type
    
    [Header("Equipment State")]
    [SerializeField] private bool isGraffitiCanEquipped = false; // Whether the graffiti can is currently equipped/grabbed
    [SerializeField] private bool requireEquipmentForRaycast = true; // Whether to require equipment for any functionality

    // Hand type enumeration
    public enum HandType
    {
        Unknown,
        LeftHand,
        RightHand
    }

    // References for hand detection
    private XRInputModalityManager xrModalityManager;
    private XRRayInteractor rayInteractor; // Store reference to the XR Ray Interactor on this hand
    private InputActionReference dynamicSelectActionReference; // Dynamic reference based on which hand is holding

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
        
        if (leftHandSelectActionReference?.action == null) Debug.LogWarning("Left Hand Select Action Reference not assigned!");
        else leftHandSelectActionReference.action.Enable();

        // Since this script is on the graffiti can, we need to check grab state
        XRGrabInteractable grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
        {
            // Subscribe to grab events to detect when picked up/dropped
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
            
            // Check if already grabbed at start
            if (grabInteractable.isSelected)
            {
                isGraffitiCanEquipped = true;
                if (autoDetectHand)
                {
                    DetectHandType();
                }
            }
        }
        else
        {
            Debug.LogError("CanvasRaycast script requires XRGrabInteractable component on the graffiti can!");
        }

        // Only setup raycast functionality if this is being held by the painting hand
        if (ShouldEnableRaycastFunctionality())
        {
            SetupContinuousLine();
        }
    }

    void OnDestroy()
    {
        if (selectActionReference?.action != null)
            selectActionReference.action.Disable();
        if (leftHandSelectActionReference?.action != null)
            leftHandSelectActionReference.action.Disable();
    }

    void Update()
    {
        // Early exit if this is not the painting hand or equipment is not available
        if (!ShouldEnableRaycastFunctionality())
        {
            // Ensure raycast visuals are hidden when not active
            if (continuousLine != null)
                continuousLine.enabled = false;
            HideCanvasPreview();
            isAimingAtCanvas = false;
            currentTargetCanvas = null;
            return;
        }

        bool isEquipped = GetCurrentSelectActionReference()?.action?.IsPressed() ?? false;
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

    #region Hand Detection and Equipment Management

    /// <summary>
    /// Detects which hand is currently holding this graffiti can by checking XRGrabInteractable
    /// </summary>
    private void DetectHandType()
    {
        // Find XRInputModalityManager for hand references
        xrModalityManager = FindFirstObjectByType<XRInputModalityManager>();
        
        // Get the XRGrabInteractable component on this graffiti can
        XRGrabInteractable grabInteractable = GetComponent<XRGrabInteractable>();
        
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            // Get the interactor that's currently grabbing this can
            var interactor = grabInteractable.interactorsSelecting[0];
            
            if (xrModalityManager != null)
            {
                // Check if the grabbing interactor is the left or right controller
                if (IsChildOf(interactor.transform, xrModalityManager.leftController?.transform))
                {
                    handType = HandType.LeftHand;
                    isPaintingHand = true; // Whichever hand is holding the can becomes the painting hand
                    rayInteractor = interactor.transform.GetComponent<XRRayInteractor>();
                    
                    // Try to find the correct input action for the left controller
                    UpdateInputActionReference(interactor.transform);
                }
                else if (IsChildOf(interactor.transform, xrModalityManager.rightController?.transform))
                {
                    handType = HandType.RightHand;
                    isPaintingHand = true; // Whichever hand is holding the can becomes the painting hand
                    rayInteractor = interactor.transform.GetComponent<XRRayInteractor>();
                    
                    // Try to find the correct input action for the right controller
                    UpdateInputActionReference(interactor.transform);
                }
            }
            
            // Fallback: Check interactor parent names for common naming conventions
            if (handType == HandType.Unknown)
            {
                Transform current = interactor.transform;
                while (current != null)
                {
                    string name = current.name.ToLower();
                    if (name.Contains("left"))
                    {
                        handType = HandType.LeftHand;
                        isPaintingHand = true; // Whichever hand is holding the can becomes the painting hand
                        rayInteractor = current.GetComponent<XRRayInteractor>();
                        if (rayInteractor == null) rayInteractor = current.GetComponentInChildren<XRRayInteractor>();
                        
                        // Try to find the correct input action for the left controller
                        UpdateInputActionReference(current);
                        
                        break;
                    }
                    else if (name.Contains("right"))
                    {
                        handType = HandType.RightHand;
                        isPaintingHand = true; // Whichever hand is holding the can becomes the painting hand
                        rayInteractor = current.GetComponent<XRRayInteractor>();
                        if (rayInteractor == null) rayInteractor = current.GetComponentInChildren<XRRayInteractor>();
                        
                        // Try to find the correct input action for the right controller
                        UpdateInputActionReference(current);
                        
                        break;
                    }
                    current = current.parent;
                }
            }
        }
        else
        {
            // Graffiti can is not currently being held
            handType = HandType.Unknown;
            isPaintingHand = false;
            rayInteractor = null;
        }
        
        if (handType == HandType.Unknown && grabInteractable != null && grabInteractable.isSelected)
        {
            Debug.LogWarning("Could not detect which hand is holding the graffiti can! Please ensure proper controller hierarchy naming.");
        }
    }

    /// <summary>
    /// Helper method to check if this transform is a child of the specified parent
    /// </summary>
    private bool IsChildOf(Transform childTransform, Transform parentTransform)
    {
        if (parentTransform == null || childTransform == null) return false;
        
        Transform current = childTransform;
        while (current != null)
        {
            if (current == parentTransform)
                return true;
            current = current.parent;
        }
        return false;
    }

    /// <summary>
    /// Helper method to check if this transform is a child of the specified parent (original method)
    /// </summary>
    private bool IsChildOf(Transform parentTransform)
    {
        return IsChildOf(transform, parentTransform);
    }

    /// <summary>
    /// Determines if raycast functionality should be enabled based on hand type and equipment state
    /// </summary>
    private bool ShouldEnableRaycastFunctionality()
    {
        // Only enable on the painting hand
        if (!isPaintingHand)
            return false;

        // Check equipment requirement
        if (requireEquipmentForRaycast && !isGraffitiCanEquipped)
            return false;

        return true;
    }

    /// <summary>
    /// Updates the input action reference to use the correct controller's trigger
    /// </summary>
    private void UpdateInputActionReference(Transform controllerTransform)
    {
        // Try the reflection approach first
        bool foundActionViaReflection = TryFindActionViaReflection(controllerTransform);
        
        if (!foundActionViaReflection)
        {
            // Fallback: Create controller-specific input action
            CreateControllerSpecificInputAction(controllerTransform);
        }
    }

    /// <summary>
    /// Attempts to find the correct input action using reflection
    /// </summary>
    private bool TryFindActionViaReflection(Transform controllerTransform)
    {
        // Try to find a more generic trigger action by searching for common input action names
        var actionProperties = controllerTransform.GetComponentsInChildren<MonoBehaviour>();
        
        foreach (var component in actionProperties)
        {
            // Use reflection to find InputActionProperty fields that might be triggers
            var fields = component.GetType().GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(InputActionProperty))
                {
                    var actionProperty = (InputActionProperty)field.GetValue(component);
                    if (actionProperty.action != null)
                    {
                        string actionName = actionProperty.action.name.ToLower();
                        
                        // Look for trigger-related actions
                        if (actionName.Contains("select") || actionName.Contains("trigger") || actionName.Contains("activate"))
                        {
                            // Test if this action actually responds to the current controller
                            bool actionRespondsToController = TestActionForController(actionProperty.action, controllerTransform);
                            
                            if (actionRespondsToController)
                            {
                                // Create a temporary InputActionReference to test this action
                                var tempReference = ScriptableObject.CreateInstance<InputActionReference>();
                                tempReference.Set(actionProperty.action);
                                
                                dynamicSelectActionReference = tempReference;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Tests if an action responds to the given controller
    /// </summary>
    private bool TestActionForController(InputAction action, Transform controllerTransform)
    {
        string controllerName = controllerTransform.name.ToLower();
        bool isLeftController = controllerName.Contains("left");
        bool isRightController = controllerName.Contains("right");
        
        // Check the action's bindings to see if they match the controller
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            string bindingPath = binding.path.ToLower();
            
            // Check if binding matches the controller type
            if (isLeftController && bindingPath.Contains("lefthand"))
            {
                return true;
            }
            else if (isRightController && bindingPath.Contains("righthand"))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets the appropriate select action reference based on current controller
    /// </summary>
    private InputActionReference GetCurrentSelectActionReference()
    {
        // Use hand-specific reference if available
        if (handType == HandType.LeftHand && leftHandSelectActionReference != null)
        {
            return leftHandSelectActionReference;
        }
        else if (handType == HandType.RightHand && selectActionReference != null)
        {
            return selectActionReference;
        }
        
        // Use dynamic reference if available, otherwise fall back to original
        return dynamicSelectActionReference ?? selectActionReference;
    }

    /// <summary>
    /// Creates a proper input action reference for the controller that's holding the graffiti can
    /// </summary>
    private void CreateControllerSpecificInputAction(Transform controllerTransform)
    {
        string controllerName = controllerTransform.name.ToLower();
        string bindingPath;
        
        // Determine the correct binding path based on controller type
        if (controllerName.Contains("left"))
        {
            bindingPath = "<XRController>{LeftHand}/triggerPressed";
        }
        else if (controllerName.Contains("right"))
        {
            bindingPath = "<XRController>{RightHand}/triggerPressed";
        }
        else
        {
            Debug.LogWarning($"Could not determine controller type from name: {controllerTransform.name}");
            dynamicSelectActionReference = selectActionReference;
            return;
        }
        
        // Create a new InputAction specifically for this controller
        var newAction = new InputAction($"GraffitiTrigger_{handType}", InputActionType.Button);
        newAction.AddBinding(bindingPath);
        
        // Enable the action
        newAction.Enable();
        
        // Create InputActionReference
        var newReference = ScriptableObject.CreateInstance<InputActionReference>();
        newReference.Set(newAction);
        
        dynamicSelectActionReference = newReference;
    }

    /// <summary>
    /// Disables the default XR Ray Interactor on this hand when graffiti painting is active
    /// This preserves UI functionality for the non-painting hand
    /// </summary>
    private void SetRayInteractorState(bool enabled)
    {
        if (rayInteractor != null)
        {
            rayInteractor.enabled = enabled;
        }
    }

    /// <summary>
    /// Called when the graffiti can is grabbed by a hand
    /// </summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isGraffitiCanEquipped = true;
        
        // Detect which hand grabbed the can
        if (autoDetectHand)
        {
            DetectHandType();
        }
        
        if (isPaintingHand)
        {
            // Disable default ray interactor on painting hand to prevent conflicts
            SetRayInteractorState(false);
            
            // Setup continuous line if not already done
            if (continuousLine == null)
            {
                SetupContinuousLine();
            }
        }
        else
        {
            // Graffiti can equipped on non-painting hand - maintaining default UI raycast functionality
        }
    }

    /// <summary>
    /// Called when the graffiti can is released/dropped
    /// </summary>
    private void OnReleased(SelectExitEventArgs args)
    {
        isGraffitiCanEquipped = false;
        handType = HandType.Unknown;
        
        if (isPaintingHand)
        {
            // Re-enable default ray interactor on painting hand
            SetRayInteractorState(true);
        }
        
        // Reset painting hand status
        isPaintingHand = false;
        rayInteractor = null;
        
        // Immediately hide any active preview when unequipped
        HideCanvasPreview();
        isAimingAtCanvas = false;
        currentTargetCanvas = null;
        
        // Disable continuous line
        if (continuousLine != null)
        {
            continuousLine.enabled = false;
        }
    }

    #endregion

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
            bool isPainting = GetCurrentSelectActionReference()?.action?.IsPressed() ?? false;
            if (!isPainting)
            {
                // Update preview position and visibility
                UpdateCanvasPreview(hit.textureCoord, hit.point, hit.normal);
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
            return;
        }
        
        // Update the canvas preview using stored UV coordinates
        UpdateCanvasPreview(lastHitUV, position, normal);
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
    }

    /// <summary>
    /// Equipment state management - these are now handled automatically by grab events
    /// Keep for backward compatibility or manual testing
    /// </summary>
    public void OnGraffitiCanEquipped()
    {
        Debug.LogWarning("OnGraffitiCanEquipped() called manually - equipment state is now handled automatically by grab events");
        
        // Force equipment state for testing
        isGraffitiCanEquipped = true;
        
        if (autoDetectHand)
        {
            DetectHandType();
        }
        
        if (isPaintingHand)
        {
            SetRayInteractorState(false);
        }
    }

    public void OnGraffitiCanUnequipped()
    {
        Debug.LogWarning("OnGraffitiCanUnequipped() called manually - equipment state is now handled automatically by grab events");
        
        isGraffitiCanEquipped = false;
        
        if (isPaintingHand)
        {
            SetRayInteractorState(true);
        }
        
        HideCanvasPreview();
        isAimingAtCanvas = false;
        currentTargetCanvas = null;
    }

    /// <summary>
    /// Check if graffiti can is currently equipped
    /// </summary>
    public bool IsGraffitiCanEquipped()
    {
        return isGraffitiCanEquipped;
    }

    /// <summary>
    /// Get current hand type for debugging
    /// </summary>
    public HandType GetHandType()
    {
        return handType;
    }

    /// <summary>
    /// Check if this is the designated painting hand
    /// </summary>
    public bool IsPaintingHand()
    {
        return isPaintingHand;
    }

    /// <summary>
    /// Manually set hand type (useful for testing or when auto-detection fails)
    /// </summary>
    public void SetHandType(HandType newHandType)
    {
        handType = newHandType;
        // When manually setting hand type, assume the graffiti can is equipped and this hand should paint
        isPaintingHand = (newHandType != HandType.Unknown);
    }

    /// <summary>
    /// Toggle whether this should be treated as the painting hand
    /// </summary>
    public void SetAsPaintingHand(bool isPainting)
    {
        isPaintingHand = isPainting;
    }

    /// <summary>
    /// Toggle whether equipment is required for functionality (useful for testing)
    /// </summary>
    public void SetRequireEquipment(bool required)
    {
        requireEquipmentForRaycast = required;
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
        }
        else
        {
            Debug.LogWarning("No valid UV coordinates stored. Aim at canvas first.");
        }
    }

    /// <summary>
    /// Test equipment state controls
    /// </summary>
    [ContextMenu("Test Equip Graffiti Can")]
    public void TestEquipGraffitiCan()
    {
        OnGraffitiCanEquipped();
    }

    [ContextMenu("Test Unequip Graffiti Can")]
    public void TestUnequipGraffitiCan()
    {
        OnGraffitiCanUnequipped();
    }

    /// <summary>
    /// Test hand detection
    /// </summary>
    [ContextMenu("Test Detect Hand Type")]
    public void TestDetectHandType()
    {
        DetectHandType();
    }

    [ContextMenu("Force Enable Painting (Left Hand)")]
    public void TestForceLeftHandPainting()
    {
        handType = HandType.LeftHand;
        isPaintingHand = true;
        isGraffitiCanEquipped = true;
    }

    [ContextMenu("Force Enable Painting (Right Hand)")]
    public void TestForceRightHandPainting()
    {
        handType = HandType.RightHand;
        isPaintingHand = true;
        isGraffitiCanEquipped = true;
    }

    [ContextMenu("Force Create Left Hand Action")]
    public void TestForceCreateLeftHandAction()
    {
        // Manually create a left hand action for testing
        var leftAction = new InputAction("TestGraffitiTrigger_LeftHand", InputActionType.Button);
        leftAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
        leftAction.Enable();
        
        var newReference = ScriptableObject.CreateInstance<InputActionReference>();
        newReference.Set(leftAction);
        
        dynamicSelectActionReference = newReference;
    }

    [ContextMenu("Debug Input Actions")]
    public void DebugInputActions()
    {
        Debug.Log($"=== INPUT ACTION DEBUG ===");
        Debug.Log($"Right hand selectActionReference: {selectActionReference?.action?.name}");
        Debug.Log($"Left hand leftHandSelectActionReference: {leftHandSelectActionReference?.action?.name}");
        Debug.Log($"Dynamic selectActionReference: {dynamicSelectActionReference?.action?.name}");
        Debug.Log($"Current hand type: {handType}");
        Debug.Log($"Current action (GetCurrentSelectActionReference): {GetCurrentSelectActionReference()?.action?.name}");
        
        var currentAction = GetCurrentSelectActionReference();
        if (currentAction?.action != null)
        {
            Debug.Log($"Action enabled: {currentAction.action.enabled}");
            Debug.Log($"Action isPressed: {currentAction.action.IsPressed()}");
            Debug.Log($"Action phase: {currentAction.action.phase}");
            Debug.Log($"Action bindings count: {currentAction.action.bindings.Count}");
            
            for (int i = 0; i < currentAction.action.bindings.Count; i++)
            {
                var binding = currentAction.action.bindings[i];
                Debug.Log($"  Binding {i}: {binding.path} (groups: {binding.groups})");
            }
        }
        
        if (xrModalityManager != null)
        {
            Debug.Log($"Left controller: {xrModalityManager.leftController?.name}");
            Debug.Log($"Right controller: {xrModalityManager.rightController?.name}");
        }
    }

    [ContextMenu("Set as Left Hand")]
    public void TestSetAsLeftHand()
    {
        SetHandType(HandType.LeftHand);
    }

    [ContextMenu("Set as Right Hand")]
    public void TestSetAsRightHand()
    {
        SetHandType(HandType.RightHand);
    }

    [ContextMenu("Toggle Painting Hand")]
    public void TestTogglePaintingHand()
    {
        SetAsPaintingHand(!isPaintingHand);
    }

    /// <summary>
    /// Check if preview system is properly initialized
    /// </summary>
    [ContextMenu("Debug Preview Status")]
    public void DebugPreviewStatus()
    {
        Debug.Log($"=== HAND DETECTION ===");
        Debug.Log($"Hand Type: {handType}");
        Debug.Log($"Is Painting Hand: {isPaintingHand}");
        Debug.Log($"Auto Detect Hand: {autoDetectHand}");
        Debug.Log($"XR Modality Manager Found: {xrModalityManager != null}");
        Debug.Log($"XR Ray Interactor Found: {rayInteractor != null}");
        if (rayInteractor != null)
            Debug.Log($"XR Ray Interactor Enabled: {rayInteractor.enabled}");
        
        Debug.Log($"=== EQUIPMENT STATE ===");
        Debug.Log($"Graffiti Can Equipped: {isGraffitiCanEquipped}");
        Debug.Log($"Require Equipment: {requireEquipmentForRaycast}");
        Debug.Log($"Should Enable Raycast: {ShouldEnableRaycastFunctionality()}");
        
        Debug.Log($"=== PREVIEW STATE ===");
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
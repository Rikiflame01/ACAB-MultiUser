using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Button component that toggles eraser mode on CanvasRaycast instances.
/// Can be attached to a UI Button to provide eraser toggle functionality.
/// </summary>
public class EraserToggleButton : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private Image buttonImage;
    
    [Header("Button Text")]
    [SerializeField] private string paintModeText = "Paint Mode";
    [SerializeField] private string eraserModeText = "Eraser Mode";
    
    [Header("Button Colors")]
    [SerializeField] private Color paintModeColor = Color.green;
    [SerializeField] private Color eraserModeColor = Color.red;
    
    [Header("Target Settings")]
    [SerializeField] private CanvasRaycast[] targetCanvasRaycasts; // Specific targets to control
    [SerializeField] private bool findAllCanvasRaycastsInScene = true; // Auto-find all CanvasRaycast instances
    [SerializeField] private bool updateButtonAppearance = true; // Whether to update button text/color
    
    private bool isCurrentlyEraserMode = false;

    void Start()
    {
        // Auto-assign button if not set
        if (toggleButton == null)
            toggleButton = GetComponent<Button>();
            
        // Auto-assign text if not set
        if (buttonText == null)
            buttonText = GetComponentInChildren<TextMeshProUGUI>();
            
        // Auto-assign image if not set
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        // Setup button click listener
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleEraserMode);
        }
        else
        {
            Debug.LogError("EraserToggleButton: No Button component found! Please assign a Button or attach this script to a GameObject with a Button component.");
        }
        
        // Find all CanvasRaycast instances if enabled
        if (findAllCanvasRaycastsInScene)
        {
            RefreshCanvasRaycastTargets();
        }
        
        // Update initial button appearance
        UpdateButtonAppearance();
    }

    void OnDestroy()
    {
        // Clean up button listener
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleEraserMode);
        }
    }

    /// <summary>
    /// Toggles eraser mode on all target CanvasRaycast instances
    /// </summary>
    public void ToggleEraserMode()
    {
        SetEraserMode(!isCurrentlyEraserMode);
    }

    /// <summary>
    /// Sets eraser mode on all target CanvasRaycast instances
    /// </summary>
    public void SetEraserMode(bool enabled)
    {
        isCurrentlyEraserMode = enabled;
        
        // Apply to all target CanvasRaycast instances
        var canvasRaycasts = GetTargetCanvasRaycasts();
        
        foreach (var canvasRaycast in canvasRaycasts)
        {
            if (canvasRaycast != null)
            {
                canvasRaycast.SetEraserMode(enabled);
            }
        }
        
        // Update button appearance
        if (updateButtonAppearance)
        {
            UpdateButtonAppearance();
        }
        
        Debug.Log($"Eraser mode {(enabled ? "enabled" : "disabled")} on {canvasRaycasts.Length} CanvasRaycast instances");
    }

    /// <summary>
    /// Gets the current eraser mode state
    /// </summary>
    public bool IsEraserMode()
    {
        return isCurrentlyEraserMode;
    }

    /// <summary>
    /// Manually refresh the list of CanvasRaycast targets
    /// </summary>
    public void RefreshCanvasRaycastTargets()
    {
        if (findAllCanvasRaycastsInScene)
        {
            targetCanvasRaycasts = FindObjectsByType<CanvasRaycast>(FindObjectsSortMode.None);
            Debug.Log($"EraserToggleButton: Found {targetCanvasRaycasts.Length} CanvasRaycast instances in scene");
        }
    }

    /// <summary>
    /// Gets all target CanvasRaycast instances (either manually assigned or auto-found)
    /// </summary>
    private CanvasRaycast[] GetTargetCanvasRaycasts()
    {
        if (findAllCanvasRaycastsInScene)
        {
            // Always get fresh list when using auto-find mode
            return FindObjectsByType<CanvasRaycast>(FindObjectsSortMode.None);
        }
        else
        {
            // Use manually assigned targets
            return targetCanvasRaycasts ?? new CanvasRaycast[0];
        }
    }

    /// <summary>
    /// Updates the button's visual appearance based on current mode
    /// </summary>
    private void UpdateButtonAppearance()
    {
        if (buttonText != null)
        {
            buttonText.text = isCurrentlyEraserMode ? eraserModeText : paintModeText;
        }
        
        if (buttonImage != null)
        {
            buttonImage.color = isCurrentlyEraserMode ? eraserModeColor : paintModeColor;
        }
    }


    /// <summary>
    /// Manually set target CanvasRaycast instances
    /// </summary>
    public void SetTargetCanvasRaycasts(CanvasRaycast[] targets)
    {
        targetCanvasRaycasts = targets;
        findAllCanvasRaycastsInScene = false; // Disable auto-find when manually setting targets
    }

    /// <summary>
    /// Add a single CanvasRaycast target to the list
    /// </summary>
    public void AddTargetCanvasRaycast(CanvasRaycast target)
    {
        if (target == null) return;
        
        // Convert to list, add target, convert back to array
        var currentTargets = targetCanvasRaycasts ?? new CanvasRaycast[0];
        var targetList = new System.Collections.Generic.List<CanvasRaycast>(currentTargets);
        
        if (!targetList.Contains(target))
        {
            targetList.Add(target);
            targetCanvasRaycasts = targetList.ToArray();
            
            // Apply current eraser state to new target
            target.SetEraserMode(isCurrentlyEraserMode);
        }
    }

    /// <summary>
    /// Remove a CanvasRaycast target from the list
    /// </summary>
    public void RemoveTargetCanvasRaycast(CanvasRaycast target)
    {
        if (target == null || targetCanvasRaycasts == null) return;
        
        var targetList = new System.Collections.Generic.List<CanvasRaycast>(targetCanvasRaycasts);
        if (targetList.Remove(target))
        {
            targetCanvasRaycasts = targetList.ToArray();
        }
    }

    /// <summary>
    /// Set button text for different modes
    /// </summary>
    public void SetButtonTexts(string paintText, string eraserText)
    {
        paintModeText = paintText;
        eraserModeText = eraserText;
        UpdateButtonAppearance();
    }

    /// <summary>
    /// Set button colors for different modes
    /// </summary>
    public void SetButtonColors(Color paintColor, Color eraserColor)
    {
        paintModeColor = paintColor;
        eraserModeColor = eraserColor;
        UpdateButtonAppearance();
    }

    /// <summary>
    /// Enable/disable automatic button appearance updates
    /// </summary>
    public void SetUpdateButtonAppearance(bool enabled)
    {
        updateButtonAppearance = enabled;
    }

    #region Debug Methods

    [ContextMenu("Test Toggle Eraser")]
    public void TestToggleEraser()
    {
        ToggleEraserMode();
    }

    [ContextMenu("Enable Eraser Mode")]
    public void TestEnableEraserMode()
    {
        SetEraserMode(true);
    }

    [ContextMenu("Disable Eraser Mode")]
    public void TestDisableEraserMode()
    {
        SetEraserMode(false);
    }

    [ContextMenu("Refresh Targets")]
    public void TestRefreshTargets()
    {
        RefreshCanvasRaycastTargets();
    }

    [ContextMenu("Debug Button State")]
    public void DebugButtonState()
    {
        Debug.Log($"=== ERASER TOGGLE BUTTON DEBUG ===");
        Debug.Log($"Is Eraser Mode: {isCurrentlyEraserMode}");
        Debug.Log($"Find All In Scene: {findAllCanvasRaycastsInScene}");
        Debug.Log($"Manual Targets Count: {(targetCanvasRaycasts?.Length ?? 0)}");
        Debug.Log($"Update Button Appearance: {updateButtonAppearance}");
        Debug.Log($"Button Component: {(toggleButton != null ? "Found" : "Missing")}");
        Debug.Log($"Button Text Component: {(buttonText != null ? "Found" : "Missing")}");
        Debug.Log($"Button Image Component: {(buttonImage != null ? "Found" : "Missing")}");
        
        var allTargets = GetTargetCanvasRaycasts();
        Debug.Log($"Total Active Targets: {allTargets.Length}");
        
        for (int i = 0; i < allTargets.Length; i++)
        {
            var target = allTargets[i];
            if (target != null)
            {
                Debug.Log($"  Target {i}: {target.gameObject.name} - Eraser Mode: {target.IsEraserMode()}");
            }
        }
    }

    #endregion
}
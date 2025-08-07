using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages a FILO (First In, Last Out) color history system for the ColorWheel
/// Automatically tracks color changes and provides quick access to recently used colors
/// </summary>
[RequireComponent(typeof(ColorWheel))]
public class ColorHistory : MonoBehaviour
{
    [Header("Color History Settings")]
    [SerializeField]
    [Tooltip("Maximum number of colors to remember in history")]
    private int maxHistorySize = 8;

    [SerializeField]
    [Tooltip("Minimum color difference to add to history (prevents duplicate similar colors)")]
    private float colorDifferenceThreshold = 0.1f;

    [SerializeField]
    [Tooltip("Auto-add colors when they're applied to canvas (painting) instead of just selection")]
    private bool trackOnCanvasApplication = true;

    [SerializeField]
    [Tooltip("Ignore rapid slider changes to prevent flooding history with similar colors")]
    private bool ignoreSliderChanges = true;

    [SerializeField]
    [Tooltip("Time delay before adding a color from slider changes (prevents spam)")]
    private float sliderChangeDelay = 1.0f;

    [Header("UI References")]
    [SerializeField]
    [Tooltip("Parent object containing the color history buttons")]
    private GameObject colorHistoryPanel;

    [SerializeField]
    [Tooltip("Array of UI buttons representing color history slots")]
    private Button[] historyButtons;

    [SerializeField]
    [Tooltip("Array of images for the history buttons")]
    private Image[] historyButtonImages;

    // Private fields
    private ColorWheel colorWheel;
    private List<Color> colorHistory = new List<Color>();
    private Color lastTrackedColor;
    private bool isLoadingFromHistory = false;
    private CanvasRaycast canvasRaycast; // Reference to track canvas painting events
    
    // Slider change tracking
    private float lastSliderChangeTime = 0f;
    private Color pendingSliderColor;
    private bool hasPendingSliderColor = false;

    private void Awake()
    {
        colorWheel = GetComponent<ColorWheel>();
        
        // Initialize with current color
        lastTrackedColor = colorWheel.GetColor();
        colorHistory.Add(lastTrackedColor);
    }

    private void Start()
    {
        // Auto-find history buttons if not manually assigned
        if (historyButtons == null || historyButtons.Length == 0)
        {
            FindHistoryButtons();
        }
        else
        {
            // Set up click listeners for manually assigned buttons
            for (int i = 0; i < historyButtons.Length; i++)
            {
                if (historyButtons[i] != null)
                {
                    int buttonIndex = i; // Capture for closure
                    historyButtons[i].onClick.RemoveAllListeners(); // Clear any existing listeners
                    historyButtons[i].onClick.AddListener(() => {
                        LoadColorFromHistory(buttonIndex);
                    });
                }
            }
        }

        // Subscribe to canvas application events instead of color changes
        if (trackOnCanvasApplication)
        {
            SetupCanvasTrackingEvents();
        }

        // Initialize UI
        UpdateHistoryUI();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (colorWheel != null)
        {
            colorWheel.OnColorChanged -= OnColorChanged;
        }

        // Unsubscribe from canvas tracking
        CleanupCanvasTrackingEvents();
    }

    /// <summary>
    /// Automatically finds history buttons in the ColorHistory_Panel
    /// </summary>
    private void FindHistoryButtons()
    {
        if (colorHistoryPanel == null)
        {
            colorHistoryPanel = GameObject.Find("ColorHistory_Panel");
        }

        if (colorHistoryPanel != null)
        {
            // Find all buttons in the history panel
            Button[] foundButtons = colorHistoryPanel.GetComponentsInChildren<Button>();
            Image[] foundImages = colorHistoryPanel.GetComponentsInChildren<Image>();

            // Filter to get only the history buttons (exclude the panel background)
            historyButtons = foundButtons.Where(b => b.name.StartsWith("ColorHistory_")).OrderBy(b => b.name).ToArray();
            historyButtonImages = foundImages.Where(img => img.name.StartsWith("ColorHistory_")).OrderBy(img => img.name).ToArray();

            Debug.Log($"✅ Found {historyButtons.Length} color history buttons");
        }
        else
        {
            Debug.LogWarning("❌ ColorHistory_Panel not found! Make sure the ColorWheelSliderGenerator has created the UI.");
        }
    }

    /// <summary>
    /// Sets up event tracking for canvas painting instead of color selection
    /// </summary>
    private void SetupCanvasTrackingEvents()
    {
        // Get CanvasRaycast reference from ColorWheel
        if (colorWheel != null)
        {
            canvasRaycast = colorWheel.CanvasRaycastReference;
        }

        // If not found directly, try to find it in the scene
        if (canvasRaycast == null)
        {
            canvasRaycast = FindFirstObjectByType<CanvasRaycast>();
        }

        if (canvasRaycast != null)
        {
            // We'll monitor painting via Update since CanvasRaycast doesn't have paint events
            // Alternative: Could modify CanvasRaycast to add events, but this is less invasive
        }
        else
        {
            Debug.LogWarning("❌ No CanvasRaycast found - falling back to color change tracking");
            
            // Fallback: track color changes if no canvas raycast found
            if (colorWheel != null)
            {
                colorWheel.OnColorChanged += OnColorChanged;
            }
        }
    }

    /// <summary>
    /// Cleanup canvas tracking events
    /// </summary>
    private void CleanupCanvasTrackingEvents()
    {
        // No specific cleanup needed for our current implementation
        // If we added custom events to CanvasRaycast, we'd unsubscribe here
    }

    /// <summary>
    /// Monitor for actual canvas painting to track color usage
    /// </summary>
    private void Update()
    {
        // Handle pending slider color changes with debounce
        if (hasPendingSliderColor && ignoreSliderChanges)
        {
            if (Time.time - lastSliderChangeTime >= sliderChangeDelay)
            {
                // Enough time has passed since last slider change, add the color
                if (IsColorSignificantlyDifferent(pendingSliderColor, lastTrackedColor))
                {
                    AddColorToHistory(pendingSliderColor);
                    lastTrackedColor = pendingSliderColor;
                }
                hasPendingSliderColor = false;
            }
        }

        // Only track if we have canvas tracking enabled and components available
        if (!trackOnCanvasApplication || canvasRaycast == null || colorWheel == null) return;

        // Check if painting is actively happening
        if (IsPaintingActive())
        {
            Color currentPaintColor = canvasRaycast.markColor;
            
            // Check if this is a new color being applied
            if (IsColorSignificantlyDifferent(currentPaintColor, lastTrackedColor))
            {
                AddColorToHistory(currentPaintColor);
                lastTrackedColor = currentPaintColor;
            }
        }
    }

    /// <summary>
    /// Public method to manually add a color when painting occurs (alternative to automatic tracking)
    /// </summary>
    public void OnColorAppliedToCanvas(Color appliedColor)
    {
        if (!trackOnCanvasApplication || isLoadingFromHistory) return;

        if (IsColorSignificantlyDifferent(appliedColor, lastTrackedColor))
        {
            AddColorToHistory(appliedColor);
            lastTrackedColor = appliedColor;
        }
    }

    /// <summary>
    /// Determines if painting is currently active
    /// </summary>
    /// <returns>True if user is actively painting on canvas</returns>
    private bool IsPaintingActive()
    {
        if (canvasRaycast == null) return false;

        // Check if the graffiti can is equipped and being used
        bool isEquipped = canvasRaycast.IsGraffitiCanEquipped();
        bool isPaintingHand = canvasRaycast.IsPaintingHand();
        
        if (!isEquipped || !isPaintingHand) return false;

        // Check if trigger is actually being pressed (this is the key missing check!)
        bool isTriggerPressed = false;
        try
        {
            // Use reflection to access the GetCurrentSelectActionReference method
            var getCurrentActionMethod = typeof(CanvasRaycast).GetMethod("GetCurrentSelectActionReference", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (getCurrentActionMethod != null)
            {
                var actionRef = getCurrentActionMethod.Invoke(canvasRaycast, null) as UnityEngine.InputSystem.InputActionReference;
                isTriggerPressed = actionRef?.action?.IsPressed() ?? false;
            }
        }
        catch (System.Exception)
        {
            // Reflection failed, fall back to assuming not painting
            return false;
        }

        if (!isTriggerPressed) return false;

        // Check if we're aiming at a canvas (use reflection to access isAimingAtCanvas)
        bool isAimingAtCanvas = false;
        try
        {
            var aimingField = typeof(CanvasRaycast).GetField("isAimingAtCanvas", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (aimingField != null)
            {
                isAimingAtCanvas = (bool)aimingField.GetValue(canvasRaycast);
            }
        }
        catch (System.Exception)
        {
            // Reflection failed, fall back to assuming not aiming at canvas
            return false;
        }

        // Only return true if trigger is pressed AND aiming at canvas
        // This matches the exact conditions used in CanvasRaycast.Update()
        return isTriggerPressed && isAimingAtCanvas;
    }

    /// <summary>
    /// Simple tracking method - call this from external scripts when painting occurs
    /// </summary>
    [ContextMenu("Test Paint Tracking")]
    public void TestPaintTracking()
    {
        if (colorWheel != null)
        {
            Color currentColor = colorWheel.GetColor();
            OnColorAppliedToCanvas(currentColor);
        }
    }

    /// <summary>
    /// Gets the current input action reference from CanvasRaycast (via reflection if needed)
    /// </summary>
    /// <returns>Current action reference or null</returns>
    private UnityEngine.InputSystem.InputActionReference GetCurrentActionReference()
    {
        if (canvasRaycast == null) return null;

        // Try to access the action reference via reflection since it's private
        try
        {
            var fieldInfo = typeof(CanvasRaycast).GetField("selectActionReference", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(canvasRaycast) as UnityEngine.InputSystem.InputActionReference;
            }
        }
        catch (System.Exception)
        {
            // Reflection failed, that's okay
        }

        return null;
    }

    /// <summary>
    /// Called when the color wheel color changes
    /// </summary>
    /// <param name="newColor">The new color selected</param>
    private void OnColorChanged(Color newColor)
    {
        // Don't track if we're currently loading from history (prevents loops)
        if (isLoadingFromHistory) return;

        // Detect if this change might be from sliders (brightness/alpha changes)
        bool isPotentialSliderChange = IsPotentialSliderChange(newColor, lastTrackedColor);

        if (isPotentialSliderChange && ignoreSliderChanges)
        {
            // This looks like a slider change - use debounced tracking
            pendingSliderColor = newColor;
            lastSliderChangeTime = Time.time;
            hasPendingSliderColor = true;
        }
        else
        {
            // This looks like a direct wheel selection - track immediately
            if (IsColorSignificantlyDifferent(newColor, lastTrackedColor))
            {
                AddColorToHistory(newColor);
                lastTrackedColor = newColor;
            }
        }
    }

    /// <summary>
    /// Determines if a color change is likely from slider adjustments rather than wheel selection
    /// </summary>
    /// <param name="newColor">The new color</param>
    /// <param name="previousColor">The previous color</param>
    /// <returns>True if this looks like a slider change</returns>
    private bool IsPotentialSliderChange(Color newColor, Color previousColor)
    {
        // Convert both colors to HSV
        Color.RGBToHSV(newColor, out float newH, out float newS, out float newV);
        Color.RGBToHSV(previousColor, out float prevH, out float prevS, out float prevV);

        // Check if only brightness (Value) or alpha changed significantly
        // while hue and saturation stayed relatively the same
        float hueDiff = Mathf.Abs(newH - prevH);
        float satDiff = Mathf.Abs(newS - prevS);
        float valueDiff = Mathf.Abs(newV - prevV);
        float alphaDiff = Mathf.Abs(newColor.a - previousColor.a);

        // If hue and saturation are nearly the same, but brightness or alpha changed,
        // this is likely a slider change
        bool hueUnchanged = hueDiff < 0.01f; // Very small hue change
        bool satUnchanged = satDiff < 0.01f; // Very small saturation change
        bool valueChanged = valueDiff > 0.01f; // Noticeable brightness change
        bool alphaChanged = alphaDiff > 0.01f; // Noticeable alpha change

        return (hueUnchanged && satUnchanged && (valueChanged || alphaChanged));
    }

    /// <summary>
    /// Checks if a color is significantly different from another color
    /// </summary>
    /// <param name="color1">First color</param>
    /// <param name="color2">Second color</param>
    /// <returns>True if colors are significantly different</returns>
    private bool IsColorSignificantlyDifferent(Color color1, Color color2)
    {
        // Calculate color distance using RGB values
        float distance = Mathf.Sqrt(
            Mathf.Pow(color1.r - color2.r, 2) +
            Mathf.Pow(color1.g - color2.g, 2) +
            Mathf.Pow(color1.b - color2.b, 2) +
            Mathf.Pow(color1.a - color2.a, 2)
        );

        return distance > colorDifferenceThreshold;
    }

    /// <summary>
    /// Adds a color to the history using FILO (First In, Last Out) logic
    /// </summary>
    /// <param name="color">Color to add to history</param>
    public void AddColorToHistory(Color color)
    {
        // Remove color if it already exists in history
        for (int i = colorHistory.Count - 1; i >= 0; i--)
        {
            if (!IsColorSignificantlyDifferent(color, colorHistory[i]))
            {
                colorHistory.RemoveAt(i);
                break;
            }
        }

        // Add color to the beginning (most recent)
        colorHistory.Insert(0, color);

        // Remove oldest colors if exceeding max size
        while (colorHistory.Count > maxHistorySize)
        {
            colorHistory.RemoveAt(colorHistory.Count - 1);
        }

        // Update UI
        UpdateHistoryUI();
    }

    /// <summary>
    /// Loads a color from history at the specified index
    /// </summary>
    /// <param name="historyIndex">Index in the history (0 = most recent)</param>
    public void LoadColorFromHistory(int historyIndex)
    {
        if (historyIndex >= 0 && historyIndex < colorHistory.Count)
        {
            Color historicalColor = colorHistory[historyIndex];
            
            // Prevent tracking this change (avoid adding it back to history)
            isLoadingFromHistory = true;
            
            // Set the color in the color wheel
            if (colorWheel != null)
            {
                colorWheel.SetColor(historicalColor);
            }
            
            // Move this color to the front of history (most recently used)
            colorHistory.RemoveAt(historyIndex);
            colorHistory.Insert(0, historicalColor);
            
            // Update UI to reflect new order
            UpdateHistoryUI();
            
            // Re-enable tracking after a short delay
            StartCoroutine(ReenableTrackingAfterDelay());
            
            lastTrackedColor = historicalColor;
        }
    }

    /// <summary>
    /// Re-enables tracking after a short delay to prevent immediate re-adding of loaded color
    /// </summary>
    private System.Collections.IEnumerator ReenableTrackingAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        isLoadingFromHistory = false;
    }

    /// <summary>
    /// Updates the history UI buttons to reflect current color history
    /// </summary>
    private void UpdateHistoryUI()
    {
        if (historyButtonImages == null) return;

        for (int i = 0; i < historyButtonImages.Length; i++)
        {
            if (historyButtonImages[i] != null)
            {
                if (i < colorHistory.Count)
                {
                    // Show historical color
                    historyButtonImages[i].color = colorHistory[i];
                    
                    // Make button fully opaque to show it's active
                    var tempColor = historyButtonImages[i].color;
                    tempColor.a = 1f;
                    historyButtonImages[i].color = tempColor;
                    
                    // Enable button interaction
                    if (historyButtons != null && i < historyButtons.Length && historyButtons[i] != null)
                    {
                        historyButtons[i].interactable = true;
                    }
                }
                else
                {
                    // Show empty slot
                    historyButtonImages[i].color = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Gray and transparent
                    
                    // Disable button interaction
                    if (historyButtons != null && i < historyButtons.Length && historyButtons[i] != null)
                    {
                        historyButtons[i].interactable = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts a color to a hex string for debugging
    /// </summary>
    /// <param name="color">Color to convert</param>
    /// <returns>Hex string representation</returns>
    private string ColorToHex(Color color)
    {
        return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
    }

    /// <summary>
    /// Manually add a color to history (useful for preset colors or external systems)
    /// </summary>
    /// <param name="color">Color to add</param>
    public void ManuallyAddColor(Color color)
    {
        AddColorToHistory(color);
    }

    /// <summary>
    /// Clear all color history
    /// </summary>
    [ContextMenu("Clear Color History")]
    public void ClearHistory()
    {
        colorHistory.Clear();
        
        // Add current color back as the only entry
        if (colorWheel != null)
        {
            colorHistory.Add(colorWheel.GetColor());
        }
        
        UpdateHistoryUI();
    }

    /// <summary>
    /// Get the current color history (for debugging or external access)
    /// </summary>
    /// <returns>List of colors in history (newest first)</returns>
    public List<Color> GetColorHistory()
    {
        return new List<Color>(colorHistory); // Return a copy
    }

    /// <summary>
    /// Debug method to show current color history
    /// </summary>
    [ContextMenu("Debug Color History")]
    public void DebugColorHistory()
    {
        Debug.Log($"=== COLOR HISTORY DEBUG ===");
        Debug.Log($"History Size: {colorHistory.Count}/{maxHistorySize}");
        Debug.Log($"Track on Canvas Application: {trackOnCanvasApplication}");
        Debug.Log($"Threshold: {colorDifferenceThreshold}");
        Debug.Log($"Canvas Raycast Found: {canvasRaycast != null}");
        
        for (int i = 0; i < colorHistory.Count; i++)
        {
            Debug.Log($"  {i}: {ColorToHex(colorHistory[i])} - {colorHistory[i]}");
        }
        
        Debug.Log($"UI Buttons Found: {historyButtons?.Length ?? 0}");
        Debug.Log($"UI Images Found: {historyButtonImages?.Length ?? 0}");
        
        if (canvasRaycast != null)
        {
            Debug.Log($"Graffiti Can Equipped: {canvasRaycast.IsGraffitiCanEquipped()}");
            Debug.Log($"Is Painting Hand: {canvasRaycast.IsPaintingHand()}");
            Debug.Log($"Current Mark Color: {ColorToHex(canvasRaycast.markColor)}");
        }
    }

    /// <summary>
    /// Context menu helper to test adding random colors
    /// </summary>
    [ContextMenu("Test Add Random Color")]
    public void TestAddRandomColor()
    {
        Color randomColor = new Color(Random.value, Random.value, Random.value, 1f);
        ManuallyAddColor(randomColor);
    }

    /// <summary>
    /// Context menu helper to populate history with test colors
    /// </summary>
    [ContextMenu("Populate Test Colors")]
    public void PopulateTestColors()
    {
        // Clear existing history first
        colorHistory.Clear();
        
        // Add some test colors
        Color[] testColors = {
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.cyan,
            Color.magenta,
            new Color(1f, 0.5f, 0f, 1f), // Orange
            new Color(0.5f, 0f, 0.5f, 1f) // Purple
        };

        foreach (Color color in testColors)
        {
            AddColorToHistory(color);
        }
    }

    /// <summary>
    /// Test method to bypass history and directly set a color on the color wheel
    /// </summary>
    [ContextMenu("Test Direct Color Set")]
    public void TestDirectColorSet()
    {
        if (colorWheel != null)
        {
            colorWheel.SetColor(Color.green);
        }
    }

    /// <summary>
    /// Manual test method to simulate a button click without UI
    /// </summary>
    [ContextMenu("Test Manual History Load")]
    public void TestManualHistoryLoad()
    {
        LoadColorFromHistory(0);
    }

    /// <summary>
    /// Debug method to test if colorWheel reference is working
    /// </summary>
    [ContextMenu("Test ColorWheel Reference")]
    public void TestColorWheelReference()
    {
        if (colorWheel != null)
        {
            // Test setting a specific color
            Color testColor = Color.magenta;
            colorWheel.SetColor(testColor);
        }
    }
}

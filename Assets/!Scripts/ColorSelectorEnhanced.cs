using UnityEngine;
using UnityEngine.UI;

public class ColorSelectorEnhanced : MonoBehaviour
{
    [Header("Color Selection Mode")]
    [SerializeField]
    [Tooltip("Choose between RGB sliders or Color Wheel for color selection.")]
    private ColorSelectionMode selectionMode = ColorSelectionMode.ColorWheel;

    [Header("RGB Slider Components")]
    [SerializeField]
    [Tooltip("Slider for the Red component (0 to 1).")]
    private Slider redSlider;

    [SerializeField]
    [Tooltip("Slider for the Green component (0 to 1).")]
    private Slider greenSlider;

    [SerializeField]
    [Tooltip("Slider for the Blue component (0 to 1).")]
    private Slider blueSlider;

    [Header("Color Wheel Components")]
    [SerializeField]
    [Tooltip("Color wheel component for HSV color selection.")]
    private ColorWheel colorWheel;

    [Header("Shared Components")]
    [SerializeField]
    [Tooltip("Slider for the Mark Size (0 to 100).")]
    private Slider markSizeSlider;

    [SerializeField]
    [Tooltip("Image to display the current color combination.")]
    private Image colorPreviewImage;

    [SerializeField]
    [Tooltip("Image to preview the mark size by scaling (0.4 to 1).")]
    private Image markSizePreviewImage;

    [SerializeField]
    [Tooltip("Reference to the CanvasRaycast script to update the mark color and size.")]
    private CanvasRaycast canvasRaycast;

    [Header("Quick Color Presets")]
    [SerializeField]
    [Tooltip("Buttons for quick color selection.")]
    private Button[] colorPresetButtons;

    [SerializeField]
    [Tooltip("Colors for the preset buttons.")]
    private Color[] presetColors = {
        Color.red, Color.green, Color.blue, Color.yellow,
        Color.cyan, Color.magenta, Color.white, Color.black
    };

    [Header("VR Optimization")]
    [SerializeField]
    [Tooltip("Enable VR-optimized interface elements.")]
    private bool enableVROptimization = true;

    [SerializeField]
    [Tooltip("Toggle buttons to switch between RGB and Color Wheel modes.")]
    private Toggle rgbModeToggle;

    [SerializeField]
    [Tooltip("Toggle buttons to switch between RGB and Color Wheel modes.")]
    private Toggle colorWheelModeToggle;

    public enum ColorSelectionMode
    {
        RGBSliders,
        ColorWheel
    }

    private const float MIN_MARK_SIZE = 1f;
    private const float MAX_MARK_SIZE = 100f;
    private const float MIN_PREVIEW_SCALE = 0.4f;
    private const float MAX_PREVIEW_SCALE = 1f;

    private Color currentColor = Color.red;
    private bool isUpdatingColor = false; // Prevent recursive updates

    private void Awake()
    {
        ValidateComponents();
        SetupPresetButtons();
        SetupModeToggles();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        
        // Initialize with current selection mode
        SetColorSelectionMode(selectionMode);
        
        // Initialize the color and mark size
        UpdateColor();
        UpdateMarkSize(0f);
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void ValidateComponents()
    {
        if (markSizeSlider == null)
        {
            Debug.LogError("Mark Size Slider is not assigned!", this);
            enabled = false;
            return;
        }

        if (colorPreviewImage == null)
        {
            Debug.LogError("Color Preview Image is not assigned!", this);
            enabled = false;
            return;
        }

        if (markSizePreviewImage == null)
        {
            Debug.LogError("Mark Size Preview Image is not assigned!", this);
            enabled = false;
            return;
        }

        if (canvasRaycast == null)
        {
            Debug.LogError("CanvasRaycast reference is not assigned!", this);
            enabled = false;
            return;
        }

        // Validate components based on selection mode
        if (selectionMode == ColorSelectionMode.RGBSliders)
        {
            if (redSlider == null || greenSlider == null || blueSlider == null)
            {
                Debug.LogError("RGB Sliders are not assigned but RGB mode is selected!", this);
            }
        }
        else if (selectionMode == ColorSelectionMode.ColorWheel)
        {
            if (colorWheel == null)
            {
                Debug.LogError("Color Wheel is not assigned but Color Wheel mode is selected!", this);
            }
        }
    }

    private void SetupPresetButtons()
    {
        if (colorPresetButtons == null) return;

        for (int i = 0; i < colorPresetButtons.Length && i < presetColors.Length; i++)
        {
            if (colorPresetButtons[i] != null)
            {
                // Set button color
                var buttonImage = colorPresetButtons[i].GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = presetColors[i];
                }

                // Setup click event
                int colorIndex = i; // Capture for closure
                colorPresetButtons[i].onClick.AddListener(() => SetPresetColor(colorIndex));
            }
        }
    }

    private void SetupModeToggles()
    {
        if (rgbModeToggle != null)
        {
            rgbModeToggle.onValueChanged.AddListener((bool isOn) => {
                if (isOn) SetColorSelectionMode(ColorSelectionMode.RGBSliders);
            });
        }

        if (colorWheelModeToggle != null)
        {
            colorWheelModeToggle.onValueChanged.AddListener((bool isOn) => {
                if (isOn) SetColorSelectionMode(ColorSelectionMode.ColorWheel);
            });
        }
    }

    private void SubscribeToEvents()
    {
        // Subscribe to RGB sliders
        if (redSlider != null)
            redSlider.onValueChanged.AddListener(OnRGBSliderChanged);
        if (greenSlider != null)
            greenSlider.onValueChanged.AddListener(OnRGBSliderChanged);
        if (blueSlider != null)
            blueSlider.onValueChanged.AddListener(OnRGBSliderChanged);

        // Subscribe to mark size slider
        if (markSizeSlider != null)
            markSizeSlider.onValueChanged.AddListener(UpdateMarkSize);

        // Subscribe to color wheel
        if (colorWheel != null)
            colorWheel.OnColorChanged += OnColorWheelChanged;
    }

    private void UnsubscribeFromEvents()
    {
        // Unsubscribe from RGB sliders
        if (redSlider != null)
            redSlider.onValueChanged.RemoveListener(OnRGBSliderChanged);
        if (greenSlider != null)
            greenSlider.onValueChanged.RemoveListener(OnRGBSliderChanged);
        if (blueSlider != null)
            blueSlider.onValueChanged.RemoveListener(OnRGBSliderChanged);

        // Unsubscribe from mark size slider
        if (markSizeSlider != null)
            markSizeSlider.onValueChanged.RemoveListener(UpdateMarkSize);

        // Unsubscribe from color wheel
        if (colorWheel != null)
            colorWheel.OnColorChanged -= OnColorWheelChanged;

        // Unsubscribe from preset buttons
        if (colorPresetButtons != null)
        {
            foreach (var button in colorPresetButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
        }

        // Unsubscribe from mode toggles
        if (rgbModeToggle != null)
            rgbModeToggle.onValueChanged.RemoveAllListeners();
        if (colorWheelModeToggle != null)
            colorWheelModeToggle.onValueChanged.RemoveAllListeners();
    }

    public void SetColorSelectionMode(ColorSelectionMode mode)
    {
        selectionMode = mode;

        // Show/hide appropriate UI elements
        if (redSlider != null && greenSlider != null && blueSlider != null)
        {
            bool showRGBSliders = (mode == ColorSelectionMode.RGBSliders);
            redSlider.gameObject.SetActive(showRGBSliders);
            greenSlider.gameObject.SetActive(showRGBSliders);
            blueSlider.gameObject.SetActive(showRGBSliders);
        }

        if (colorWheel != null)
        {
            bool showColorWheel = (mode == ColorSelectionMode.ColorWheel);
            colorWheel.gameObject.SetActive(showColorWheel);
            
            if (showColorWheel && enableVROptimization)
            {
                colorWheel.EnableVRMode(true);
            }
        }

        // Update toggles
        if (rgbModeToggle != null)
            rgbModeToggle.isOn = (mode == ColorSelectionMode.RGBSliders);
        if (colorWheelModeToggle != null)
            colorWheelModeToggle.isOn = (mode == ColorSelectionMode.ColorWheel);

        // Update color with current mode
        UpdateColor();
    }

    private void OnRGBSliderChanged(float value)
    {
        if (isUpdatingColor || selectionMode != ColorSelectionMode.RGBSliders) return;
        UpdateColor();
    }

    private void OnColorWheelChanged(Color color)
    {
        if (isUpdatingColor || selectionMode != ColorSelectionMode.ColorWheel) return;
        
        currentColor = color;
        UpdateColorDisplay();
        UpdateCanvasRaycastColor();
    }

    private void UpdateColor()
    {
        if (isUpdatingColor) return;

        isUpdatingColor = true;

        switch (selectionMode)
        {
            case ColorSelectionMode.RGBSliders:
                if (redSlider != null && greenSlider != null && blueSlider != null)
                {
                    float r = redSlider.value;
                    float g = greenSlider.value;
                    float b = blueSlider.value;
                    currentColor = new Color(r, g, b, currentColor.a);
                }
                break;

            case ColorSelectionMode.ColorWheel:
                if (colorWheel != null)
                {
                    currentColor = colorWheel.GetColor();
                }
                break;
        }

        UpdateColorDisplay();
        UpdateCanvasRaycastColor();

        isUpdatingColor = false;
    }

    private void UpdateColorDisplay()
    {
        // Update preview image
        if (colorPreviewImage != null)
        {
            colorPreviewImage.color = currentColor;
        }
    }

    private void UpdateCanvasRaycastColor()
    {
        // Update the markColor in the CanvasRaycast script
        if (canvasRaycast != null)
        {
            canvasRaycast.markColor = currentColor;
        }
    }

    private void UpdateMarkSize(float value)
    {
        // Get the mark size from the slider (0 to 100 range)
        float markSize = markSizeSlider.value;

        // Ensure markSize is at least 1 (as per requirements)
        markSize = Mathf.Max(MIN_MARK_SIZE, markSize);

        // Update the markSize in the CanvasRaycast script
        if (canvasRaycast != null)
        {
            canvasRaycast.markSize = markSize;
        }

        // Map the markSize (1 to 100) to the preview scale (0.4 to 1)
        float t = (markSize - MIN_MARK_SIZE) / (MAX_MARK_SIZE - MIN_MARK_SIZE);
        float previewScale = Mathf.Lerp(MIN_PREVIEW_SCALE, MAX_PREVIEW_SCALE, t);

        // Apply the scale to the preview image's RectTransform
        if (markSizePreviewImage != null)
        {
            markSizePreviewImage.rectTransform.localScale = new Vector3(previewScale, previewScale, 1f);
        }
    }

    public void SetPresetColor(int presetIndex)
    {
        if (presetIndex >= 0 && presetIndex < presetColors.Length)
        {
            Color newColor = presetColors[presetIndex];
            SetColor(newColor);
        }
    }

    public void SetColor(Color color)
    {
        currentColor = color;

        isUpdatingColor = true;

        // Update appropriate controls based on current mode
        switch (selectionMode)
        {
            case ColorSelectionMode.RGBSliders:
                if (redSlider != null) redSlider.value = color.r;
                if (greenSlider != null) greenSlider.value = color.g;
                if (blueSlider != null) blueSlider.value = color.b;
                break;

            case ColorSelectionMode.ColorWheel:
                if (colorWheel != null) colorWheel.SetColor(color);
                break;
        }

        UpdateColorDisplay();
        UpdateCanvasRaycastColor();

        isUpdatingColor = false;
    }

    public Color GetColor()
    {
        return currentColor;
    }

    // Public methods for external control
    public void EnableVROptimization(bool enabled)
    {
        enableVROptimization = enabled;
        if (colorWheel != null)
        {
            colorWheel.EnableVRMode(enabled);
        }
    }

    // Context menu methods for testing
    [ContextMenu("Switch to RGB Sliders")]
    public void TestSwitchToRGB() => SetColorSelectionMode(ColorSelectionMode.RGBSliders);

    [ContextMenu("Switch to Color Wheel")]
    public void TestSwitchToColorWheel() => SetColorSelectionMode(ColorSelectionMode.ColorWheel);

    [ContextMenu("Set Random Color")]
    public void TestSetRandomColor()
    {
        Color randomColor = new Color(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            1f
        );
        SetColor(randomColor);
    }

    [ContextMenu("Enable VR Mode")]
    public void TestEnableVRMode() => EnableVROptimization(true);

    [ContextMenu("Disable VR Mode")]
    public void TestDisableVRMode() => EnableVROptimization(false);
}

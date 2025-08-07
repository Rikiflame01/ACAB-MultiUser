using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper script to automatically set up a VR Color Wheel UI in your scene
/// Attach this to an empty GameObject and run CreateColorWheelUI() to generate the complete UI
/// </summary>
public class ColorWheelPrefabGenerator : MonoBehaviour
{
    [Header("Canvas Setup")]
    [SerializeField]
    [Tooltip("Parent Canvas for the color wheel UI. If null, will search for existing canvas.")]
    private Canvas parentCanvas;

    [SerializeField]
    [Tooltip("Reference to CanvasRaycast for color integration.")]
    private CanvasRaycast canvasRaycast;

    [Header("Generation Settings")]
    [SerializeField]
    [Tooltip("Size of the color wheel in UI units.")]
    private float colorWheelSize = 200f;

    [SerializeField]
    [Tooltip("Enable VR optimizations during creation.")]
    private bool enableVROptimizations = true;

    [SerializeField]
    [Tooltip("Include quick color preset buttons.")]
    private bool includePresetButtons = true;

    [SerializeField]
    [Tooltip("Include RGB slider fallback option.")]
    private bool includeRGBSliders = true;

    [Header("Positioning")]
    [SerializeField]
    [Tooltip("Position offset from canvas center.")]
    private Vector2 positionOffset = Vector2.zero;

    [SerializeField]
    [Tooltip("Enable automatic positioning for VR body-locked UI.")]
    private bool autoPositionForVR = true;

    /// <summary>
    /// Creates a complete color wheel UI setup
    /// </summary>
    [ContextMenu("Create Color Wheel UI")]
    public void CreateColorWheelUI()
    {
        if (parentCanvas == null)
        {
            parentCanvas = FindFirstObjectByType<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogError("No Canvas found in scene! Please create a Canvas first.");
                return;
            }
        }

        if (canvasRaycast == null)
        {
            canvasRaycast = FindFirstObjectByType<CanvasRaycast>();
            if (canvasRaycast == null)
            {
                Debug.LogWarning("No CanvasRaycast found in scene. Color integration may not work.");
            }
        }

        // Create main container
        GameObject colorWheelUI = CreateMainContainer();

        // Create color wheel components
        GameObject colorWheelPanel = CreateColorWheelPanel(colorWheelUI);
        
        // Create RGB slider panel (if enabled)
        GameObject rgbPanel = null;
        if (includeRGBSliders)
        {
            rgbPanel = CreateRGBSliderPanel(colorWheelUI);
        }

        // Create preset buttons (if enabled)
        GameObject presetPanel = null;
        if (includePresetButtons)
        {
            presetPanel = CreatePresetButtonPanel(colorWheelUI);
        }

        // Create preview and size controls
        GameObject controlsPanel = CreateControlsPanel(colorWheelUI);

        // Setup the ColorSelectorEnhanced component
        SetupColorSelectorEnhanced(colorWheelUI, colorWheelPanel, rgbPanel, presetPanel, controlsPanel);

        // Apply VR optimizations
        if (enableVROptimizations)
        {
            VRColorUtilities.OptimizeForVR(colorWheelUI);
        }

        Debug.Log("Color Wheel UI created successfully!");
    }

    private GameObject CreateMainContainer()
    {
        GameObject container = new GameObject("ColorWheel_UI");
        container.transform.SetParent(parentCanvas.transform, false);

        RectTransform rectTransform = container.AddComponent<RectTransform>();
        
        if (autoPositionForVR)
        {
            // Position for VR body-locked UI
            rectTransform.anchorMin = new Vector2(0.5f, 0.3f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.3f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = positionOffset;
        }
        else
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        return container;
    }

    private GameObject CreateColorWheelPanel(GameObject parent)
    {
        GameObject panel = new GameObject("ColorWheel_Panel");
        panel.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(colorWheelSize + 100f, colorWheelSize + 150f);
        rectTransform.anchoredPosition = Vector2.zero;

        // Create color wheel image
        GameObject wheelImage = new GameObject("ColorWheel_Image");
        wheelImage.transform.SetParent(panel.transform, false);
        
        RectTransform wheelRect = wheelImage.AddComponent<RectTransform>();
        wheelRect.sizeDelta = Vector2.one * colorWheelSize;
        wheelRect.anchoredPosition = new Vector2(0, 25f);

        RawImage rawImage = wheelImage.AddComponent<RawImage>();
        
        // Add ColorWheel component
        ColorWheel colorWheel = wheelImage.AddComponent<ColorWheel>();
        
        // Create selector
        GameObject selector = VRColorUtilities.CreateVRSelector(wheelImage.transform, enableVROptimizations ? 20f : 15f);

        // Create brightness slider
        GameObject brightnessSlider = CreateSlider(panel, "Brightness_Slider", new Vector2(0, -100f), new Vector2(colorWheelSize * 0.8f, 30f));
        
        // Create alpha slider
        GameObject alphaSlider = CreateSlider(panel, "Alpha_Slider", new Vector2(0, -140f), new Vector2(colorWheelSize * 0.8f, 30f));

        // Setup ColorWheel component references using the new setup method
        colorWheel.SetupReferences(
            rawImage,
            selector.GetComponent<RectTransform>(),
            brightnessSlider.GetComponent<Slider>(),
            alphaSlider.GetComponent<Slider>(),
            null, // colorPreview will be set by ColorSelectorEnhanced
            canvasRaycast
        );

        return panel;
    }

    private GameObject CreateRGBSliderPanel(GameObject parent)
    {
        GameObject panel = new GameObject("RGB_Panel");
        panel.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(colorWheelSize, 150f);
        rectTransform.anchoredPosition = new Vector2(colorWheelSize * 0.7f, 0);

        // Create RGB sliders
        GameObject redSlider = CreateSlider(panel, "Red_Slider", new Vector2(0, 50f), new Vector2(colorWheelSize * 0.8f, 30f));
        GameObject greenSlider = CreateSlider(panel, "Green_Slider", new Vector2(0, 0f), new Vector2(colorWheelSize * 0.8f, 30f));
        GameObject blueSlider = CreateSlider(panel, "Blue_Slider", new Vector2(0, -50f), new Vector2(colorWheelSize * 0.8f, 30f));

        // Set slider colors
        redSlider.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Color.red;
        greenSlider.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Color.green;
        blueSlider.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Color.blue;

        // Hide by default (ColorSelectorEnhanced will manage visibility)
        panel.SetActive(false);

        return panel;
    }

    private GameObject CreatePresetButtonPanel(GameObject parent)
    {
        GameObject panel = new GameObject("Preset_Panel");
        panel.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(colorWheelSize, 100f);
        rectTransform.anchoredPosition = new Vector2(-colorWheelSize * 0.7f, 0);

        // Create grid layout
        GridLayoutGroup gridLayout = panel.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = Vector2.one * (enableVROptimizations ? 40f : 30f);
        gridLayout.spacing = Vector2.one * 5f;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;

        // Create preset buttons
        for (int i = 0; i < 8; i++)
        {
            GameObject button = CreateColorPresetButton(panel, i);
        }

        return panel;
    }

    private GameObject CreateControlsPanel(GameObject parent)
    {
        GameObject panel = new GameObject("Controls_Panel");
        panel.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(colorWheelSize, 100f);
        rectTransform.anchoredPosition = new Vector2(0, -colorWheelSize * 0.7f);

        // Create color preview
        GameObject colorPreview = new GameObject("Color_Preview");
        colorPreview.transform.SetParent(panel.transform, false);
        
        RectTransform previewRect = colorPreview.AddComponent<RectTransform>();
        previewRect.sizeDelta = Vector2.one * 50f;
        previewRect.anchoredPosition = new Vector2(-60f, 25f);
        
        Image previewImage = colorPreview.AddComponent<Image>();
        previewImage.color = Color.red;

        // Create mark size slider
        GameObject markSizeSlider = CreateSlider(panel, "MarkSize_Slider", new Vector2(30f, 25f), new Vector2(120f, 30f));
        markSizeSlider.GetComponent<Slider>().maxValue = 100f;
        markSizeSlider.GetComponent<Slider>().value = 20f;

        // Create mark size preview
        GameObject markSizePreview = new GameObject("MarkSize_Preview");
        markSizePreview.transform.SetParent(panel.transform, false);
        
        RectTransform markPreviewRect = markSizePreview.AddComponent<RectTransform>();
        markPreviewRect.sizeDelta = Vector2.one * 30f;
        markPreviewRect.anchoredPosition = new Vector2(0, -25f);
        
        Image markPreviewImage = markSizePreview.AddComponent<Image>();
        markPreviewImage.color = Color.white;

        return panel;
    }

    private GameObject CreateSlider(GameObject parent, string name, Vector2 position, Vector2 size)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = sliderObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // Create Background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f);

        // Create Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Create Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = Color.white;

        // Create Handle Slide Area
        GameObject handleSlideArea = new GameObject("Handle Slide Area");
        handleSlideArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleSlideArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        // Create Handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleSlideArea.transform, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = Vector2.one * (enableVROptimizations ? 25f : 20f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        // Setup slider references
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        return sliderObj;
    }

    private GameObject CreateColorPresetButton(GameObject parent, int colorIndex)
    {
        GameObject button = new GameObject($"PresetButton_{colorIndex}");
        button.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = button.AddComponent<RectTransform>();
        Button buttonComponent = button.AddComponent<Button>();
        Image buttonImage = button.AddComponent<Image>();

        // Set preset color
        if (colorIndex < VRColorUtilities.VROptimizedColorPresets.Length)
        {
            buttonImage.color = VRColorUtilities.VROptimizedColorPresets[colorIndex];
        }

        return button;
    }

    private void SetupColorSelectorEnhanced(GameObject container, GameObject colorWheelPanel, GameObject rgbPanel, GameObject presetPanel, GameObject controlsPanel)
    {
        ColorSelectorEnhanced colorSelector = container.AddComponent<ColorSelectorEnhanced>();

        // Setup color wheel reference
        ColorWheel colorWheel = colorWheelPanel.GetComponentInChildren<ColorWheel>();
        
        // Use public properties instead of reflection
        if (colorWheel != null)
        {
            // Set the ColorWheel reference directly
            var colorWheelField = colorSelector.GetType().GetField("colorWheel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            colorWheelField?.SetValue(colorSelector, colorWheel);
        }

        // Setup RGB sliders (if available)
        if (rgbPanel != null)
        {
            Slider[] rgbSliders = rgbPanel.GetComponentsInChildren<Slider>();
            if (rgbSliders.Length >= 3)
            {
                var redSliderField = colorSelector.GetType().GetField("redSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var greenSliderField = colorSelector.GetType().GetField("greenSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var blueSliderField = colorSelector.GetType().GetField("blueSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                redSliderField?.SetValue(colorSelector, rgbSliders[0]);
                greenSliderField?.SetValue(colorSelector, rgbSliders[1]);
                blueSliderField?.SetValue(colorSelector, rgbSliders[2]);
            }
        }

        // Setup preset buttons (if available)
        if (presetPanel != null)
        {
            Button[] presetButtons = presetPanel.GetComponentsInChildren<Button>();
            var presetButtonsField = colorSelector.GetType().GetField("colorPresetButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            presetButtonsField?.SetValue(colorSelector, presetButtons);
        }

        // Setup controls
        if (controlsPanel != null)
        {
            Image colorPreview = controlsPanel.transform.Find("Color_Preview")?.GetComponent<Image>();
            Slider markSizeSlider = controlsPanel.transform.Find("MarkSize_Slider")?.GetComponent<Slider>();
            Image markSizePreview = controlsPanel.transform.Find("MarkSize_Preview")?.GetComponent<Image>();

            var colorPreviewField = colorSelector.GetType().GetField("colorPreviewImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var markSizeSliderField = colorSelector.GetType().GetField("markSizeSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var markSizePreviewField = colorSelector.GetType().GetField("markSizePreviewImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            colorPreviewField?.SetValue(colorSelector, colorPreview);
            markSizeSliderField?.SetValue(colorSelector, markSizeSlider);
            markSizePreviewField?.SetValue(colorSelector, markSizePreview);
        }

        // Setup CanvasRaycast reference
        var canvasRaycastField = colorSelector.GetType().GetField("canvasRaycast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        canvasRaycastField?.SetValue(colorSelector, canvasRaycast);

        // Set initial mode to Color Wheel
        colorSelector.SetColorSelectionMode(ColorSelectorEnhanced.ColorSelectionMode.ColorWheel);
    }

    [ContextMenu("Find CanvasRaycast")]
    public void FindCanvasRaycast()
    {
        canvasRaycast = FindFirstObjectByType<CanvasRaycast>();
        if (canvasRaycast != null)
        {
            Debug.Log($"Found CanvasRaycast on: {canvasRaycast.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("No CanvasRaycast found in scene.");
        }
    }

    [ContextMenu("Test VR Optimizations")]
    public void TestVROptimizations()
    {
        GameObject testUI = GameObject.Find("ColorWheel_UI");
        if (testUI != null)
        {
            VRColorUtilities.OptimizeForVR(testUI, 1.5f);
            Debug.Log("Applied VR optimizations to existing ColorWheel UI");
        }
        else
        {
            Debug.LogWarning("No ColorWheel UI found. Create one first.");
        }
    }
}

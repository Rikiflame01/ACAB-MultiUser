using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;

/// <summary>
/// Simplified generator focused specifically on creating the sliders and controls for the ColorWheel system
/// This version uses more compatible Unity UI creation methods
/// </summary>
public class ColorWheelSliderGenerator : MonoBehaviour
{
    [Header("Target Setup")]
    [SerializeField]
    [Tooltip("The ColorWheel component that needs sliders")]
    private ColorWheel targetColorWheel;

    [SerializeField]
    [Tooltip("The Canvas to create the UI under (will find automatically if not set)")]
    private Canvas targetCanvas;

    [Header("Slider Settings")]
    [SerializeField]
    [Tooltip("Width of the sliders")]
    private float sliderWidth = 200f;

    [SerializeField]
    [Tooltip("Height of the sliders")]
    private float sliderHeight = 20f;

    [SerializeField]
    [Tooltip("Spacing between UI elements")]
    private float elementSpacing = 30f;

    [SerializeField]
    [Tooltip("Size of the color preview square")]
    private float previewSize = 50f;

    [Header("VR Optimization")]
    [SerializeField]
    [Tooltip("Scale up UI elements for VR")]
    private bool vrOptimized = true;

    [SerializeField]
    [Tooltip("Scale factor for VR")]
    private float vrScale = 1.5f;

    /// <summary>
    /// Creates the slider controls for the color wheel
    /// </summary>
    [ContextMenu("Create Color Wheel Sliders")]
    public void CreateColorWheelSliders()
    {
        if (targetColorWheel == null)
        {
            Debug.LogError("Target ColorWheel is not assigned!");
            return;
        }

        // Find canvas if not assigned
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
            if (targetCanvas == null)
            {
                Debug.LogError("No Canvas found in scene!");
                return;
            }
        }

        // Apply VR scaling if needed
        float scale = vrOptimized ? vrScale : 1f;

        // Ensure proper canvas setup for UI interaction
        EnsureCanvasUIComponents(targetCanvas.gameObject);

        // Create main control panel
        GameObject controlPanel = CreateControlPanel(scale);

        // Create color preview
        GameObject colorPreview = CreateColorPreview(controlPanel, scale);

        // Create brightness slider
        GameObject brightnessSlider = CreateSlider(controlPanel, "Brightness", 1f, scale, 0);

        // Create alpha slider
        GameObject alphaSlider = CreateSlider(controlPanel, "Alpha", 1f, scale, 1);

        // Create preset buttons (optional)
        GameObject presetPanel = CreatePresetButtons(controlPanel, scale);

        // Connect to ColorWheel component
        ConnectToColorWheel(colorPreview, brightnessSlider, alphaSlider);

        Debug.Log("✅ Color Wheel Sliders created successfully!");
        Debug.Log("📋 Next steps:");
        Debug.Log("1. Assign the Canvas Raycast reference in the ColorWheel component");
        Debug.Log("2. Test the sliders in Play mode");
    }

    private GameObject CreateControlPanel(float scale)
    {
        GameObject panel = new GameObject("ColorWheel_Controls");
        panel.transform.SetParent(targetCanvas.transform, false);

        // Add RectTransform
        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        
        // Position the panel (adjust as needed)
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(50, -50);
        rectTransform.sizeDelta = new Vector2(250 * scale, 200 * scale);

        // Add background
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Semi-transparent dark background

        return panel;
    }

    private GameObject CreateColorPreview(GameObject parent, float scale)
    {
        GameObject preview = new GameObject("Color_Preview");
        preview.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = preview.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0, -10 * scale);
        rectTransform.sizeDelta = new Vector2(previewSize * scale, previewSize * scale);

        Image previewImage = preview.AddComponent<Image>();
        previewImage.color = Color.red; // Default color

        // Add border
        Outline outline = preview.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2, 2);

        return preview;
    }

    private GameObject CreateSlider(GameObject parent, string label, float defaultValue, float scale, int index)
    {
        // Create slider container
        GameObject sliderContainer = new GameObject($"{label}_Container");
        sliderContainer.transform.SetParent(parent.transform, false);

        RectTransform containerRect = sliderContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(1, 1);
        containerRect.pivot = new Vector2(0.5f, 1);
        
        float yPos = -(70 + (index * elementSpacing)) * scale;
        containerRect.anchoredPosition = new Vector2(0, yPos);
        containerRect.sizeDelta = new Vector2(0, sliderHeight * scale);

        // Create label
        GameObject labelObj = new GameObject($"{label}_Label");
        labelObj.transform.SetParent(sliderContainer.transform, false);

        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.5f);
        labelRect.anchorMax = new Vector2(0, 0.5f);
        labelRect.pivot = new Vector2(0, 0.5f);
        labelRect.anchoredPosition = new Vector2(10 * scale, 0);
        labelRect.sizeDelta = new Vector2(60 * scale, 20 * scale);

        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = label;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = Mathf.RoundToInt(12 * scale);
        labelText.color = Color.white;

        // Create actual slider
        GameObject sliderObj = new GameObject($"{label}_Slider");
        sliderObj.transform.SetParent(sliderContainer.transform, false);

        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0.5f);
        sliderRect.anchorMax = new Vector2(1, 0.5f);
        sliderRect.pivot = new Vector2(0, 0.5f);
        sliderRect.anchoredPosition = new Vector2(70 * scale, 0);
        sliderRect.sizeDelta = new Vector2(-80 * scale, sliderHeight * scale);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = defaultValue;
        slider.wholeNumbers = false;

        // Create slider background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(sliderObj.transform, false);

        RectTransform bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        bgImage.type = Image.Type.Sliced;

        // Create fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);

        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;
        fillAreaRect.anchoredPosition = Vector2.zero;

        // Create fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);

        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;

        Image fillImage = fill.AddComponent<Image>();
        
        // Set fill color based on slider type
        if (label == "Brightness")
        {
            fillImage.color = Color.white;
        }
        else if (label == "Alpha")
        {
            fillImage.color = new Color(1f, 1f, 1f, 0.8f);
        }

        // Create handle slide area
        GameObject handleSlideArea = new GameObject("Handle Slide Area");
        handleSlideArea.transform.SetParent(sliderObj.transform, false);

        RectTransform handleAreaRect = handleSlideArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;
        handleAreaRect.anchoredPosition = Vector2.zero;

        // Create handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleSlideArea.transform, false);

        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20 * scale, 20 * scale);
        handleRect.anchoredPosition = Vector2.zero;

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        // Add button component for interaction
        Button handleButton = handle.AddComponent<Button>();
        handleButton.targetGraphic = handleImage;

        // Assign slider components
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = fillImage;
        slider.direction = Slider.Direction.LeftToRight;

        return sliderObj;
    }

    private GameObject CreatePresetButtons(GameObject parent, float scale)
    {
        GameObject presetPanel = new GameObject("ColorHistory_Panel");
        presetPanel.transform.SetParent(parent.transform, false);

        RectTransform presetRect = presetPanel.AddComponent<RectTransform>();
        presetRect.anchorMin = new Vector2(0, 0);
        presetRect.anchorMax = new Vector2(1, 0);
        presetRect.pivot = new Vector2(0.5f, 0);
        presetRect.anchoredPosition = new Vector2(0, 10 * scale);
        presetRect.sizeDelta = new Vector2(0, 40 * scale);

        // Add title for the color history
        GameObject titleObj = new GameObject("ColorHistory_Title");
        titleObj.transform.SetParent(presetPanel.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -5 * scale);
        titleRect.sizeDelta = new Vector2(120 * scale, 15 * scale);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "Recent Colors";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = Mathf.RoundToInt(10 * scale);
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;

        // Create color history buttons (8 slots)
        int historyCount = 8;
        float buttonSize = 22 * scale;
        float spacing = 4 * scale;
        float totalWidth = (historyCount * buttonSize) + ((historyCount - 1) * spacing);
        float startX = -totalWidth * 0.5f + buttonSize * 0.5f;

        for (int i = 0; i < historyCount; i++)
        {
            GameObject colorButton = new GameObject($"ColorHistory_{i}");
            colorButton.transform.SetParent(presetPanel.transform, false);

            RectTransform buttonRect = colorButton.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
            buttonRect.anchoredPosition = new Vector2(startX + i * (buttonSize + spacing), 5 * scale);

            Image buttonImage = colorButton.AddComponent<Image>();
            // Start with default colors, will be updated by ColorWheel
            buttonImage.color = i == 0 ? Color.red : Color.gray;
            // Ensure the image can be raycast for button interactions
            buttonImage.raycastTarget = true;

            Button button = colorButton.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            
            // Ensure the button is interactable and has proper settings
            button.interactable = true;
            button.transition = Selectable.Transition.ColorTint;

            // Add click event for color history
            int historyIndex = i; // Capture for closure
            
            // Add the click listener
            button.onClick.AddListener(() => {
                if (targetColorWheel != null)
                {
                    var colorHistory = targetColorWheel.GetComponent<ColorHistory>();
                    if (colorHistory != null)
                    {
                        colorHistory.LoadColorFromHistory(historyIndex);
                    }
                }
            });

            // Add outline
            Outline outline = colorButton.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1, 1);

            // Make inactive colors more transparent initially
            if (i > 0)
            {
                var tempColor = buttonImage.color;
                tempColor.a = 0.3f;
                buttonImage.color = tempColor;
            }
        }

        return presetPanel;
    }

    /// <summary>
    /// Ensures the canvas has proper components for UI interaction
    /// </summary>
    private void EnsureCanvasUIComponents(GameObject canvasObject)
    {
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            // Check for GraphicRaycaster
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            // Check for CanvasScaler
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }
        }
        
        // Ensure EventSystem exists in scene
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            
            // Try to use Input System UI Input Module, fall back to Standalone if not available
            try
            {
                var inputModule = eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            catch (System.Exception)
            {
                // Fallback to old input module if Input System UI is not available
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
        else
        {
            // Check if existing EventSystem has the right input module
            var currentEventSystem = UnityEngine.EventSystems.EventSystem.current;
            var oldInputModule = currentEventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            var newInputModule = currentEventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            
            if (oldInputModule != null && newInputModule == null)
            {
                try
                {
                    // Replace old input module with new one
                    DestroyImmediate(oldInputModule);
                    currentEventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
                catch (System.Exception)
                {
                    // Re-add the old one if new one fails
                    if (currentEventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
                    {
                        currentEventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    }
                }
            }
        }
    }

    private void ConnectToColorWheel(GameObject colorPreview, GameObject brightnessSlider, GameObject alphaSlider)
    {
        if (targetColorWheel == null) return;

        // Get components
        Image previewImage = colorPreview.GetComponent<Image>();
        Slider brightnessSliderComponent = brightnessSlider.GetComponent<Slider>();
        Slider alphaSliderComponent = alphaSlider.GetComponent<Slider>();

        // Connect via public properties
        targetColorWheel.ColorPreview = previewImage;
        targetColorWheel.BrightnessSlider = brightnessSliderComponent;
        targetColorWheel.AlphaSlider = alphaSliderComponent;

        // Add ColorHistory component if it doesn't exist
        ColorHistory colorHistory = targetColorWheel.GetComponent<ColorHistory>();
        if (colorHistory == null)
        {
            colorHistory = targetColorWheel.gameObject.AddComponent<ColorHistory>();
        }
    }

    /// <summary>
    /// Find and auto-assign the ColorWheel component
    /// </summary>
    [ContextMenu("Find ColorWheel Component")]
    public void FindColorWheelComponent()
    {
        if (targetColorWheel == null)
        {
            targetColorWheel = FindFirstObjectByType<ColorWheel>();
            if (targetColorWheel != null)
            {
                Debug.Log($"✅ Found ColorWheel component on: {targetColorWheel.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("❌ No ColorWheel component found in scene!");
            }
        }
    }

    /// <summary>
    /// Clean up existing sliders (useful for regenerating)
    /// </summary>
    [ContextMenu("Clean Up Existing Controls")]
    public void CleanUpExistingControls()
    {
        GameObject existingControls = GameObject.Find("ColorWheel_Controls");
        if (existingControls != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existingControls);
            }
            else
            {
                DestroyImmediate(existingControls);
            }
            Debug.Log("✅ Cleaned up existing color wheel controls");
        }
        else
        {
            Debug.Log("ℹ️ No existing controls found to clean up");
        }
    }

    /// <summary>
    /// Complete setup: Clean up + Create new sliders
    /// </summary>
    [ContextMenu("Complete Slider Setup")]
    public void CompleteSliderSetup()
    {
        FindColorWheelComponent();
        CleanUpExistingControls();
        CreateColorWheelSliders();
    }
}

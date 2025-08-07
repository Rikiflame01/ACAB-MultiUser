using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ColorWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Color Wheel Components")]
    [SerializeField]
    [Tooltip("The RawImage component displaying the color wheel texture.")]
    private RawImage colorWheelImage;

    [SerializeField]
    [Tooltip("The selector indicator (small circle) that shows current selection.")]
    private RectTransform selector;

    [SerializeField]
    [Tooltip("Brightness/Value slider for HSV adjustment.")]
    private Slider brightnessSlider;

    [SerializeField]
    [Tooltip("Alpha/Opacity slider.")]
    private Slider alphaSlider;

    [SerializeField]
    [Tooltip("Image to preview the selected color.")]
    private Image colorPreview;

    [SerializeField]
    [Tooltip("Reference to the CanvasRaycast script to update the mark color.")]
    private CanvasRaycast canvasRaycast;

    [Header("Color Wheel Settings")]
    [SerializeField]
    [Tooltip("Size of the color wheel texture (resolution).")]
    private int textureSize = 256;

    [SerializeField]
    [Tooltip("Size of the selector indicator.")]
    private float selectorSize = 10f;

    [SerializeField]
    [Tooltip("Whether to use VR-optimized interaction (larger touch targets).")]
    private bool vrOptimized = true;

    [SerializeField]
    [Tooltip("Scale factor for VR pointer interactions.")]
    private float vrPointerScale = 2f;

    // Color properties
    private Color currentColor = Color.red;
    private float currentHue = 0f;
    private float currentSaturation = 1f;
    private float currentValue = 1f;
    private float currentAlpha = 1f;

    // Texture and interaction
    private Texture2D colorWheelTexture;
    private RectTransform wheelRect;
    private bool isDragging = false;
    private Vector2 wheelCenter;
    private float wheelRadius;

    // Events
    public System.Action<Color> OnColorChanged;

    // Public properties for external access (needed for prefab generator)
    public RawImage ColorWheelImage
    {
        get => colorWheelImage;
        set => colorWheelImage = value;
    }

    public RectTransform Selector
    {
        get => selector;
        set => selector = value;
    }

    public Slider BrightnessSlider
    {
        get => brightnessSlider;
        set => brightnessSlider = value;
    }

    public Slider AlphaSlider
    {
        get => alphaSlider;
        set => alphaSlider = value;
    }

    public Image ColorPreview
    {
        get => colorPreview;
        set => colorPreview = value;
    }

    public CanvasRaycast CanvasRaycastReference
    {
        get => canvasRaycast;
        set => canvasRaycast = value;
    }

    private void Awake()
    {
        ValidateComponents();
        GenerateColorWheelTexture();
        SetupComponents();
    }

    private void Start()
    {
        // Initialize with current color
        UpdateColorDisplay();
        UpdateCanvasRaycastColor();
    }

    private void OnEnable()
    {
        // Subscribe to slider events
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        
        if (alphaSlider != null)
            alphaSlider.onValueChanged.AddListener(OnAlphaChanged);
    }

    private void OnDisable()
    {
        // Unsubscribe from slider events
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
        
        if (alphaSlider != null)
            alphaSlider.onValueChanged.RemoveListener(OnAlphaChanged);
    }

    private void ValidateComponents()
    {
        if (colorWheelImage == null)
        {
            Debug.LogError("Color Wheel Image is not assigned!", this);
            enabled = false;
            return;
        }

        if (selector == null)
        {
            Debug.LogError("Selector is not assigned!", this);
            enabled = false;
            return;
        }

        if (canvasRaycast == null)
        {
            Debug.LogError("CanvasRaycast reference is not assigned!", this);
            enabled = false;
            return;
        }

        wheelRect = colorWheelImage.rectTransform;
    }

    private void SetupComponents()
    {
        // Calculate wheel dimensions
        Rect rect = wheelRect.rect;
        wheelRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        wheelCenter = Vector2.zero; // Relative to the wheel's center

        // Setup selector
        if (selector != null)
        {
            selector.sizeDelta = Vector2.one * selectorSize;
            // Position selector at initial hue/saturation
            UpdateSelectorPosition();
        }

        // Setup brightness slider
        if (brightnessSlider != null)
        {
            brightnessSlider.value = currentValue;
        }

        // Setup alpha slider
        if (alphaSlider != null)
        {
            alphaSlider.value = currentAlpha;
        }

        // VR optimization - increase touch target sizes
        if (vrOptimized)
        {
            // Make the color wheel easier to interact with in VR
            var canvasGroup = colorWheelImage.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = colorWheelImage.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void GenerateColorWheelTexture()
    {
        colorWheelTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGB24, false);
        colorWheelTexture.filterMode = FilterMode.Bilinear;
        colorWheelTexture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
        float radius = textureSize * 0.5f;

        for (int x = 0; x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                Vector2 pos = new Vector2(x, y);
                Vector2 offset = pos - center;
                float distance = offset.magnitude;

                if (distance <= radius)
                {
                    // Calculate hue from angle
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float hue = (angle * Mathf.Rad2Deg + 360f) % 360f / 360f;

                    // Calculate saturation from distance
                    float saturation = distance / radius;

                    // Use current brightness value
                    Color pixelColor = Color.HSVToRGB(hue, saturation, currentValue);
                    colorWheelTexture.SetPixel(x, y, pixelColor);
                }
                else
                {
                    // Outside the circle - transparent or background color
                    colorWheelTexture.SetPixel(x, y, Color.clear);
                }
            }
        }

        colorWheelTexture.Apply();
        colorWheelImage.texture = colorWheelTexture;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            wheelRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            if (IsPointInWheel(localPoint))
            {
                isDragging = true;
                UpdateColorFromPosition(localPoint);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            wheelRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            UpdateColorFromPosition(localPoint);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    private bool IsPointInWheel(Vector2 localPoint)
    {
        // Check if point is within the wheel radius
        float distance = Vector2.Distance(localPoint, wheelCenter);
        return distance <= wheelRadius;
    }

    private void UpdateColorFromPosition(Vector2 localPoint)
    {
        // Convert local point to hue and saturation
        Vector2 offset = localPoint - wheelCenter;
        float distance = offset.magnitude;

        // Clamp to wheel radius
        if (distance > wheelRadius)
        {
            offset = offset.normalized * wheelRadius;
            distance = wheelRadius;
        }

        // Calculate hue from angle
        float angle = Mathf.Atan2(offset.y, offset.x);
        currentHue = (angle * Mathf.Rad2Deg + 360f) % 360f / 360f;

        // Calculate saturation from distance
        currentSaturation = distance / wheelRadius;

        // Update color and UI
        UpdateColorDisplay();
        UpdateSelectorPosition();
        UpdateCanvasRaycastColor();
    }

    private void UpdateSelectorPosition()
    {
        if (selector == null) return;

        // Convert hue/saturation back to position
        float angle = currentHue * 360f * Mathf.Deg2Rad;
        float distance = currentSaturation * wheelRadius;

        Vector2 position = new Vector2(
            Mathf.Cos(angle) * distance,
            Mathf.Sin(angle) * distance
        );

        selector.anchoredPosition = position;
    }

    private void OnBrightnessChanged(float value)
    {
        currentValue = value;
        
        // Regenerate wheel texture with new brightness
        GenerateColorWheelTexture();
        
        // Update current color
        UpdateColorDisplay();
        UpdateCanvasRaycastColor();
    }

    private void OnAlphaChanged(float value)
    {
        currentAlpha = value;
        UpdateColorDisplay();
        UpdateCanvasRaycastColor();
    }

    private void UpdateColorDisplay()
    {
        // Convert HSV to RGB
        currentColor = Color.HSVToRGB(currentHue, currentSaturation, currentValue);
        currentColor.a = currentAlpha;

        // Update preview
        if (colorPreview != null)
        {
            colorPreview.color = currentColor;
        }

        // Trigger event
        OnColorChanged?.Invoke(currentColor);
    }

    private void UpdateCanvasRaycastColor()
    {
        if (canvasRaycast != null)
        {
            canvasRaycast.markColor = currentColor;
        }
    }

    // Public methods for external control
    public void SetColor(Color color)
    {
        Color.RGBToHSV(color, out currentHue, out currentSaturation, out currentValue);
        currentAlpha = color.a;

        // Temporarily disable slider events to prevent feedback loops
        bool brightnessEventEnabled = false;
        bool alphaEventEnabled = false;

        // Update sliders without triggering events
        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
            brightnessEventEnabled = true;
            brightnessSlider.value = currentValue;
        }
        if (alphaSlider != null)
        {
            alphaSlider.onValueChanged.RemoveListener(OnAlphaChanged);
            alphaEventEnabled = true;
            alphaSlider.value = currentAlpha;
        }

        // Regenerate texture and update display
        GenerateColorWheelTexture();
        UpdateColorDisplay();
        UpdateSelectorPosition();

        // Re-enable slider events
        if (brightnessEventEnabled && brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }
        if (alphaEventEnabled && alphaSlider != null)
        {
            alphaSlider.onValueChanged.AddListener(OnAlphaChanged);
        }

        // Update CanvasRaycast with the new color
        UpdateCanvasRaycastColor();
    }

    public Color GetColor()
    {
        return currentColor;
    }

    public void SetHue(float hue)
    {
        currentHue = Mathf.Clamp01(hue);
        UpdateColorDisplay();
        UpdateSelectorPosition();
        UpdateCanvasRaycastColor();
    }

    public void SetSaturation(float saturation)
    {
        currentSaturation = Mathf.Clamp01(saturation);
        UpdateColorDisplay();
        UpdateSelectorPosition();
        UpdateCanvasRaycastColor();
    }

    public void SetBrightness(float brightness)
    {
        currentValue = Mathf.Clamp01(brightness);
        if (brightnessSlider != null)
            brightnessSlider.value = currentValue;
        OnBrightnessChanged(currentValue);
    }

    public void SetAlpha(float alpha)
    {
        currentAlpha = Mathf.Clamp01(alpha);
        if (alphaSlider != null)
            alphaSlider.value = currentAlpha;
        OnAlphaChanged(currentAlpha);
    }

    // VR-specific helper methods
    public void EnableVRMode(bool enabled)
    {
        vrOptimized = enabled;
        
        if (enabled)
        {
            // Increase selector size for VR
            if (selector != null)
            {
                selector.sizeDelta = Vector2.one * (selectorSize * vrPointerScale);
            }
        }
        else
        {
            // Reset to normal size
            if (selector != null)
            {
                selector.sizeDelta = Vector2.one * selectorSize;
            }
        }
    }

    // Preset colors for quick selection
    public void SetPresetColor(int presetIndex)
    {
        Color[] presets = {
            Color.red,      // 0
            Color.green,    // 1
            Color.blue,     // 2
            Color.yellow,   // 3
            Color.cyan,     // 4
            Color.magenta,  // 5
            Color.white,    // 6
            Color.black     // 7
        };

        if (presetIndex >= 0 && presetIndex < presets.Length)
        {
            SetColor(presets[presetIndex]);
        }
    }

    // Context menu methods for testing
    [ContextMenu("Set Red")]
    public void TestSetRed() => SetPresetColor(0);

    [ContextMenu("Set Green")]
    public void TestSetGreen() => SetPresetColor(1);

    [ContextMenu("Set Blue")]
    public void TestSetBlue() => SetPresetColor(2);

    [ContextMenu("Test SetColor Directly")]
    public void TestSetColorDirectly()
    {
        SetColor(Color.blue);
    }

    [ContextMenu("Enable VR Mode")]
    public void TestEnableVRMode() => EnableVRMode(true);

    [ContextMenu("Disable VR Mode")]
    public void TestDisableVRMode() => EnableVRMode(false);

    /// <summary>
    /// Setup all component references in one call (useful for programmatic setup)
    /// </summary>
    public void SetupReferences(RawImage wheelImage, RectTransform selectorTransform, 
        Slider brightness, Slider alpha, Image preview, CanvasRaycast raycast)
    {
        colorWheelImage = wheelImage;
        selector = selectorTransform;
        brightnessSlider = brightness;
        alphaSlider = alpha;
        colorPreview = preview;
        canvasRaycast = raycast;

        // Re-validate and setup with new references
        ValidateComponents();
        if (enabled)
        {
            GenerateColorWheelTexture();
            SetupComponents();
            UpdateColorDisplay();
            UpdateCanvasRaycastColor();
        }
    }

    private void OnDestroy()
    {
        if (colorWheelTexture != null)
        {
            DestroyImmediate(colorWheelTexture);
        }
    }
}

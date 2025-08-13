using UnityEngine;
using UnityEngine.UI;

public class ColorSelector : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Slider for the Red component (0 to 1).")]
    private Slider redSlider;

    [SerializeField]
    [Tooltip("Slider for the Green component (0 to 1).")]
    private Slider greenSlider;

    [SerializeField]
    [Tooltip("Slider for the Blue component (0 to 1).")]
    private Slider blueSlider;

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

    [SerializeField]
    [Tooltip("TextMeshProUGUI to display the current mark size value.")]
    private TMPro.TextMeshProUGUI markSizeText;

    private const float MIN_MARK_SIZE = 1f; // Minimum mark size (as defined in requirements)
    private const float MAX_MARK_SIZE = 100f; // Maximum mark size
    private const float MIN_PREVIEW_SCALE = 0.4f; // Scale at mark size 1
    private const float MAX_PREVIEW_SCALE = 1f; // Scale at mark size 100

    private void Awake()
    {
        // Validate references
        if (redSlider == null || greenSlider == null || blueSlider == null || markSizeSlider == null)
        {
            Debug.LogError("One or more sliders are not assigned in the Inspector!", this);
            enabled = false;
            return;
        }

        if (colorPreviewImage == null)
        {
            Debug.LogError("Color Preview Image is not assigned in the Inspector!", this);
            enabled = false;
            return;
        }

        if (markSizePreviewImage == null)
        {
            Debug.LogError("Mark Size Preview Image is not assigned in the Inspector!", this);
            enabled = false;
            return;
        }

        if (canvasRaycast == null)
        {
            Debug.LogError("CanvasRaycast reference is not assigned in the Inspector!", this);
            enabled = false;
            return;
        }

        if (markSizeText == null)
        {
            Debug.LogWarning("Mark Size TextMeshProUGUI is not assigned in the Inspector! Numerical display will be disabled.", this);
        }
    }

    private void OnEnable()
    {
        // Subscribe to the sliders' value changed events
        redSlider.onValueChanged.AddListener(UpdateColor);
        greenSlider.onValueChanged.AddListener(UpdateColor);
        blueSlider.onValueChanged.AddListener(UpdateColor);
        markSizeSlider.onValueChanged.AddListener(UpdateMarkSize);

        // Initialize the color and mark size based on the current slider values
        UpdateColor(0f);
        UpdateMarkSize(0f);
    }

    private void OnDisable()
    {
        // Unsubscribe from the events to prevent memory leaks
        redSlider.onValueChanged.RemoveListener(UpdateColor);
        greenSlider.onValueChanged.RemoveListener(UpdateColor);
        blueSlider.onValueChanged.RemoveListener(UpdateColor);
        markSizeSlider.onValueChanged.RemoveListener(UpdateMarkSize);
    }

    private void UpdateColor(float value)
    {
        // Get the RGB values from the sliders (0 to 1 range)
        float r = redSlider.value;
        float g = greenSlider.value;
        float b = blueSlider.value;

        // Create the color
        Color selectedColor = new Color(r, g, b);

        // Update the preview image
        colorPreviewImage.color = selectedColor;

        // Update the markColor in the CanvasRaycast script
        canvasRaycast.markColor = selectedColor;
    }

    private void UpdateMarkSize(float value)
    {
        // Get the mark size from the slider (0 to 100 range)
        float markSize = markSizeSlider.value;

        // Ensure markSize is at least 1 (as per requirements)
        markSize = Mathf.Max(MIN_MARK_SIZE, markSize);

        // Update the markSize in the CanvasRaycast script
        canvasRaycast.markSize = markSize;

        // Update the numerical display if assigned
        if (markSizeText != null)
        {
            markSizeText.text = Mathf.RoundToInt(markSize).ToString();
        }

        // Map the markSize (1 to 100) to the preview scale (0.4 to 1)
        float t = (markSize - MIN_MARK_SIZE) / (MAX_MARK_SIZE - MIN_MARK_SIZE); // Normalize to 0-1
        float previewScale = Mathf.Lerp(MIN_PREVIEW_SCALE, MAX_PREVIEW_SCALE, t); // Interpolate between 0.4 and 1

        // Apply the scale to the preview image's RectTransform
        markSizePreviewImage.rectTransform.localScale = new Vector3(previewScale, previewScale, 1f);
    }
}
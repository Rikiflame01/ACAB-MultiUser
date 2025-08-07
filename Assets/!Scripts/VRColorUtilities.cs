using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utility class for VR-optimized color selection and UI helpers
/// </summary>
public static class VRColorUtilities
{
    /// <summary>
    /// Generates a color wheel texture with specified parameters
    /// </summary>
    public static Texture2D GenerateColorWheelTexture(int size, float brightness = 1f, bool antialias = true)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        texture.filterMode = antialias ? FilterMode.Bilinear : FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
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

                    // Apply antialiasing at edges
                    float alpha = 1f;
                    if (antialias && distance > radius - 1f)
                    {
                        alpha = radius - distance;
                    }

                    Color pixelColor = Color.HSVToRGB(hue, saturation, brightness);
                    pixelColor.a = alpha;
                    texture.SetPixel(x, y, pixelColor);
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Converts a world position to UV coordinates on a color wheel
    /// </summary>
    public static Vector2 WorldToColorWheelUV(Vector3 worldPos, Transform wheelTransform, float wheelRadius)
    {
        Vector3 localPos = wheelTransform.InverseTransformPoint(worldPos);
        Vector2 offset = new Vector2(localPos.x, localPos.y);
        
        // Normalize to 0-1 range
        Vector2 uv = (offset / wheelRadius + Vector2.one) * 0.5f;
        return uv;
    }

    /// <summary>
    /// Converts UV coordinates to HSV values for color wheel
    /// </summary>
    public static void UVToHSV(Vector2 uv, out float hue, out float saturation)
    {
        // Convert UV (0-1) to centered coordinates (-1 to 1)
        Vector2 centered = (uv - Vector2.one * 0.5f) * 2f;
        
        // Calculate angle for hue
        float angle = Mathf.Atan2(centered.y, centered.x);
        hue = (angle * Mathf.Rad2Deg + 360f) % 360f / 360f;
        
        // Calculate distance for saturation
        saturation = Mathf.Clamp01(centered.magnitude);
    }

    /// <summary>
    /// Converts HSV values to UV coordinates for color wheel
    /// </summary>
    public static Vector2 HSVToUV(float hue, float saturation)
    {
        float angle = hue * 360f * Mathf.Deg2Rad;
        Vector2 centered = new Vector2(
            Mathf.Cos(angle) * saturation,
            Mathf.Sin(angle) * saturation
        );
        
        // Convert centered coordinates (-1 to 1) to UV (0-1)
        return (centered + Vector2.one) * 0.5f;
    }

    /// <summary>
    /// Optimizes UI elements for VR interaction
    /// </summary>
    public static void OptimizeForVR(GameObject uiElement, float scaleFactor = 2f)
    {
        if (uiElement == null) return;

        // Increase button sizes
        Button[] buttons = uiElement.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            var rectTransform = button.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta *= scaleFactor;
            }
        }

        // Increase slider handle sizes
        Slider[] sliders = uiElement.GetComponentsInChildren<Slider>(true);
        foreach (var slider in sliders)
        {
            var handleRect = slider.handleRect;
            if (handleRect != null)
            {
                handleRect.sizeDelta *= scaleFactor;
            }
        }

        // Increase toggle sizes
        Toggle[] toggles = uiElement.GetComponentsInChildren<Toggle>(true);
        foreach (var toggle in toggles)
        {
            var rectTransform = toggle.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta *= scaleFactor;
            }
        }
    }

    /// <summary>
    /// Creates a high-contrast selector indicator for VR
    /// </summary>
    public static GameObject CreateVRSelector(Transform parent, float size = 15f)
    {
        GameObject selector = new GameObject("ColorWheel_Selector");
        selector.transform.SetParent(parent, false);

        // Add RectTransform
        RectTransform rectTransform = selector.AddComponent<RectTransform>();
        rectTransform.sizeDelta = Vector2.one * size;
        rectTransform.anchorMin = Vector2.one * 0.5f;
        rectTransform.anchorMax = Vector2.one * 0.5f;

        // Add Image component
        Image image = selector.AddComponent<Image>();
        
        // Create a circular sprite with white center and black outline
        Texture2D selectorTexture = CreateSelectorTexture((int)size);
        Sprite selectorSprite = Sprite.Create(selectorTexture, 
            new Rect(0, 0, selectorTexture.width, selectorTexture.height), 
            Vector2.one * 0.5f);
        
        image.sprite = selectorSprite;
        image.type = Image.Type.Simple;

        return selector;
    }

    /// <summary>
    /// Creates a texture for the color wheel selector with high contrast
    /// </summary>
    private static Texture2D CreateSelectorTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = Vector2.one * (size * 0.5f);
        float radius = size * 0.5f;
        float innerRadius = radius * 0.6f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);

                if (distance <= radius)
                {
                    if (distance <= innerRadius)
                    {
                        // White center
                        texture.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        // Black outline
                        texture.SetPixel(x, y, Color.black);
                    }
                }
                else
                {
                    // Transparent outside
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Color presets optimized for VR visibility and common use
    /// </summary>
    public static readonly Color[] VROptimizedColorPresets = {
        Color.red,                          // Primary Red
        new Color(0f, 0.8f, 0f),           // VR-friendly Green (less bright)
        Color.blue,                         // Primary Blue
        Color.yellow,                       // Primary Yellow
        Color.cyan,                         // Primary Cyan
        Color.magenta,                      // Primary Magenta
        new Color(1f, 0.5f, 0f),           // Orange
        new Color(0.5f, 0f, 1f),           // Purple
        Color.white,                        // White
        new Color(0.2f, 0.2f, 0.2f),       // Dark Gray (better than black for VR)
        new Color(1f, 0.75f, 0.8f),        // Pink
        new Color(0.5f, 0.25f, 0f),        // Brown
        new Color(0f, 0.5f, 0.5f),         // Teal
        new Color(0.75f, 0.75f, 0f),       // Olive
        new Color(0.5f, 0.5f, 0.5f),       // Gray
        new Color(0.9f, 0.9f, 0.9f)        // Light Gray
    };

    /// <summary>
    /// Gets a color preset by name for easy access
    /// </summary>
    public static Color GetPresetColor(string colorName)
    {
        switch (colorName.ToLower())
        {
            case "red": return Color.red;
            case "green": return new Color(0f, 0.8f, 0f);
            case "blue": return Color.blue;
            case "yellow": return Color.yellow;
            case "cyan": return Color.cyan;
            case "magenta": return Color.magenta;
            case "orange": return new Color(1f, 0.5f, 0f);
            case "purple": return new Color(0.5f, 0f, 1f);
            case "white": return Color.white;
            case "black": return new Color(0.2f, 0.2f, 0.2f);
            case "pink": return new Color(1f, 0.75f, 0.8f);
            case "brown": return new Color(0.5f, 0.25f, 0f);
            case "teal": return new Color(0f, 0.5f, 0.5f);
            case "olive": return new Color(0.75f, 0.75f, 0f);
            case "gray": return new Color(0.5f, 0.5f, 0.5f);
            case "lightgray": return new Color(0.9f, 0.9f, 0.9f);
            default: return Color.white;
        }
    }

    /// <summary>
    /// Validates color for VR visibility (ensures minimum contrast)
    /// </summary>
    public static Color ValidateVRColor(Color color, float minBrightness = 0.1f)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        
        // Ensure minimum brightness for VR visibility
        if (v < minBrightness)
        {
            v = minBrightness;
            color = Color.HSVToRGB(h, s, v);
            color.a = color.a; // Preserve alpha
        }

        return color;
    }

    /// <summary>
    /// Creates a gradient texture for brightness/alpha sliders
    /// </summary>
    public static Texture2D CreateGradientTexture(int width, int height, Color startColor, Color endColor)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        
        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1);
            Color gradientColor = Color.Lerp(startColor, endColor, t);
            
            for (int y = 0; y < height; y++)
            {
                texture.SetPixel(x, y, gradientColor);
            }
        }
        
        texture.Apply();
        return texture;
    }
}

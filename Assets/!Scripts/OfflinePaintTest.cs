using UnityEngine;

/// <summary>
/// Example script showing how to use the NetworkCanvas in offline mode
/// This can be attached to any GameObject for testing offline painting functionality
/// </summary>
public class OfflinePaintTest : MonoBehaviour
{
    [SerializeField] private NetworkCanvas networkCanvas;
    [SerializeField] private Color paintColor = Color.red;
    [SerializeField] private int brushSize = 5;

    private void Update()
    {
        // Example: Paint at random UV coordinates when pressing Space
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TestOfflinePaint();
        }
    }

    public void TestOfflinePaint()
    {
        if (networkCanvas == null)
        {
            Debug.LogWarning("NetworkCanvas not assigned!");
            return;
        }

        // Generate random UV coordinates (0-1 range)
        Vector2 randomUV = new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f));
        
        // Paint using the unified Paint method - works in both offline and online modes
        networkCanvas.Paint(randomUV, paintColor, brushSize);
        
        Debug.Log($"Painted at UV: {randomUV} with color: {paintColor}");
    }

    /// <summary>
    /// Public method to paint at specific UV coordinates
    /// Can be called from other scripts or UI elements
    /// </summary>
    public void PaintAtCoordinates(float u, float v)
    {
        if (networkCanvas != null)
        {
            networkCanvas.Paint(new Vector2(u, v), paintColor, brushSize);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class CanvasRaycast : MonoBehaviour
{
    private int canvasLayerMask;
    private float debugLineDuration = 2.0f;

    [SerializeField]
    private Transform raycastOriginPoint;

    [SerializeField]
    private float debugLineWidth = 0.05f;

    [SerializeField]
    [Tooltip("The reference to the action for the VR controller's front trigger (Select).")]
    private InputActionReference selectActionReference;

    public Color markColor = Color.red; // Color to mark the hit area

    public float markSize = 20f; // Size of the mark in pixels

    private LineRenderer continuousLine; // Persistent line for visualization

    void Start()
    {
        canvasLayerMask = LayerMask.GetMask("Canvas");

        if (raycastOriginPoint == null)
        {
            Debug.LogWarning("RaycastOriginPoint is not assigned in the Inspector!");
        }

        if (selectActionReference != null && selectActionReference.action != null)
        {
            selectActionReference.action.Enable();
        }
        else
        {
            Debug.LogWarning("Select Action Reference is not assigned or invalid!");
        }

        // Setup material with a readable texture if needed
        SetupURPMaterial();

        // Initialize the continuous line
        SetupContinuousLine();
    }

    void OnDestroy()
    {
        if (selectActionReference != null && selectActionReference.action != null)
        {
            selectActionReference.action.Disable();
        }
    }

    void Update()
    {
        // Check if the object is "equipped" (assumed to be when the trigger is pressed)
        // If "equipped" means something else (e.g., the object is grabbed in VR), replace this condition
        bool isEquipped = selectActionReference != null && selectActionReference.action != null && selectActionReference.action.IsPressed();

        if (isEquipped)
        {
            Debug.Log("Object is equipped - casting ray continuously");
            CastRayFromObjectPosition();
            continuousLine.enabled = true; // Show the line while equipped
        }
        else
        {
            continuousLine.enabled = false; // Hide the line when not equipped
            if (selectActionReference == null)
            {
                Debug.Log("Select Action Reference is null or invalid!");
            }
        }
    }

    public void CastRayFromPoint(Vector3 raycastOrigin, Vector3 raycastDirection)
    {
        Ray ray = new Ray(raycastOrigin, raycastDirection);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, canvasLayerMask))
        {
            Vector3 end = raycastOrigin + raycastDirection * hit.distance;
            UpdateContinuousLine(raycastOrigin, end, Color.red); // Red line on hit
            Debug.Log($"Raycast successful! Hit object: {hit.collider.gameObject.name} at position: {hit.point}");

            // Mark the hit area on the texture
            MarkHitArea(hit);
        }
        else
        {
            Vector3 end = raycastOrigin + raycastDirection * 1000f;
            UpdateContinuousLine(raycastOrigin, end, Color.white); // White line on miss
            Debug.Log("Raycast failed - No Canvas layer objects hit");
        }
    }

    public void CastRayFromObjectPosition()
    {
        if (raycastOriginPoint == null)
        {
            Debug.LogError("Cannot cast ray: RaycastOriginPoint Transform is not assigned!");
            return;
        }

        Vector3 origin = raycastOriginPoint.position;
        Vector3 direction = -raycastOriginPoint.up; // Local negative Y direction
        CastRayFromPoint(origin, direction);
    }

    private void SetupURPMaterial()
    {
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("No Renderer found on this GameObject!");
            return;
        }

        if (renderer.material == null || renderer.material.mainTexture == null)
        {
            Material urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture2D texture = new Texture2D(1028, 1028, TextureFormat.RGBA32, false, true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[1028 * 1028];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            texture.SetPixels(pixels);
            texture.Apply();

            urpMaterial.SetTexture("_BaseMap", texture);
            renderer.material = urpMaterial;

            Debug.Log("URP material with 1028x1028 texture created and applied.");
        }
        else
        {
            Texture2D texture = renderer.material.mainTexture as Texture2D;
            if (texture == null || !texture.isReadable)
            {
                Debug.LogWarning("Existing texture is not readable! Enable Read/Write in Texture Import Settings.");
            }
            else if (texture.width != 1028 || texture.height != 1028)
            {
                Debug.LogWarning($"Texture size mismatch! Expected 1028x1028, got {texture.width}x{texture.height}.");
            }
        }
    }

    private void MarkHitArea(RaycastHit hit)
    {
        Renderer renderer = hit.collider.GetComponent<Renderer>();
        if (renderer == null || renderer.material == null || renderer.material.mainTexture == null)
        {
            Debug.LogWarning("No valid renderer or texture found!");
            return;
        }

        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogWarning("Texture is not a Texture2D!");
            return;
        }

        if (!texture.isReadable)
        {
            Debug.LogWarning("Texture is not readable! Enable Read/Write in Texture Import Settings.");
            return;
        }

        int x = (int)(hit.textureCoord.x * 1028);
        int y = (int)(hit.textureCoord.y * 1028);
        Debug.Log($"Hit at UV: {hit.textureCoord}, Pixel: ({x}, {y})");

        if (x < 0 || x >= 1028 || y < 0 || y >= 1028)
        {
            Debug.LogWarning("Hit outside texture bounds!");
            return;
        }

        for (int i = -(int)markSize; i <= (int)markSize; i++)
        {
            for (int j = -(int)markSize; j <= (int)markSize; j++)
            {
                if (i * i + j * j <= markSize * markSize)
                {
                    int px = Mathf.Clamp(x + i, 0, 1027);
                    int py = Mathf.Clamp(y + j, 0, 1027);
                    texture.SetPixel(px, py, markColor);
                }
            }
        }

        texture.Apply();
        Debug.Log("Texture updated with mark.");
    }

    private void SetupContinuousLine()
    {
        GameObject lineObj = new GameObject("ContinuousRayLine");
        lineObj.transform.SetParent(transform);
        continuousLine = lineObj.AddComponent<LineRenderer>();
        continuousLine.startWidth = debugLineWidth;
        continuousLine.endWidth = debugLineWidth;
        continuousLine.positionCount = 2;
        continuousLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        continuousLine.startColor = Color.white;
        continuousLine.endColor = Color.white;
        continuousLine.material.color = Color.white;
        continuousLine.enabled = false; // Hidden by default
        Debug.Log("Continuous line setup complete.");
    }

    private void UpdateContinuousLine(Vector3 start, Vector3 end, Color color)
    {
        continuousLine.SetPosition(0, start);
        continuousLine.SetPosition(1, end);
        continuousLine.startColor = color;
        continuousLine.endColor = color;
        continuousLine.material.color = color;
        Debug.Log($"Line updated: Start={start}, End={end}, Color={color}");
    }
}
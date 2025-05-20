using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class CanvasRaycast : MonoBehaviour
{
    private int canvasLayerMask;
    private float debugLineDuration = 2.0f;

    [SerializeField] private Transform raycastOriginPoint;
    [SerializeField] private float debugLineWidth = 0.05f;
    [SerializeField] private InputActionReference selectActionReference;
    public Color markColor = Color.red;
    public float markSize = 20f;

    private LineRenderer continuousLine;

    void Start()
    {
        canvasLayerMask = LayerMask.GetMask("Canvas");

        if (raycastOriginPoint == null) Debug.LogWarning("RaycastOriginPoint not assigned!");
        if (selectActionReference?.action == null) Debug.LogWarning("Select Action Reference invalid!");
        else selectActionReference.action.Enable();

        SetupContinuousLine();
    }

    void OnDestroy()
    {
        if (selectActionReference?.action != null)
            selectActionReference.action.Disable();
    }

    void Update()
    {
        bool isEquipped = selectActionReference?.action?.IsPressed() ?? false;
        continuousLine.enabled = isEquipped;

        if (isEquipped)
        {
            Debug.Log("Casting ray continuously");
            CastRayFromObjectPosition();
        }
    }

    public void CastRayFromPoint(Vector3 raycastOrigin, Vector3 raycastDirection)
    {
        Ray ray = new Ray(raycastOrigin, raycastDirection);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, canvasLayerMask))
        {
            Vector3 end = raycastOrigin + raycastDirection * hit.distance;
            UpdateContinuousLine(raycastOrigin, end, Color.red);
            Debug.Log($"Hit: {hit.collider.gameObject.name} at {hit.point}");

            NetworkCanvas networkCanvas = hit.collider.GetComponent<NetworkCanvas>();
            if (networkCanvas != null)
            {
                // Apply paint locally first for immediate feedback
                networkCanvas.ApplyPaintLocally(hit.textureCoord, markColor, (int)markSize);
                // Send the paint action to the server with the local client ID
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                networkCanvas.PaintServerRpc(hit.textureCoord, markColor, (int)markSize, clientId);
            }
            else
            {
                Debug.LogWarning("No NetworkCanvas on hit object!");
            }
        }
        else
        {
            Vector3 end = raycastOrigin + raycastDirection * 1000f;
            UpdateContinuousLine(raycastOrigin, end, Color.white);
            Debug.Log("Raycast missed Canvas layer");
        }
    }

    public void CastRayFromObjectPosition()
    {
        if (raycastOriginPoint == null)
        {
            Debug.LogError("RaycastOriginPoint not assigned!");
            return;
        }
        Vector3 origin = raycastOriginPoint.position;
        Vector3 direction = -raycastOriginPoint.up;
        CastRayFromPoint(origin, direction);
    }

    private void SetupContinuousLine()
    {
        GameObject lineObj = new GameObject("ContinuousRayLine") { transform = { parent = transform } };
        continuousLine = lineObj.AddComponent<LineRenderer>();
        continuousLine.startWidth = continuousLine.endWidth = debugLineWidth;
        continuousLine.positionCount = 2;
        continuousLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        continuousLine.startColor = continuousLine.endColor = Color.white;
        continuousLine.material.color = Color.white;
        continuousLine.enabled = false;
        Debug.Log("Continuous line setup complete.");
    }

    private void UpdateContinuousLine(Vector3 start, Vector3 end, Color color)
    {
        continuousLine.SetPosition(0, start);
        continuousLine.SetPosition(1, end);
        continuousLine.startColor = continuousLine.endColor = color;
        continuousLine.material.color = color;
    }
}
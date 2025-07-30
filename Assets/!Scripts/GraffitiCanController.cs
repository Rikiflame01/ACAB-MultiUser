using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using XRMultiplayer;

/// <summary>
/// Controls the graffiti can equipment state and connects grab events to the painting system
/// </summary>
[RequireComponent(typeof(NetworkBaseInteractable))]
[RequireComponent(typeof(CanvasRaycast))]
public class GraffitiCanController : MonoBehaviour
{
    [Header("Graffiti Can Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private NetworkBaseInteractable networkInteractable;
    private CanvasRaycast canvasRaycast;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable baseInteractable;
    
    void Awake()
    {
        // Get required components
        networkInteractable = GetComponent<NetworkBaseInteractable>();
        canvasRaycast = GetComponent<CanvasRaycast>();
        baseInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        
        if (networkInteractable == null)
        {
            Debug.LogError("GraffitiCanController requires NetworkBaseInteractable component!", this);
        }
        
        if (canvasRaycast == null)
        {
            Debug.LogError("GraffitiCanController requires CanvasRaycast component!", this);
        }
    }
    
    void OnEnable()
    {
        // Subscribe to grab events
        if (baseInteractable != null)
        {
            baseInteractable.selectEntered.AddListener(OnGrabbed);
            baseInteractable.selectExited.AddListener(OnReleased);
        }
    }
    
    void OnDisable()
    {
        // Unsubscribe from grab events
        if (baseInteractable != null)
        {
            baseInteractable.selectEntered.RemoveListener(OnGrabbed);
            baseInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
    
    /// <summary>
    /// Called when the graffiti can is grabbed
    /// </summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Graffiti can grabbed by: {args.interactorObject.transform.name}");
        }
        
        // Enable painting functionality
        if (canvasRaycast != null)
        {
            canvasRaycast.OnGraffitiCanEquipped();
        }
    }
    
    /// <summary>
    /// Called when the graffiti can is released
    /// </summary>
    private void OnReleased(SelectExitEventArgs args)
    {
        // Only trigger release if no other hands are holding it
        if (baseInteractable.isSelected)
        {
            if (enableDebugLogs)
            {
                Debug.Log("Graffiti can still held by another hand, not disabling painting");
            }
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"Graffiti can released by: {args.interactorObject.transform.name}");
        }
        
        // Disable painting functionality
        if (canvasRaycast != null)
        {
            canvasRaycast.OnGraffitiCanUnequipped();
        }
    }
    
    /// <summary>
    /// Get current equipment state
    /// </summary>
    public bool IsEquipped()
    {
        return canvasRaycast != null && canvasRaycast.IsGraffitiCanEquipped();
    }
    
    /// <summary>
    /// Manual testing methods
    /// </summary>
    [ContextMenu("Test Equip")]
    public void TestEquip()
    {
        if (canvasRaycast != null)
        {
            canvasRaycast.OnGraffitiCanEquipped();
        }
    }
    
    [ContextMenu("Test Unequip")]
    public void TestUnequip()
    {
        if (canvasRaycast != null)
        {
            canvasRaycast.OnGraffitiCanUnequipped();
        }
    }
}

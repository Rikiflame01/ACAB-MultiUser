using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Example script showing how to integrate the CanvasRaycast hand-specific functionality
/// with a graffiti can that can be picked up and equipped.
/// 
/// This script should be attached to your graffiti can GameObject that has an XRGrabInteractable component.
/// </summary>
public class GraffitiCanEquipment : MonoBehaviour
{
    [Header("Canvas Raycast Integration")]
    [SerializeField] private bool findCanvasRaycastsAutomatically = true;
    [SerializeField] private CanvasRaycast[] manualCanvasRaycasts; // Manually assign if needed

    private CanvasRaycast[] canvasRaycasts;
    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        // Get the grab interactable component
        grabInteractable = GetComponent<XRGrabInteractable>();
        
        if (grabInteractable == null)
        {
            Debug.LogError("GraffitiCanEquipment requires an XRGrabInteractable component on the same GameObject!");
            return;
        }

        // Find all CanvasRaycast scripts in the scene
        if (findCanvasRaycastsAutomatically)
        {
            canvasRaycasts = FindObjectsOfType<CanvasRaycast>();
        }
        else
        {
            canvasRaycasts = manualCanvasRaycasts;
        }

        // Subscribe to grab events
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    /// <summary>
    /// Called when the graffiti can is grabbed by a hand
    /// </summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Notify all canvas raycasts that graffiti can is equipped
        foreach (var canvasRaycast in canvasRaycasts)
        {
            if (canvasRaycast != null)
            {
                canvasRaycast.OnGraffitiCanEquipped();
            }
        }

        // Optional: Add visual feedback, sound effects, etc.
        OnEquipmentStateChanged(true);
    }

    /// <summary>
    /// Called when the graffiti can is released
    /// </summary>
    private void OnReleased(SelectExitEventArgs args)
    {
        // Notify all canvas raycasts that graffiti can is unequipped
        foreach (var canvasRaycast in canvasRaycasts)
        {
            if (canvasRaycast != null)
            {
                canvasRaycast.OnGraffitiCanUnequipped();
            }
        }

        // Optional: Add visual feedback, sound effects, etc.
        OnEquipmentStateChanged(false);
    }

    /// <summary>
    /// Optional method for additional feedback when equipment state changes
    /// </summary>
    private void OnEquipmentStateChanged(bool isEquipped)
    {
        // Example: Change material, play sound, show/hide UI elements, etc.
        // Equipment state change handling code can be added here
    }

    /// <summary>
    /// Public method to manually trigger equipment state (useful for testing)
    /// </summary>
    [ContextMenu("Test Equip")]
    public void TestEquip()
    {
        foreach (var canvasRaycast in canvasRaycasts)
        {
            if (canvasRaycast != null)
            {
                canvasRaycast.OnGraffitiCanEquipped();
            }
        }
        OnEquipmentStateChanged(true);
    }

    /// <summary>
    /// Public method to manually trigger unequip state (useful for testing)
    /// </summary>
    [ContextMenu("Test Unequip")]
    public void TestUnequip()
    {
        foreach (var canvasRaycast in canvasRaycasts)
        {
            if (canvasRaycast != null)
            {
                canvasRaycast.OnGraffitiCanUnequipped();
            }
        }
        OnEquipmentStateChanged(false);
    }

    /// <summary>
    /// Debug method to show current state
    /// </summary>
    [ContextMenu("Debug Equipment State")]
    public void DebugEquipmentState()
    {
        bool isCurrentlyGrabbed = grabInteractable.isSelected;
        Debug.Log($"=== GRAFFITI CAN STATE ===");
        Debug.Log($"Currently Grabbed: {isCurrentlyGrabbed}");
        Debug.Log($"Canvas Raycasts Found: {canvasRaycasts?.Length ?? 0}");
        
        if (isCurrentlyGrabbed && grabInteractable.interactorsSelecting.Count > 0)
        {
            var interactor = grabInteractable.interactorsSelecting[0];
            Debug.Log($"Grabbed by: {interactor.transform.name}");
        }

        // Show state of each canvas raycast
        for (int i = 0; i < canvasRaycasts?.Length; i++)
        {
            var raycast = canvasRaycasts[i];
            if (raycast != null)
            {
                Debug.Log($"CanvasRaycast {i}: Hand={raycast.GetHandType()}, IsPaintingHand={raycast.IsPaintingHand()}, IsEquipped={raycast.IsGraffitiCanEquipped()}");
            }
        }
    }
}

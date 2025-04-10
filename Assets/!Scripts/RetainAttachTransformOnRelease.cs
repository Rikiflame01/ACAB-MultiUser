using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class RetainAttachTransformOnRelease : MonoBehaviour
{
    [SerializeField]
    [Tooltip("When enabled, the object will remain a child of the attach transform after being released, until it is grabbed again.")]
    private bool m_RetainAttachTransformOnRelease = true;

    /// <summary>
    /// When enabled, the object will remain a child of the attach transform after being released, until it is grabbed again.
    /// </summary>
    public bool retainAttachTransformOnRelease
    {
        get => m_RetainAttachTransformOnRelease;
        set => m_RetainAttachTransformOnRelease = value;
    }

    private XRGrabInteractable m_GrabInteractable;
    private Transform m_OriginalParent; // Store the original parent to restore later
    private Transform m_AttachTransform; // The attach transform the object is currently parented to
    private bool m_IsRetained; // Track if the object is currently retained by an attach transform

    private void Awake()
    {
        // Get the required XRGrabInteractable component
        m_GrabInteractable = GetComponent<XRGrabInteractable>();

        if (m_GrabInteractable == null)
        {
            Debug.LogError("RetainAttachTransformOnRelease requires an XRGrabInteractable component.", this);
            enabled = false;
            return;
        }

        // Initialize the retained state
        m_IsRetained = false;
        m_OriginalParent = transform.parent; // Store the original parent
    }

    private void OnEnable()
    {
        // Subscribe to the grab and release events
        m_GrabInteractable.selectEntered.AddListener(OnGrab);
        m_GrabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        // Unsubscribe from the events
        m_GrabInteractable.selectEntered.RemoveListener(OnGrab);
        m_GrabInteractable.selectExited.RemoveListener(OnRelease);

        // Ensure the object is unparented when this script is disabled
        if (m_IsRetained)
        {
            DetachFromAttachTransform();
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // If the object is currently retained by an attach transform, detach it
        if (m_RetainAttachTransformOnRelease && m_IsRetained)
        {
            DetachFromAttachTransform();
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // If enabled and no interactors are selecting the object, retain the attach transform
        if (m_RetainAttachTransformOnRelease && !m_IsRetained && m_GrabInteractable.interactorsSelecting.Count == 0)
        {
            RetainAttachTransform(args.interactorObject);
        }
    }

    private void RetainAttachTransform(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
    {
        // Get the attach transform of the interactor that released the object
        m_AttachTransform = interactor.GetAttachTransform(m_GrabInteractable);

        if (m_AttachTransform == null)
        {
            Debug.LogWarning("Attach transform is null. Cannot retain the object.", this);
            return;
        }

        // Store the original parent (if not already stored) and parent the object to the attach transform
        if (!m_IsRetained)
        {
            m_OriginalParent = transform.parent;
        }

        transform.SetParent(m_AttachTransform, true); // Keep world position
        m_IsRetained = true;

        // Ensure the Rigidbody is kinematic to prevent physics interference while parented
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    private void DetachFromAttachTransform()
    {
        // Restore the original parent
        transform.SetParent(m_OriginalParent, true); // Keep world position
        m_AttachTransform = null;
        m_IsRetained = false;

        // Restore the Rigidbody's kinematic state (if it was originally non-kinematic)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !m_GrabInteractable.isSelected)
        {
            // Let XRGrabInteractable handle the Rigidbody settings during a grab
            rb.isKinematic = false; // Assuming it was non-kinematic originally; adjust if needed
        }
    }
}
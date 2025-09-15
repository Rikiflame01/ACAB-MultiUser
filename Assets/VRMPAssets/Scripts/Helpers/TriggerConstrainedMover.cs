using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

namespace XRMultiplayer
{
    /// <summary>
    /// Allows moving an object with trigger input while constraining Y position and rotation.
    /// Uses ray casting from controllers to detect and move objects.
    /// Toggle behavior: Press trigger to start following controller, press any trigger again to stop.
    /// 
    /// Features:
    /// - Haptic feedback on controller when starting/stopping movement
    /// - Visual feedback (glow/color change) on the controlled object
    /// - Optional visual feedback on the active controller
    /// - Supports both left and right controllers
    /// - Automatic controller discovery
    /// </summary>
    public class TriggerConstrainedMover : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Lock the Y position to prevent vertical movement")]
        bool m_LockYPosition = true;

        [SerializeField]
        [Tooltip("Lock all rotation axes")]
        bool m_LockRotation = true;

        [SerializeField]
        [Tooltip("Maximum distance from initial position for X and Z movement")]
        float m_MaxMovementRange = 5f;

        [SerializeField]
        [Tooltip("How smoothly the object moves to the target position")]
        [Range(0.1f, 20f)]
        float m_MovementSpeed = 10f;

        [Header("Input Settings")]
        [SerializeField]
        [Tooltip("Input action for left trigger")]
        InputActionReference m_LeftTriggerAction;

        [SerializeField]
        [Tooltip("Input action for right trigger")]
        InputActionReference m_RightTriggerAction;

        [SerializeField]
        [Tooltip("Minimum trigger value to start moving")]
        [Range(0f, 1f)]
        float m_TriggerThreshold = 0.1f;

        [Header("Selection Settings")]
        [SerializeField]
        [Tooltip("Only move when pointing directly at this object")]
        bool m_RequireDirectAiming = true;

        [SerializeField]
        [Tooltip("Maximum distance to detect this object for aiming")]
        float m_SelectionDistance = 10f;

        [Header("Ray Casting")]
        [SerializeField]
        [Tooltip("Layer mask for what the rays can hit")]
        LayerMask m_RaycastLayerMask = -1;

        [SerializeField]
        [Tooltip("Maximum distance for ray casting")]
        float m_MaxRayDistance = 10f;

    [Header("Controller References")]
    [SerializeField]
    [Tooltip("Left controller transform (will try to find automatically if not set)")]
    Transform m_LeftController;

    [SerializeField]
    [Tooltip("Right controller transform (will try to find automatically if not set)")]
    Transform m_RightController;

    [Header("Feedback Settings")]
    [SerializeField]
    [Tooltip("Enable haptic feedback on trigger press")]
    bool m_EnableHapticFeedback = true;

    [SerializeField]
    [Tooltip("Intensity of haptic feedback (0-1)")]
    [Range(0f, 1f)]
    float m_HapticIntensity = 0.3f;

    [SerializeField]
    [Tooltip("Duration of haptic feedback in seconds")]
    [Range(0.01f, 1f)]
    float m_HapticDuration = 0.1f;

    [SerializeField]
    [Tooltip("Enable visual feedback on the object being controlled")]
    bool m_EnableVisualFeedback = true;

    [SerializeField]
    [Tooltip("Material to use for visual feedback (optional - will modify existing material if not set)")]
    Material m_FeedbackMaterial;

    [SerializeField]
    [Tooltip("Color to use for visual feedback")]
    Color m_FeedbackColor = new Color(0.3f, 0.8f, 1f, 1f);

    [SerializeField]
    [Tooltip("Emission intensity for glow effect")]
    [Range(0f, 5f)]
    float m_EmissionIntensity = 1.5f;

    [SerializeField]
    [Tooltip("Enable visual feedback on the active controller")]
    bool m_EnableControllerVisualFeedback = false;

    [SerializeField]
    [Tooltip("Color for controller visual feedback")]
    Color m_ControllerFeedbackColor = new Color(1f, 0.5f, 0.2f, 1f);

    [SerializeField]
    [Tooltip("Enable hover visual feedback when pointing at object")]
    bool m_EnableHoverFeedback = false;

    [SerializeField]
    [Tooltip("Color for hover visual feedback")]
    Color m_HoverFeedbackColor = new Color(0.3f, 0.8f, 1f, 1f);

    [SerializeField]
    [Tooltip("Hover effect emission intensity")]
    [Range(0f, 2f)]
    float m_HoverEmissionIntensity = 0.8f;

        // Private variables
        Vector3 m_InitialPosition;
        Quaternion m_InitialRotation;
        bool m_IsMoving;
        bool m_IsSelected;
        Transform m_ActiveController;
        Vector3 m_LastHitPoint;
        Collider m_ObjectCollider;
        
        // Trigger state tracking for toggle behavior
        bool m_LeftTriggerPreviousFrame;
        bool m_RightTriggerPreviousFrame;

        // Feedback system variables
        Renderer m_ObjectRenderer;
        Material m_OriginalMaterial;
        Material m_RuntimeFeedbackMaterial;
        Color m_OriginalEmissionColor;
        bool m_OriginalEmissionEnabled;

        // Controller feedback variables
        Renderer m_ActiveControllerRenderer;
        Material m_OriginalControllerMaterial;
        Material m_RuntimeControllerMaterial;

        // Hover feedback variables
        bool m_IsHovered;
        Transform m_HoveringController;
        Material m_RuntimeHoverMaterial;
        
        void Start()
        {
            // Store initial transform values
            m_InitialPosition = transform.position;
            m_InitialRotation = transform.rotation;

            // Get the collider for selection detection
            m_ObjectCollider = GetComponent<Collider>();
            if (m_ObjectCollider == null)
            {
                Debug.LogWarning($"TriggerConstrainedMover on {gameObject.name}: No collider found! A collider is required for object detection.", this);
            }

            // Try to find controllers automatically if not assigned
            FindControllers();

            // Enable input actions
            EnableInputActions();

            // Setup visual feedback system
            SetupVisualFeedback();
        }

        void OnDestroy()
        {
            // Disable input actions
            DisableInputActions();

            // Clean up runtime materials
            if (m_RuntimeFeedbackMaterial != null)
            {
                DestroyImmediate(m_RuntimeFeedbackMaterial);
            }
            
            if (m_RuntimeControllerMaterial != null)
            {
                DestroyImmediate(m_RuntimeControllerMaterial);
            }

            if (m_RuntimeHoverMaterial != null)
            {
                DestroyImmediate(m_RuntimeHoverMaterial);
            }
        }

        void Update()
        {
            HandleTriggerInput();
            
            if (m_EnableHoverFeedback)
            {
                HandleHoverFeedback();
            }
        }

        void SetupVisualFeedback()
        {
            if (!m_EnableVisualFeedback)
                return;

            // Get the renderer component
            m_ObjectRenderer = GetComponent<Renderer>();
            if (m_ObjectRenderer == null)
            {
                Debug.LogWarning($"TriggerConstrainedMover on {gameObject.name}: No Renderer found! Visual feedback requires a Renderer component.", this);
                return;
            }

            // Store original material properties
            m_OriginalMaterial = m_ObjectRenderer.material;
            
            // Check if the material supports emission
            if (m_OriginalMaterial.HasProperty("_EmissionColor"))
            {
                m_OriginalEmissionColor = m_OriginalMaterial.GetColor("_EmissionColor");
                m_OriginalEmissionEnabled = m_OriginalMaterial.IsKeywordEnabled("_EMISSION");
            }

            // Create a runtime copy of the material to avoid modifying the asset
            m_RuntimeFeedbackMaterial = new Material(m_FeedbackMaterial != null ? m_FeedbackMaterial : m_OriginalMaterial);
        }

        void TriggerHapticFeedback(Transform controller)
        {
            if (!m_EnableHapticFeedback || controller == null)
                return;

            // Try to find SimpleHapticFeedback component on the controller
            // Use reflection to find the component since the exact namespace might vary
            var hapticComponents = controller.GetComponentsInChildren<MonoBehaviour>();
            foreach (var component in hapticComponents)
            {
                if (component.GetType().Name == "SimpleHapticFeedback")
                {
                    // Use reflection to call SendHapticImpulse
                    var method = component.GetType().GetMethod("SendHapticImpulse", new[] { typeof(float), typeof(float) });
                    if (method != null)
                    {
                        method.Invoke(component, new object[] { m_HapticIntensity, m_HapticDuration });
                        break;
                    }
                }
            }
        }

        void EnableVisualFeedback()
        {
            if (!m_EnableVisualFeedback || m_ObjectRenderer == null || m_RuntimeFeedbackMaterial == null)
                return;

            // Apply the feedback material
            m_ObjectRenderer.material = m_RuntimeFeedbackMaterial;

            // Set up glow effect if the material supports emission
            if (m_RuntimeFeedbackMaterial.HasProperty("_EmissionColor"))
            {
                m_RuntimeFeedbackMaterial.EnableKeyword("_EMISSION");
                Color emissionColor = m_FeedbackColor * m_EmissionIntensity;
                m_RuntimeFeedbackMaterial.SetColor("_EmissionColor", emissionColor);
            }

            // Set the base color if supported
            if (m_RuntimeFeedbackMaterial.HasProperty("_BaseColor"))
            {
                m_RuntimeFeedbackMaterial.SetColor("_BaseColor", m_FeedbackColor);
            }
            else if (m_RuntimeFeedbackMaterial.HasProperty("_Color"))
            {
                m_RuntimeFeedbackMaterial.SetColor("_Color", m_FeedbackColor);
            }
        }

        void DisableVisualFeedback()
        {
            if (!m_EnableVisualFeedback || m_ObjectRenderer == null || m_OriginalMaterial == null)
                return;

            // Restore original material
            m_ObjectRenderer.material = m_OriginalMaterial;
        }

        void EnableControllerFeedback(Transform controller)
        {
            if (!m_EnableControllerVisualFeedback || controller == null)
                return;

            // Find a renderer on the controller
            m_ActiveControllerRenderer = controller.GetComponentInChildren<Renderer>();
            if (m_ActiveControllerRenderer == null)
                return;

            // Store original material
            m_OriginalControllerMaterial = m_ActiveControllerRenderer.material;

            // Create runtime material for controller
            m_RuntimeControllerMaterial = new Material(m_OriginalControllerMaterial);
            
            // Apply controller feedback color
            if (m_RuntimeControllerMaterial.HasProperty("_EmissionColor"))
            {
                m_RuntimeControllerMaterial.EnableKeyword("_EMISSION");
                Color emissionColor = m_ControllerFeedbackColor * 0.8f;
                m_RuntimeControllerMaterial.SetColor("_EmissionColor", emissionColor);
            }

            if (m_RuntimeControllerMaterial.HasProperty("_BaseColor"))
            {
                Color blendedColor = Color.Lerp(m_OriginalControllerMaterial.GetColor("_BaseColor"), m_ControllerFeedbackColor, 0.3f);
                m_RuntimeControllerMaterial.SetColor("_BaseColor", blendedColor);
            }
            else if (m_RuntimeControllerMaterial.HasProperty("_Color"))
            {
                Color blendedColor = Color.Lerp(m_OriginalControllerMaterial.GetColor("_Color"), m_ControllerFeedbackColor, 0.3f);
                m_RuntimeControllerMaterial.SetColor("_Color", blendedColor);
            }

            // Apply the feedback material
            m_ActiveControllerRenderer.material = m_RuntimeControllerMaterial;
        }

        void DisableControllerFeedback()
        {
            if (!m_EnableControllerVisualFeedback || m_ActiveControllerRenderer == null || m_OriginalControllerMaterial == null)
                return;

            // Restore original controller material
            m_ActiveControllerRenderer.material = m_OriginalControllerMaterial;
            
            // Clean up
            if (m_RuntimeControllerMaterial != null)
            {
                DestroyImmediate(m_RuntimeControllerMaterial);
                m_RuntimeControllerMaterial = null;
            }
            
            m_ActiveControllerRenderer = null;
            m_OriginalControllerMaterial = null;
        }

        void FindControllers()
        {
            if (m_LeftController == null || m_RightController == null)
            {
                // Try to find XR Origin and controllers
                var xrOrigin = FindFirstObjectByType<XROrigin>();
                if (xrOrigin != null)
                {
                    if (m_LeftController == null)
                    {
                        var leftController = xrOrigin.transform.Find("Camera Offset/Left Controller");
                        if (leftController == null)
                            leftController = xrOrigin.transform.Find("Camera Offset/LeftHand Controller");
                        m_LeftController = leftController;
                    }

                    if (m_RightController == null)
                    {
                        var rightController = xrOrigin.transform.Find("Camera Offset/Right Controller");
                        if (rightController == null)
                            rightController = xrOrigin.transform.Find("Camera Offset/RightHand Controller");
                        m_RightController = rightController;
                    }
                }
            }
        }

        void EnableInputActions()
        {
            if (m_LeftTriggerAction?.action != null)
                m_LeftTriggerAction.action.Enable();
            
            if (m_RightTriggerAction?.action != null)
                m_RightTriggerAction.action.Enable();
        }

        void DisableInputActions()
        {
            if (m_LeftTriggerAction?.action != null)
                m_LeftTriggerAction.action.Disable();
            
            if (m_RightTriggerAction?.action != null)
                m_RightTriggerAction.action.Disable();
        }

        void HandleTriggerInput()
        {
            bool leftTriggerPressed = false;
            bool rightTriggerPressed = false;

            // Check current trigger values
            if (m_LeftTriggerAction?.action != null)
                leftTriggerPressed = m_LeftTriggerAction.action.ReadValue<float>() > m_TriggerThreshold;
            
            if (m_RightTriggerAction?.action != null)
                rightTriggerPressed = m_RightTriggerAction.action.ReadValue<float>() > m_TriggerThreshold;

            // Detect trigger press events (transition from not pressed to pressed)
            bool leftTriggerJustPressed = leftTriggerPressed && !m_LeftTriggerPreviousFrame;
            bool rightTriggerJustPressed = rightTriggerPressed && !m_RightTriggerPreviousFrame;
            bool anyTriggerJustPressed = leftTriggerJustPressed || rightTriggerJustPressed;

            // Update previous frame states for next frame
            m_LeftTriggerPreviousFrame = leftTriggerPressed;
            m_RightTriggerPreviousFrame = rightTriggerPressed;

            if (anyTriggerJustPressed)
            {
                if (!m_IsMoving)
                {
                    // Start moving - determine which controller triggered
                    Transform triggeringController = null;
                    if (leftTriggerJustPressed && m_LeftController != null)
                        triggeringController = m_LeftController;
                    else if (rightTriggerJustPressed && m_RightController != null)
                        triggeringController = m_RightController;

                    if (triggeringController != null)
                    {
                        // Check if we're pointing at this object (if required)
                        bool isPointingAtObject = true;
                        if (m_RequireDirectAiming)
                        {
                            isPointingAtObject = IsPointingAtThisObject(triggeringController);
                        }

                        if (isPointingAtObject)
                        {
                            StartMoving(triggeringController);
                        }
                    }
                }
                else
                {
                    // Already moving, any trigger press should stop movement
                    StopMoving();
                }
            }

            // Update movement if currently moving
            if (m_IsMoving)
            {
                UpdateMovement();
            }
        }

        void HandleHoverFeedback()
        {
            bool wasHovered = m_IsHovered;
            m_IsHovered = false;
            m_HoveringController = null;

            // Check if either controller is pointing at this object
            if (m_LeftController != null && IsPointingAtThisObject(m_LeftController))
            {
                m_IsHovered = true;
                m_HoveringController = m_LeftController;
            }
            else if (m_RightController != null && IsPointingAtThisObject(m_RightController))
            {
                m_IsHovered = true;
                m_HoveringController = m_RightController;
            }

            // Handle hover state changes
            if (m_IsHovered && !wasHovered)
            {
                StartHover();
            }
            else if (!m_IsHovered && wasHovered)
            {
                StopHover();
            }
        }

        void StartHover()
        {
            EnableHoverVisualFeedback();
        }

        void StopHover()
        {
            DisableHoverVisualFeedback();
        }

        void EnableHoverVisualFeedback()
        {
            if (!m_EnableHoverFeedback || m_ObjectRenderer == null)
                return;

            // Don't override active movement feedback
            if (m_IsMoving)
                return;

            SetupHoverVisualFeedback();

            if (m_RuntimeHoverMaterial != null)
            {
                m_ObjectRenderer.material = m_RuntimeHoverMaterial;

                // Set emission properties for hover glow
                m_RuntimeHoverMaterial.EnableKeyword("_EMISSION");
                m_RuntimeHoverMaterial.SetColor("_EmissionColor", m_HoverFeedbackColor * m_HoverEmissionIntensity);
            }
        }

        void DisableHoverVisualFeedback()
        {
            if (!m_EnableHoverFeedback || m_ObjectRenderer == null)
                return;

            // Don't disable if we're currently moving (active feedback takes priority)
            if (m_IsMoving)
                return;

            // Restore original material
            if (m_OriginalMaterial != null)
            {
                m_ObjectRenderer.material = m_OriginalMaterial;
            }
        }

        void SetupHoverVisualFeedback()
        {
            if (m_RuntimeHoverMaterial != null)
                return;

            // Create runtime material for hover feedback
            if (m_OriginalMaterial != null)
            {
                m_RuntimeHoverMaterial = new Material(m_OriginalMaterial);
            }
        }

        void StartMoving(Transform controller)
        {
            m_IsMoving = true;
            m_ActiveController = controller;

            // Perform initial raycast to get starting point
            if (Physics.Raycast(controller.position, controller.forward, out RaycastHit hit, m_MaxRayDistance, m_RaycastLayerMask))
            {
                m_LastHitPoint = hit.point;
            }
            else
            {
                // Use a point in front of the controller if no hit
                m_LastHitPoint = controller.position + controller.forward * 2f;
            }

            // Trigger feedback systems
            TriggerHapticFeedback(controller);
            EnableVisualFeedback();
            EnableControllerFeedback(controller);
        }

        void UpdateMovement()
        {
            if (!m_IsMoving || m_ActiveController == null)
                return;

            Vector3 targetPosition;

            // Perform raycast to get new target position
            if (Physics.Raycast(m_ActiveController.position, m_ActiveController.forward, out RaycastHit hit, m_MaxRayDistance, m_RaycastLayerMask))
            {
                targetPosition = hit.point;
                m_LastHitPoint = hit.point;
            }
            else
            {
                // Use a point in front of the controller if no hit
                targetPosition = m_ActiveController.position + m_ActiveController.forward * 2f;
            }

            // Apply constraints
            ApplyMovementConstraints(ref targetPosition);

            // Move object smoothly to target position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * m_MovementSpeed);

            // Apply rotation lock
            if (m_LockRotation)
            {
                transform.rotation = m_InitialRotation;
            }
        }

        void StopMoving()
        {
            // Trigger haptic feedback when releasing
            if (m_ActiveController != null)
            {
                TriggerHapticFeedback(m_ActiveController);
            }

            m_IsMoving = false;
            m_IsSelected = false;
            m_ActiveController = null;

            // Disable visual feedback
            DisableVisualFeedback();
            DisableControllerFeedback();

            // Re-enable hover feedback if still hovering
            if (m_IsHovered)
            {
                EnableHoverVisualFeedback();
            }
        }

        bool IsPointingAtThisObject(Transform controller)
        {
            if (m_ObjectCollider == null || controller == null)
                return false;

            // Cast a ray from the controller
            Ray ray = new Ray(controller.position, controller.forward);
            
            // Check if the ray hits this object's collider within selection distance
            if (m_ObjectCollider.Raycast(ray, out RaycastHit hit, m_SelectionDistance))
            {
                return true;
            }

            return false;
        }

        void ApplyMovementConstraints(ref Vector3 targetPosition)
        {
            // Lock Y position
            if (m_LockYPosition)
            {
                targetPosition.y = m_InitialPosition.y;
            }

            // Constrain X and Z movement to maximum range
            Vector3 flatMovement = new Vector3(targetPosition.x - m_InitialPosition.x, 0, targetPosition.z - m_InitialPosition.z);
            if (flatMovement.magnitude > m_MaxMovementRange)
            {
                flatMovement = flatMovement.normalized * m_MaxMovementRange;
                targetPosition.x = m_InitialPosition.x + flatMovement.x;
                targetPosition.z = m_InitialPosition.z + flatMovement.z;
            }
        }

        /// <summary>
        /// Reset the object to its initial position and rotation
        /// </summary>
        public void ResetToInitialPosition()
        {
            StopMoving();
            transform.position = m_InitialPosition;
            transform.rotation = m_InitialRotation;
        }

        /// <summary>
        /// Set new initial position (useful for dynamic objects)
        /// </summary>
        public void SetNewInitialPosition()
        {
            m_InitialPosition = transform.position;
            m_InitialRotation = transform.rotation;
        }

        void OnDrawGizmosSelected()
        {
            // Draw movement range
            if (m_MaxMovementRange > 0f)
            {
                Gizmos.color = Color.yellow;
                Vector3 center = Application.isPlaying ? m_InitialPosition : transform.position;
                Gizmos.DrawWireSphere(new Vector3(center.x, center.y, center.z), m_MaxMovementRange);
            }

            // Draw ray from active controller
            if (m_IsMoving && m_ActiveController != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(m_ActiveController.position, m_ActiveController.forward * m_MaxRayDistance);
                
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(m_LastHitPoint, 0.05f);
            }

            // Draw initial position
            if (Application.isPlaying)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(m_InitialPosition, Vector3.one * 0.1f);
            }
        }
    }
}

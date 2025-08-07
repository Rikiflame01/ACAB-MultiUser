using UnityEngine;

namespace XRMultiplayer
{
    public class PopoutUI : MonoBehaviour
    {
        [SerializeField] bool m_HideOnStart = false;
        [SerializeField] float m_DistanceFromFace = .25f;
        [SerializeField] float m_YOffset;
        [SerializeField] float m_XOffset; // Horizontal offset: negative = left, positive = right
        [SerializeField] bool m_UseRelativePositioning = true; // If true, uses camera's right vector; if false, uses world X-axis
        [SerializeField] bool m_AutoSetXOffsetFromInput = true; // If true, automatically sets X offset based on input hand
        [SerializeField] float m_LeftHandXOffset = -1f; // X offset when left hand/controller triggers menu
        [SerializeField] float m_RightHandXOffset = 1f; // X offset when right hand/controller triggers menu
        [SerializeField] bool m_ManualTestMode = false; // For debugging: if true, uses manual test hand setting
        [SerializeField] int m_ManualTestHand = 1; // -1 = left, 1 = right, 0 = default (only used when ManualTestMode is true)
        [SerializeField] bool m_UseHeadDirectionDetection = true; // If true, uses head direction relative to body to position menu
        [SerializeField] Transform m_PlayerBodyTransform; // The player's body/prefab transform for direction reference
        [SerializeField] float m_DirectionThreshold = 0.3f; // How far left/right user must be looking to trigger side positioning
        Transform m_MainCamTransform;
        
        // Cache to prevent repeated lookups and potential infinite loops
        private bool m_HasSearchedForPlayerBody = false;

        // Static property to track which hand triggered the menu
        public static int LastTriggeredHand { get; set; } // -1 = left, 1 = right, 0 = unknown

        private void Start()
        {
            if (m_HideOnStart)
                gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            try
            {
                if (m_MainCamTransform == null)
                {
                    m_MainCamTransform = Camera.main?.transform;
                    if (m_MainCamTransform == null)
                    {
                        Debug.LogError("PopoutUI: Could not find main camera, disabling component");
                        enabled = false;
                        return;
                    }
                }

                // Auto-set X offset based on which hand triggered the menu
                float currentXOffset = m_XOffset;
            if (m_AutoSetXOffsetFromInput)
            {
                int handToUse = 0;
                
                if (m_ManualTestMode)
                {
                    handToUse = m_ManualTestHand;
                    Debug.Log($"PopoutUI: Using MANUAL test mode, hand = {handToUse}");
                }
                else if (m_UseHeadDirectionDetection)
                {
                    handToUse = DetectHeadDirection();
                    Debug.Log($"PopoutUI: Using HEAD DIRECTION detection, result = {handToUse}");
                }
                else
                {
                    handToUse = LastTriggeredHand;
                    Debug.Log($"PopoutUI: Using WRIST ANCHOR detection, LastTriggeredHand = {handToUse}");
                }
                
                if (handToUse == -1) // Left side
                {
                    currentXOffset = m_LeftHandXOffset;
                    Debug.Log($"PopoutUI: Using LEFT side offset = {currentXOffset}");
                }
                else if (handToUse == 1) // Right side
                {
                    currentXOffset = m_RightHandXOffset;
                    Debug.Log($"PopoutUI: Using RIGHT side offset = {currentXOffset}");
                }
                else
                {
                    Debug.Log($"PopoutUI: Using DEFAULT offset = {currentXOffset} (direction value was {handToUse})");
                }
            }
            else
            {
                Debug.Log($"PopoutUI: Auto-set is DISABLED, using manual X offset = {currentXOffset}");
            }

            transform.position = m_MainCamTransform.position;

            // Move forward from camera
            transform.position += m_MainCamTransform.forward * m_DistanceFromFace;
            
            // Apply vertical offset (downward)
            transform.position += Vector3.up * -m_YOffset;
            
            // Apply horizontal offset (left/right) using the determined offset
            if (m_UseRelativePositioning)
            {
                // Use camera's right vector for relative positioning
                transform.position += m_MainCamTransform.right * currentXOffset;
            }
            else
            {
                // Use world X-axis for absolute positioning
                transform.position += Vector3.right * currentXOffset;
            }

            // Always face the camera from the final position
            Vector3 directionToCamera = m_MainCamTransform.position - transform.position;
            directionToCamera.y = 0; // Keep it level (no pitch)
            
            if (directionToCamera != Vector3.zero)
            {
                // Reverse the direction so the UI faces towards the camera, not away from it
                transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PopoutUI: Error in OnEnable: {ex.Message}");
                // Use default positioning if there's an error
                transform.position = m_MainCamTransform.position + m_MainCamTransform.forward * m_DistanceFromFace;
            }
        }

        private int DetectHeadDirection()
        {
            // Safety check to prevent infinite loops
            if (m_MainCamTransform == null)
            {
                Debug.LogError("PopoutUI: Main camera transform is null, cannot detect head direction");
                return 0;
            }

            // If no player body reference is set, try to find it automatically (but only once)
            if (m_PlayerBodyTransform == null && !m_HasSearchedForPlayerBody)
            {
                m_HasSearchedForPlayerBody = true;
                m_PlayerBodyTransform = FindPlayerBodyTransform();
            }
            
            if (m_PlayerBodyTransform == null)
            {
                Debug.LogWarning("PopoutUI: No player body transform found for head direction detection");
                return 0;
            }

            // Safety check to prevent self-reference issues
            if (m_PlayerBodyTransform == transform || m_MainCamTransform == transform)
            {
                Debug.LogError("PopoutUI: Invalid transform reference detected, preventing infinite loop");
                return 0;
            }

            try
            {
                // Get the player's body forward direction (where the body is facing)
                Vector3 bodyForward = m_PlayerBodyTransform.forward;
                
                // Get the head/camera forward direction (where the user is looking)
                Vector3 headForward = m_MainCamTransform.forward;
                
                // Calculate the dot product of head direction with body's right vector
                Vector3 bodyRight = m_PlayerBodyTransform.right;
                float rightDot = Vector3.Dot(headForward, bodyRight);
                
                Debug.Log($"PopoutUI: Body forward: {bodyForward}, Head forward: {headForward}, Right dot: {rightDot:F3}");
                
                // Check if user is looking significantly left or right relative to their body
                if (rightDot > m_DirectionThreshold)
                {
                    // Looking right relative to body
                    Debug.Log($"PopoutUI: User looking RIGHT (dot: {rightDot:F3} > threshold: {m_DirectionThreshold})");
                    return 1;
                }
                else if (rightDot < -m_DirectionThreshold)
                {
                    // Looking left relative to body
                    Debug.Log($"PopoutUI: User looking LEFT (dot: {rightDot:F3} < -threshold: {-m_DirectionThreshold})");
                    return -1;
                }
                else
                {
                    // Looking mostly forward
                    Debug.Log($"PopoutUI: User looking FORWARD (dot: {rightDot:F3} within threshold: ±{m_DirectionThreshold})");
                    return 0;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PopoutUI: Error in DetectHeadDirection: {ex.Message}");
                return 0;
            }
        }

        private Transform FindPlayerBodyTransform()
        {
            try
            {
                // Try to find common player prefab names
                GameObject playerObject = GameObject.Find("XR Origin") ?? 
                                         GameObject.Find("XROrigin") ?? 
                                         GameObject.Find("Player") ?? 
                                         GameObject.Find("VR Player") ??
                                         GameObject.Find("XR Rig") ??
                                         GameObject.Find("XRRig");
                
                if (playerObject != null)
                {
                    Debug.Log($"PopoutUI: Found player body transform: {playerObject.name}");
                    return playerObject.transform;
                }
                
                // Try to find by going up the camera hierarchy (with safety limit)
                Transform current = m_MainCamTransform?.parent;
                int safetyCounter = 0;
                const int maxHierarchyDepth = 10; // Prevent infinite loops
                
                while (current != null && safetyCounter < maxHierarchyDepth)
                {
                    string name = current.name.ToLower();
                    if (name.Contains("origin") || name.Contains("player") || name.Contains("rig"))
                    {
                        Debug.Log($"PopoutUI: Found player body transform via camera hierarchy: {current.name}");
                        return current;
                    }
                    current = current.parent;
                    safetyCounter++;
                }
                
                if (safetyCounter >= maxHierarchyDepth)
                {
                    Debug.LogWarning("PopoutUI: Reached maximum hierarchy search depth, stopping search");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PopoutUI: Error in FindPlayerBodyTransform: {ex.Message}");
            }
            
            return null;
        }
    }
}

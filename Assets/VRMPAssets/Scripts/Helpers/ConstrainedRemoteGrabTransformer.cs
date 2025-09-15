using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace XRMultiplayer
{
    /// <summary>
    /// A grab transformer that constrains object movement for remote grabbing.
    /// Locks Y position and rotation while allowing X and Z movement.
    /// </summary>
    public class ConstrainedRemoteGrabTransformer : XRBaseGrabTransformer
    {
        [SerializeField]
        [Tooltip("Lock the Y position to prevent vertical movement")]
        bool m_LockYPosition = true;

        [SerializeField]
        [Tooltip("Lock all rotation axes")]
        bool m_LockRotation = true;

        [SerializeField]
        [Tooltip("Maximum distance from initial position for X and Z movement")]
        float m_MaxMovementRange = 5f;

        Vector3 m_InitialPosition;
        Quaternion m_InitialRotation;

        /// <inheritdoc />
        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        /// <inheritdoc />
        public override void OnLink(XRGrabInteractable grabInteractable)
        {
            base.OnLink(grabInteractable);
            m_InitialPosition = grabInteractable.transform.position;
            m_InitialRotation = grabInteractable.transform.rotation;
        }

        /// <inheritdoc />
        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            Vector3 newPosition = targetPose.position;
            Quaternion newRotation = targetPose.rotation;

            // Lock Y position
            if (m_LockYPosition)
            {
                newPosition.y = m_InitialPosition.y;
            }

            // Constrain X and Z movement to maximum range
            Vector3 flatMovement = new Vector3(newPosition.x - m_InitialPosition.x, 0, newPosition.z - m_InitialPosition.z);
            if (flatMovement.magnitude > m_MaxMovementRange)
            {
                flatMovement = flatMovement.normalized * m_MaxMovementRange;
                newPosition.x = m_InitialPosition.x + flatMovement.x;
                newPosition.z = m_InitialPosition.z + flatMovement.z;
            }

            // Lock rotation
            if (m_LockRotation)
            {
                newRotation = m_InitialRotation;
            }

            targetPose.position = newPosition;
            targetPose.rotation = newRotation;
        }
    }
}

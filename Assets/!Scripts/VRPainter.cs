using UnityEngine;
using UnityEngine.InputSystem;


public class VRPainter : MonoBehaviour
{
    [SerializeField] private InputActionReference selectAction; // Still serialized for flexibility
    [SerializeField] private Material brushMaterial;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

    [SerializeField] private float brushSize = 0.01f;

    void Start()
    {
        rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        if (rayInteractor == null)
        {
            Debug.LogError("XRRayInteractor component not found on this GameObject.");
        }

        // Assign the Select action programmatically if not set in Inspector
        if (selectAction == null)
        {
            InputActionAsset inputActions = Resources.Load<InputActionAsset>("XRI Default Input Actions");
            if (inputActions != null)
            {
                selectAction = InputActionReference.Create(inputActions.FindAction("XRI RightHand Interaction/Select"));
                if (selectAction == null)
                {
                    Debug.LogError("Could not find Select action in XRI Default Input Actions.");
                }
                else
                {
                    selectAction.action.Enable();
                }
            }
            else
            {
                Debug.LogError("XRI Default Input Actions asset not found in Resources.");
            }
        }
        else
        {
            selectAction.action.Enable();
        }
    }

    void OnDestroy()
    {
        if (selectAction != null)
        {
            selectAction.action.Disable();
        }
    }
    
    void Update()
    {
        if (selectAction != null && selectAction.action.ReadValue<float>() > 0.5f)
        {
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                Renderer renderer = hit.collider.GetComponent<Renderer>();
                if (renderer != null && renderer.material.mainTexture is RenderTexture rt)
                {
                    PaintOnRenderTexture(rt, hit.textureCoord);
                }
            }
        }
    }

    // PaintOnRenderTexture method remains unchanged
    void PaintOnRenderTexture(RenderTexture rt, Vector2 uv) { /* ... */ }
}
using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class NetworkCanvas : NetworkBehaviour
{
    private Texture2D sharedTexture;
    [SerializeField] private Material canvasMaterial; // Assign in Inspector
    private bool isInitialized = false;

    private void Start()
    {
        // Initialize for offline mode or if network isn't ready yet
        if (!IsSpawned)
        {
            InitializeCanvas();
        }
    }

    public override void OnNetworkSpawn()
    {
        // Initialize for network mode
        if (!isInitialized)
        {
            InitializeCanvas();
        }
    }

    private void InitializeCanvas()
    {
        if (isInitialized) return;

        // Initialize texture with transparent pixels
        sharedTexture = new Texture2D(1028, 1028, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[1028 * 1028];
        // Initialize with transparent pixels instead of white
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        sharedTexture.SetPixels(pixels);
        sharedTexture.Apply();
        
        // Apply texture to material and configure for transparency
        if (canvasMaterial != null)
        {
            // Create a new material instance to avoid modifying the original asset
            Material materialInstance = new Material(canvasMaterial);
            materialInstance.mainTexture = sharedTexture;
            
            // Configure material for transparency
            ConfigureMaterialForTransparency(materialInstance);
            
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material = materialInstance;
            }
        }

        isInitialized = true;
    }

    private void ConfigureMaterialForTransparency(Material material)
    {
        // Set rendering mode to transparent
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3); // Transparent mode
        }
        
        // Enable alpha blending
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0); // Disable Z-write for transparency
        }
        
        // Set render queue for transparency
        material.renderQueue = 3000; // Transparent queue
        
        // Enable keywords for transparency
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    /// <summary>
    /// Main paint method that works in both offline and online modes
    /// </summary>
    public void Paint(Vector2 uv, Color color, int brushSize)
    {
        if (!isInitialized)
        {
            InitializeCanvas();
        }

        // Apply paint locally first
        ApplyPaintLocally(uv, color, brushSize);

        // If we're in network mode and this is the server or client, send to network
        if (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                // If we're the server, broadcast to all clients except ourselves
                var targetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Where(id => id != NetworkManager.Singleton.LocalClientId).ToArray();
                if (targetClientIds.Length > 0)
                {
                    PaintClientRpc(uv, color, brushSize, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = targetClientIds }
                    });
                }
            }
            else
            {
                // If we're a client, send to server which will broadcast to others
                PaintServerRpc(uv, color, brushSize, NetworkManager.Singleton.LocalClientId);
            }
        }
        // If not in network mode, just the local paint is enough (offline mode)
    }

    [ServerRpc(RequireOwnership = false)]
    public void PaintServerRpc(Vector2 uv, Color color, int brushSize, ulong senderClientId)
    {
        // Broadcast the paint action to all clients except the sender
        var targetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Where(id => id != senderClientId).ToArray();
        PaintClientRpc(uv, color, brushSize, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = targetClientIds }
        });
    }

    [ClientRpc]
    private void PaintClientRpc(Vector2 uv, Color color, int brushSize, ClientRpcParams clientRpcParams)
    {
        ApplyPaintLocally(uv, color, brushSize);
    }

    public void ApplyPaintLocally(Vector2 uv, Color color, int brushSize)
    {
        if (sharedTexture == null)
        {
            InitializeCanvas();
        }

        int x = (int)(uv.x * sharedTexture.width);
        int y = (int)(uv.y * sharedTexture.height);
        for (int i = -brushSize; i <= brushSize; i++)
        {
            for (int j = -brushSize; j <= brushSize; j++)
            {
                if (i * i + j * j <= brushSize * brushSize)
                {
                    int px = Mathf.Clamp(x + i, 0, sharedTexture.width - 1);
                    int py = Mathf.Clamp(y + j, 0, sharedTexture.height - 1);
                    sharedTexture.SetPixel(px, py, color);
                }
            }
        }
        sharedTexture.Apply();
    }

    /// <summary>
    /// Clears the entire canvas by resetting all pixels to transparent
    /// </summary>
    public void ClearCanvas()
    {
        if (sharedTexture == null)
        {
            InitializeCanvas();
            return;
        }

        // Reset all pixels to transparent
        Color[] pixels = new Color[sharedTexture.width * sharedTexture.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        sharedTexture.SetPixels(pixels);
        sharedTexture.Apply();

        // If in network mode, sync the cleared canvas
        if (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                // Broadcast clear to all clients
                ClearCanvasClientRpc();
            }
            else
            {
                // Send clear request to server
                ClearCanvasServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearCanvasServerRpc()
    {
        // Clear on server and broadcast to all clients
        ClearCanvasLocally();
        ClearCanvasClientRpc();
    }

    [ClientRpc]
    private void ClearCanvasClientRpc()
    {
        ClearCanvasLocally();
    }

    private void ClearCanvasLocally()
    {
        if (sharedTexture == null) return;

        Color[] pixels = new Color[sharedTexture.width * sharedTexture.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        sharedTexture.SetPixels(pixels);
        sharedTexture.Apply();
    }

    public void SyncTextureToClient(ulong clientId)
    {
        if (!IsServer) return;

        byte[] textureData = sharedTexture.EncodeToPNG();
        const int chunkSize = 16300;
        int chunks = Mathf.CeilToInt((float)textureData.Length / chunkSize);

        for (int i = 0; i < chunks; i++)
        {
            int start = i * chunkSize;
            int length = Mathf.Min(chunkSize, textureData.Length - start);
            byte[] chunk = new byte[length];
            System.Array.Copy(textureData, start, chunk, 0, length);
            SyncTextureChunkClientRpc(chunk, i, chunks, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });
        }
    }

    [ClientRpc]
    private void SyncTextureChunkClientRpc(byte[] chunk, int index, int totalChunks, ClientRpcParams clientRpcParams)
    {
        static byte[] CombineChunks(byte[] existing, byte[] newChunk, int index, int chunkSize)
        {
            if (existing == null)
                return newChunk;
            byte[] combined = new byte[existing.Length + newChunk.Length];
            System.Array.Copy(existing, 0, combined, 0, existing.Length);
            System.Array.Copy(newChunk, 0, combined, existing.Length, newChunk.Length);
            return combined;
        }

        if (index == 0)
            textureBuffer = null;

        textureBuffer = CombineChunks(textureBuffer, chunk, index, chunk.Length);

        if (index == totalChunks - 1)
        {
            sharedTexture.LoadImage(textureBuffer);
            sharedTexture.Apply();
            textureBuffer = null;
        }
    }

    private byte[] textureBuffer;

    /// <summary>
    /// Public method to get the current canvas texture for saving purposes
    /// </summary>
    public Texture2D GetCanvasTexture()
    {
        return sharedTexture;
    }

    /// <summary>
    /// Public method to check if the canvas is initialized
    /// </summary>
    public bool IsCanvasInitialized()
    {
        return isInitialized && sharedTexture != null;
    }
}
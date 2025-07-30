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

        // Initialize texture
        sharedTexture = new Texture2D(1028, 1028, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[1028 * 1028];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        sharedTexture.SetPixels(pixels);
        sharedTexture.Apply();
        
        // Apply texture to material
        if (canvasMaterial != null)
        {
            canvasMaterial.mainTexture = sharedTexture;
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material = canvasMaterial;
            }
        }

        isInitialized = true;
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
}
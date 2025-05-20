using UnityEngine;
using Unity.Netcode;

public class NetworkCanvas : NetworkBehaviour
{
    private Texture2D sharedTexture;
    [SerializeField] private Material canvasMaterial; // Assign in Inspector

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialize texture on server
            sharedTexture = new Texture2D(1028, 1028, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[1028 * 1028];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            sharedTexture.SetPixels(pixels);
            sharedTexture.Apply();
            canvasMaterial.mainTexture = sharedTexture;
            GetComponent<MeshRenderer>().material = canvasMaterial;
        }
        else
        {
            sharedTexture = new Texture2D(1028, 1028, TextureFormat.RGBA32, false);
            canvasMaterial.mainTexture = sharedTexture;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PaintServerRpc(Vector2 uv, Color color, int brushSize)
    {
        // Broadcast painting action to all clients
        PaintClientRpc(uv, color, brushSize);
    }

    [ClientRpc]
    private void PaintClientRpc(Vector2 uv, Color color, int brushSize)
    {
        // Apply painting locally on all clients
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

    // Rest of the script (SyncTextureToClient, SyncTextureChunkClientRpc, textureBuffer) remains unchanged
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
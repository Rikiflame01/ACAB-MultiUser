using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections;

public class CanvasSaver : MonoBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private NetworkCanvas networkCanvas;
    [SerializeField] private int exportWidth = 1080;
    [SerializeField] private int exportHeight = 1080;
    
    [Header("File Settings")]
    [SerializeField] private string defaultFileName = "my_painting";
    
    [Header("VR/Fullscreen Settings")]
    [SerializeField] private bool exitVROnSave = true;
    [SerializeField] private bool exitFullscreenOnSave = true;
    [SerializeField] private bool showVRExitMessage = true;
    
    // JavaScript function import for WebGL
    [DllImport("__Internal")]
    private static extern void DownloadFile(byte[] array, int byteLength, string fileName);
    
    [DllImport("__Internal")]
    private static extern int ExitVR();

    private void Start()
    {
        // Auto-find NetworkCanvas if not assigned
        if (networkCanvas == null)
        {
            networkCanvas = FindFirstObjectByType<NetworkCanvas>();
            if (networkCanvas == null)
            {
                Debug.LogError("CanvasSaver: No NetworkCanvas found in the scene!");
            }
        }
    }

    /// <summary>
    /// Saves the current canvas as a PNG file with specified dimensions
    /// </summary>
    public void SaveCanvasAsPNG()
    {
        SaveCanvasAsPNG(defaultFileName);
    }

    /// <summary>
    /// Saves the current canvas as a PNG file with a custom filename
    /// </summary>
    /// <param name="fileName">Name of the file (without extension)</param>
    public void SaveCanvasAsPNG(string fileName)
    {
        if (networkCanvas == null)
        {
            Debug.LogError("CanvasSaver: NetworkCanvas is not assigned!");
            return;
        }

        try
        {
            // Get the current texture from the NetworkCanvas
            Texture2D canvasTexture = GetCanvasTexture();
            
            if (canvasTexture == null)
            {
                Debug.LogError("CanvasSaver: Could not retrieve canvas texture!");
                return;
            }

            // Create a resized version if needed
            Texture2D exportTexture = ResizeTexture(canvasTexture, exportWidth, exportHeight);
            
            // Convert to PNG
            byte[] pngData = exportTexture.EncodeToPNG();
            
            // Add timestamp to filename to avoid conflicts
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fullFileName = $"{fileName}_{timestamp}.png";
            
            // Exit VR/Fullscreen before saving (better user experience for downloads)
            ExitVRAndFullscreen();
            
            // Add a small delay to ensure VR exit completes before download
            #if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, give the browser a moment to process the VR exit
            StartCoroutine(DelayedSave(pngData, fullFileName));
            #else
            // In editor/standalone, save immediately
            SavePNGFile(pngData, fullFileName);
            #endif
            
            // Clean up temporary texture if we created one
            if (exportTexture != canvasTexture)
            {
                DestroyImmediate(exportTexture);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CanvasSaver: Error saving canvas - {e.Message}");
        }
    }

    /// <summary>
    /// Gets the current texture from the NetworkCanvas
    /// </summary>
    private Texture2D GetCanvasTexture()
    {
        // Check if canvas is initialized
        if (!networkCanvas.IsCanvasInitialized())
        {
            Debug.LogWarning("CanvasSaver: Canvas is not initialized yet. Try painting something first.");
            return null;
        }

        // Get the texture directly from the NetworkCanvas
        return networkCanvas.GetCanvasTexture();
    }

    /// <summary>
    /// Resizes a texture to the specified dimensions
    /// </summary>
    private Texture2D ResizeTexture(Texture2D originalTexture, int newWidth, int newHeight)
    {
        // If already the correct size, return original
        if (originalTexture.width == newWidth && originalTexture.height == newHeight)
        {
            return originalTexture;
        }

        // Create a temporary RenderTexture
        RenderTexture renderTexture = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
        renderTexture.filterMode = FilterMode.Bilinear;

        // Set the RenderTexture as active and draw the original texture to it
        RenderTexture.active = renderTexture;
        Graphics.Blit(originalTexture, renderTexture);

        // Create a new Texture2D and read the RenderTexture into it
        Texture2D resizedTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resizedTexture.Apply();

        // Clean up
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);

        return resizedTexture;
    }

    /// <summary>
    /// Saves the PNG data based on the current platform
    /// </summary>
    private void SavePNGFile(byte[] pngData, string fileName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // For WebGL builds, use JavaScript to trigger browser download
        try
        {
            DownloadFile(pngData, pngData.Length, fileName);
        }
        catch (Exception e)
        {
            Debug.LogError($"CanvasSaver: WebGL download failed - {e.Message}");
        }
#else
        // For editor and standalone builds, save to persistent data path
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        try
        {
            System.IO.File.WriteAllBytes(filePath, pngData);
            
            // Try to open the folder (Windows only)
            #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + filePath.Replace("/", "\\"));
            }
            catch
            {
                // Explorer failed to open
            }
            #endif
        }
        catch (Exception e)
        {
            Debug.LogError($"CanvasSaver: Failed to save file - {e.Message}");
        }
#endif
    }

    /// <summary>
    /// Public method to set custom export dimensions
    /// </summary>
    public void SetExportDimensions(int width, int height)
    {
        exportWidth = Mathf.Max(64, width);   // Minimum 64px
        exportHeight = Mathf.Max(64, height); // Minimum 64px
    }

    /// <summary>
    /// Public method to set default filename
    /// </summary>
    public void SetDefaultFileName(string fileName)
    {
        if (!string.IsNullOrEmpty(fileName))
        {
            defaultFileName = fileName;
        }
    }

    /// <summary>
    /// Enable or disable VR exit on save
    /// </summary>
    public void SetVRExitOnSave(bool enabled)
    {
        exitVROnSave = enabled;
    }

    /// <summary>
    /// Enable or disable fullscreen exit on save
    /// </summary>
    public void SetFullscreenExitOnSave(bool enabled)
    {
        exitFullscreenOnSave = enabled;
    }

    /// <summary>
    /// Manually exit VR and fullscreen (can be called independently)
    /// </summary>
    public void ExitVRAndFullscreenManually()
    {
        ExitVRAndFullscreen();
    }

    /// <summary>
    /// Delayed save coroutine for WebGL to allow VR exit to complete
    /// </summary>
    private System.Collections.IEnumerator DelayedSave(byte[] pngData, string fileName)
    {
        // Wait a brief moment for VR exit to complete
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("CanvasSaver: Proceeding with delayed save after VR exit...");
        SavePNGFile(pngData, fileName);
    }

    /// <summary>
    /// Exits VR and fullscreen mode to provide better file download experience
    /// </summary>
    private void ExitVRAndFullscreen()
    {
        try
        {
            bool vrExited = false;
            bool fullscreenExited = false;

            // Exit VR mode if enabled and active
            if (exitVROnSave)
            {
                #if UNITY_XR_MANAGEMENT
                // Check if XR is active and stop it
                var xrGeneralSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                if (xrGeneralSettings != null && xrGeneralSettings.Manager != null && xrGeneralSettings.Manager.activeLoader != null)
                {
                    xrGeneralSettings.Manager.StopSubsystems();
                    xrGeneralSettings.Manager.DeinitializeLoader();
                    vrExited = true;
                }
                #endif

                // Alternative VR exit method for WebXR or other VR systems
                #if UNITY_WEBGL && !UNITY_EDITOR
                // For WebXR, we can try to exit VR through JavaScript
                try
                {
                    Debug.Log("CanvasSaver: Attempting WebGL VR exit...");
                    int exitResult = ExitVR();
                    if (exitResult == 1)
                    {
                        vrExited = true;
                        Debug.Log("CanvasSaver: WebGL VR exit successful");
                    }
                    else
                    {
                        Debug.Log("CanvasSaver: WebGL VR exit returned no changes");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"CanvasSaver: WebGL VR exit failed: {ex.Message}");
                }
                #endif
            }

            // Exit fullscreen mode if enabled and active
            if (exitFullscreenOnSave)
            {
                Debug.Log($"CanvasSaver: Checking fullscreen state - Screen.fullScreen: {Screen.fullScreen}");
                if (Screen.fullScreen)
                {
                    Debug.Log("CanvasSaver: Unity reports fullscreen active - exiting...");
                    Screen.fullScreen = false;
                    fullscreenExited = true;
                }

                // Additional fullscreen check for WebGL
                #if UNITY_WEBGL && !UNITY_EDITOR
                // WebGL might not report fullscreen correctly through Screen.fullScreen
                // The JavaScript function will handle browser fullscreen detection
                #endif
            }

            // Show user message if something was exited
            if (showVRExitMessage && (vrExited || fullscreenExited))
            {
                string message = "Exited ";
                if (vrExited && fullscreenExited)
                    message += "VR and fullscreen ";
                else if (vrExited)
                    message += "VR ";
                else if (fullscreenExited)
                    message += "fullscreen ";
                
                message += "mode for file download.";
                Debug.Log($"CanvasSaver: {message}");
            }
            else if (showVRExitMessage)
            {
                Debug.Log("CanvasSaver: No VR or fullscreen mode detected to exit");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"CanvasSaver: Could not exit VR/Fullscreen mode - {e.Message}");
            // Don't let VR exit errors prevent the save operation
        }
    }
}

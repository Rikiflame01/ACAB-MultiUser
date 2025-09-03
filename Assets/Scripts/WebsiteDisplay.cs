using UnityEngine;
using UnityEngine.UI;

public class WebsiteDisplay : MonoBehaviour
{
    [Header("Website Settings")]
    public string websiteURL = "https://www.example.com";
    
    [Header("Fallback Options")]
    public bool openInNewTabIfBlocked = true;
    public string fallbackMessage = "This website cannot be displayed in an iframe. Opening in new tab...";
    
    [Header("UI Controls")]
    public Button showWebsiteButton;
    public Button hideWebsiteButton;
    
    [Header("Overlay Settings")]
    public bool allowUserToClose = true;
    [Range(50, 100)]
    public int overlayWidthPercent = 90;
    [Range(50, 100)]
    public int overlayHeightPercent = 90;
    
    private bool isWebsiteVisible = false;

    void Start()
    {
        // Setup button listeners
        if (showWebsiteButton != null)
            showWebsiteButton.onClick.AddListener(ShowWebsite);
            
        if (hideWebsiteButton != null)
            hideWebsiteButton.onClick.AddListener(HideWebsite);
    }

    public void ShowWebsite()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        ShowWebGLOverlay();
        #else
        Debug.LogWarning("Website display is only supported in WebGL builds. Use Application.OpenURL for other platforms.");
        Application.OpenURL(websiteURL);
        #endif
        
        isWebsiteVisible = true;
    }

    public void HideWebsite()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        HideWebGLOverlay();
        #endif
        
        isWebsiteVisible = false;
    }

    public void ToggleWebsite()
    {
        if (isWebsiteVisible)
            HideWebsite();
        else
            ShowWebsite();
    }

    #if UNITY_WEBGL && !UNITY_EDITOR
    private void ShowWebGLOverlay()
    {
        string closeButtonHtml = allowUserToClose ? 
            $@"
                // Create close button
                window.unityCloseBtn = document.createElement('button');
                window.unityCloseBtn.innerHTML = '×';
                window.unityCloseBtn.style.position = 'absolute';
                window.unityCloseBtn.style.top = '10px';
                window.unityCloseBtn.style.right = '10px';
                window.unityCloseBtn.style.width = '40px';
                window.unityCloseBtn.style.height = '40px';
                window.unityCloseBtn.style.fontSize = '24px';
                window.unityCloseBtn.style.backgroundColor = '#ff4444';
                window.unityCloseBtn.style.color = 'white';
                window.unityCloseBtn.style.border = 'none';
                window.unityCloseBtn.style.borderRadius = '50%';
                window.unityCloseBtn.style.cursor = 'pointer';
                window.unityCloseBtn.onclick = function() {{
                    window.unityWebOverlay.style.display = 'none';
                    unityInstance.SendMessage('{gameObject.name}', 'OnWebsiteClosed');
                }};
                window.unityWebOverlay.appendChild(window.unityCloseBtn);
            " : "";

        string jsCode = $@"
            if (!window.unityWebOverlay) {{
                // Create overlay container
                window.unityWebOverlay = document.createElement('div');
                window.unityWebOverlay.style.position = 'fixed';
                window.unityWebOverlay.style.top = '0';
                window.unityWebOverlay.style.left = '0';
                window.unityWebOverlay.style.width = '100vw';
                window.unityWebOverlay.style.height = '100vh';
                window.unityWebOverlay.style.backgroundColor = 'rgba(0,0,0,0.8)';
                window.unityWebOverlay.style.zIndex = '9999';
                window.unityWebOverlay.style.display = 'flex';
                window.unityWebOverlay.style.justifyContent = 'center';
                window.unityWebOverlay.style.alignItems = 'center';
                
                // Create iframe
                window.unityWebIframe = document.createElement('iframe');
                window.unityWebIframe.style.width = '{overlayWidthPercent}%';
                window.unityWebIframe.style.height = '{overlayHeightPercent}%';
                window.unityWebIframe.style.border = 'none';
                window.unityWebIframe.style.borderRadius = '10px';
                window.unityWebIframe.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture';
                
                // Handle iframe load errors
                window.unityWebIframe.onerror = function() {{
                    console.error('Failed to load iframe');
                    if ({openInNewTabIfBlocked.ToString().ToLower()}) {{
                        window.open('{websiteURL}', '_blank');
                        window.unityWebOverlay.style.display = 'none';
                        unityInstance.SendMessage('{gameObject.name}', 'OnWebsiteOpenedInNewTab');
                    }}
                }};
                
                // Check if iframe is blocked after a short delay
                setTimeout(function() {{
                    try {{
                        if (window.unityWebIframe.contentDocument === null) {{
                            console.warn('Iframe blocked by X-Frame-Options');
                            if ({openInNewTabIfBlocked.ToString().ToLower()}) {{
                                window.open('{websiteURL}', '_blank');
                                window.unityWebOverlay.style.display = 'none';
                                unityInstance.SendMessage('{gameObject.name}', 'OnWebsiteOpenedInNewTab');
                            }}
                        }}
                    }} catch(e) {{
                        console.warn('Iframe access blocked:', e.message);
                        if ({openInNewTabIfBlocked.ToString().ToLower()}) {{
                            window.open('{websiteURL}', '_blank');
                            window.unityWebOverlay.style.display = 'none';
                            unityInstance.SendMessage('{gameObject.name}', 'OnWebsiteOpenedInNewTab');
                        }}
                    }}
                }}, 1000);
                
                window.unityWebIframe.src = '{websiteURL}';
                
                window.unityWebOverlay.appendChild(window.unityWebIframe);
                {closeButtonHtml}
                document.body.appendChild(window.unityWebOverlay);
            }} else {{
                window.unityWebOverlay.style.display = 'flex';
                window.unityWebIframe.src = '{websiteURL}';
            }}
        ";
        
        Application.ExternalEval(jsCode);
    }

    private void HideWebGLOverlay()
    {
        Application.ExternalEval(@"
            if (window.unityWebOverlay) {
                window.unityWebOverlay.style.display = 'none';
            }
        ");
    }
    #endif

    // Called from JavaScript when overlay is closed
    public void OnWebsiteClosed()
    {
        isWebsiteVisible = false;
        Debug.Log("Website overlay was closed");
    }

    // Called from JavaScript when website opens in new tab due to iframe blocking
    public void OnWebsiteOpenedInNewTab()
    {
        isWebsiteVisible = false;
        Debug.Log($"Website opened in new tab due to iframe restrictions: {websiteURL}");
    }

    void OnDestroy()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        // Clean up overlay when object is destroyed
        Application.ExternalEval(@"
            if (window.unityWebOverlay) {
                document.body.removeChild(window.unityWebOverlay);
                window.unityWebOverlay = null;
                window.unityWebIframe = null;
                window.unityCloseBtn = null;
            }
        ");
        #endif
    }
}

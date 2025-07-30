mergeInto(LibraryManager.library, {

  DownloadFile: function (array, size, fileNamePtr) {
    var fileName = UTF8ToString(fileNamePtr);
    
    // Create a blob from the byte array
    var bytes = new Uint8Array(size);
    for (var i = 0; i < size; i++) {
      bytes[i] = HEAPU8[array + i];
    }
    
    var blob = new Blob([bytes], { type: 'image/png' });
    
    // Create download link
    var link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    
    // Trigger download
    document.body.appendChild(link);
    link.click();
    
    // Clean up
    document.body.removeChild(link);
    URL.revokeObjectURL(link.href);
    
    console.log('Download triggered for: ' + fileName);
  },

  ExitVR: function() {
    // Define exitVR function on window if it doesn't exist
    if (typeof window !== 'undefined' && typeof window.exitVR === 'undefined') {
      window.exitVR = function() {
        try {
          console.log('Starting VR/Fullscreen exit process...');
          var exitedSomething = false;
          
          // Exit WebXR session if active
          if (navigator.xr && window.xrSession) {
            console.log('WebXR session detected, ending...');
            window.xrSession.end();
            window.xrSession = null;
            exitedSomething = true;
          }
          
          // Check for WebXR in Unity WebGL context
          if (typeof unityInstance !== 'undefined' && unityInstance.Module && unityInstance.Module.webxr) {
            console.log('Unity WebXR detected, attempting exit...');
            try {
              unityInstance.Module.webxr.exitXR();
              exitedSomething = true;
            } catch(e) {
              console.log('Unity WebXR exit failed:', e);
            }
          }
          
          // Exit fullscreen - comprehensive approach
          var isFullscreen = document.fullscreenElement || 
                           document.webkitFullscreenElement || 
                           document.mozFullScreenElement || 
                           document.msFullscreenElement;
          
          if (isFullscreen) {
            console.log('Fullscreen detected, exiting...');
            if (document.exitFullscreen) {
              document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
              document.webkitExitFullscreen();
            } else if (document.mozCancelFullScreen) {
              document.mozCancelFullScreen();
            } else if (document.msExitFullscreen) {
              document.msExitFullscreen();
            }
            exitedSomething = true;
          }
          
          // Exit pointer lock if active
          if (document.pointerLockElement) {
            console.log('Pointer lock detected, exiting...');
            document.exitPointerLock();
            exitedSomething = true;
          }
          
          // Force fullscreen exit via canvas if Unity canvas is detected
          var canvas = document.querySelector('#unity-canvas') || document.querySelector('canvas');
          if (canvas && canvas.requestFullscreen) {
            // If canvas is in fullscreen, exit it
            if (document.fullscreenElement === canvas) {
              console.log('Canvas fullscreen detected, forcing exit...');
              if (document.exitFullscreen) {
                document.exitFullscreen();
                exitedSomething = true;
              }
            }
          }
          
          if (exitedSomething) {
            console.log('VR/Fullscreen exit completed successfully');
          } else {
            console.log('No VR or fullscreen mode detected to exit');
          }
          
          return exitedSomething;
        } catch (error) {
          console.warn('Error exiting VR/Fullscreen:', error);
          return false;
        }
      };
    }
    
    // Call the exitVR function and return result
    var result = false;
    if (typeof window !== 'undefined' && typeof window.exitVR === 'function') {
      result = window.exitVR();
    }
    
    return result ? 1 : 0;
  }

});

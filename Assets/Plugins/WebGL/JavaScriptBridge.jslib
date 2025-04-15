mergeInto(LibraryManager.library, {
  // Simple function to request the seed phrase from Vue
  RequestSeedPhraseFromJS: function() {
    try {
      // Simple retry mechanism
      var maxRetries = 5;
      var retryDelay = 500; // ms
      var retryCount = 0;
      
      function attemptToSendSeedPhrase() {
        if (window && window.sendSeedPhraseToUnity) {
          console.log("Unity is requesting seed phrase from Vue");
          window.sendSeedPhraseToUnity();
        } else {
          retryCount++;
          if (retryCount <= maxRetries) {
            console.log("sendSeedPhraseToUnity not found, retrying in " + retryDelay + "ms (attempt " + retryCount + "/" + maxRetries + ")");
            setTimeout(attemptToSendSeedPhrase, retryDelay);
          } else {
            console.warn('sendSeedPhraseToUnity not found on window after ' + maxRetries + ' attempts.');
          }
        }
      }
      
      // Start the retry process
      attemptToSendSeedPhrase();
    } catch (error) {
      console.error('Error requesting seed phrase from web app:', error);
    }
  },
  
  // Function to call WebGLBridge static method directly from JavaScript
  CallStaticMethod: function(className, methodName, arg) {
    try {
      console.log("Calling static method: " + UTF8ToString(className) + "." + UTF8ToString(methodName));
      
      var classNameStr = UTF8ToString(className);
      var methodNameStr = UTF8ToString(methodName);
      var argStr = UTF8ToString(arg);
      
      // For WebGL, we need to use the unityInstance.SendMessage approach
      if (window.gameInstance) {
        // For WebGLBridge.SetSeedPhraseGlobal, we need a special approach
        if (classNameStr === "WebGLBridge" && methodNameStr === "SetSeedPhraseGlobal") {
          // Find appropriate GameObject that can forward to WebGLBridge static methods
          var bridgeGameObjects = ['Bridge', 'WebGLBridge', 'GameManager', 'SceneManager', 'EventSystem'];
          var messageSent = false;
          
          for (var i = 0; i < bridgeGameObjects.length; i++) {
            try {
              window.gameInstance.SendMessage(bridgeGameObjects[i], "CallStaticMethod", 
                JSON.stringify({
                  className: classNameStr,
                  methodName: methodNameStr,
                  arg: argStr
                })
              );
              console.log("Successfully called static method via " + bridgeGameObjects[i]);
              messageSent = true;
              break;
            } catch (err) {
              console.warn("Failed to call static method via " + bridgeGameObjects[i], err);
            }
          }
          
          // If no GameObject found, try with special Unity approach
          if (!messageSent) {
            try {
              // Try to use direct method from unityInstance - sometimes available
              if (window.gameInstance.Module && 
                  typeof window.gameInstance.Module.WebGLBridge === 'object' &&
                  typeof window.gameInstance.Module.WebGLBridge.SetSeedPhraseGlobal === 'function') {
                window.gameInstance.Module.WebGLBridge.SetSeedPhraseGlobal(argStr);
                console.log("Called WebGLBridge.SetSeedPhraseGlobal directly");
              } else {
                console.error("Could not find WebGLBridge in Unity instance");
              }
            } catch (err) {
              console.error("Error calling WebGLBridge directly:", err);
            }
          }
        }
      } else {
        console.warn("Unity instance not available for static method call");
      }
    } catch (error) {
      console.error('Error calling static method:', error);
    }
  },
  
  // Function to list active GameObjects that can receive messages
  // This helps diagnose which GameObject to target
  ListActiveGameObjects: function() {
    try {
      if (window && window.listUnityGameObjects) {
        console.log("Requesting list of active Unity GameObjects");
        window.listUnityGameObjects();
      } else {
        console.warn('listUnityGameObjects function not found on window');
      }
    } catch (error) {
      console.error('Error listing game objects:', error);
    }
  },
  
  // Function to request authentication data from the web app
  RequestAuthData: function() {
    try {
      window.dispatchUnityEvent('requestAuthData');
    } catch (error) {
      console.error("Error requesting auth data from web app:", error);
    }
  },

  // Function to handle logout request
  RequestLogout: function() {
    try {
      window.dispatchUnityEvent('logoutRequested');
    } catch (error) {
      console.error("Error requesting logout from web app:", error);
    }
  },

  // Function to save player data to the web app
  SavePlayerData: function(playerDataJson) {
    try {
      const jsonStr = UTF8ToString(playerDataJson);
      window.dispatchUnityEvent('savePlayerData', jsonStr);
    } catch (error) {
      console.error("Error saving player data to web app:", error);
    }
  }
}); 
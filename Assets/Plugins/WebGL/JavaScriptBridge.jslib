mergeInto(LibraryManager.library, {
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
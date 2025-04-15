mergeInto(LibraryManager.library, {
  // Create a new function with the same name as
  // the event listeners name and make sure the
  // parameters match as well.

      JSDashboardStarts: function () {
            ReactUnityWebGL.DashboardStarts();
      },

      JSSaveScore: function (score) {
            ReactUnityWebGL.SaveScore(score);
      },

      JSSavePlayerConfig: function (json) {
            ReactUnityWebGL.SavePlayerConfig(Pointer_stringify(json));
      },

      JSSavePlayerCharacter: function (nftid) {
            ReactUnityWebGL.SavePlayerCharacter(nftid);
      },

      JSSendMasterData: function (json) {
            ReactUnityWebGL.SendMasterData(Pointer_stringify(json));
      },

      JSSendClientData: function (json) {
            ReactUnityWebGL.SendClientData(Pointer_stringify(json));
      },

      JSGameStarts: function () {
            ReactUnityWebGL.GameStarts();
      },

      JSExitGame: function () {
            ReactUnityWebGL.ExitGame();
      },

      JSSearchGame: function (json) {
            ReactUnityWebGL.SearchGame(Pointer_stringify(json));
      },

      JSCancelGame: function (gameId) {
            ReactUnityWebGL.CancelGame(gameId);
      },

     JSLoginPanel: function (json) {
            ReactUnityWebGL.JSLoginPanel(Pointer_stringify(json));

      },

      JSWalletsLogin: function (walletName) {
            ReactUnityWebGL.JSWalletsLogin(Pointer_stringify(walletName));
      },

       JSAnvilConnect: function() {
          ReactUnityWebGL.JSAnvilConnect();
    },
    
    CheckAnvilConnection: function(anvilIndex) {
        dispatchReactUnityEvent("CheckAnvilConnection", anvilUrl);
    },
    
    JSGetAnvilNfts : function (json) 
    {
       ReactUnityWebGL.JSAnvilConnect(Pointer_stringify(json));
    }
    ,
    
    JSClaimNft: function(Index) {
         ReactUnityWebGL.JSClaimNft(Index);
    }
    ,
    JSClaimAllNft: function (indexArray) {
                ReactUnityWebGL.JSClaimAllNft(Pointer_stringify(indexArray));
    }
    ,
    JSGoToMenu: function () {
                ReactUnityWebGL.JSGoToMenu();
          }

});
namespace CosmicraftsSP {
using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * This is the in-game multiplayer data
 * Saves and manages the network data
 */

public static class GameNetwork
{
    //Game package data (MASTER)
    static NetGamePack GameNetPack;
    //Game package data (CLIENT)
    static NetClientGamePack ClientNetPack;

    //Init game packages
    public static void Start()
    {
        GameNetPack = new NetGamePack();
        ClientNetPack = new NetClientGamePack();
        GameNetPack.LastUpdate = DateTime.Now;
        ClientNetPack.LastUpdate = DateTime.Now;
    }

    //Returns the multiplayer game id (back end)
    public static int GetId()
    {
        return GameNetPack.GameId;
    }

    //Set the multiplayer game id
    public static void SetClientGameId(int gameId)
    {
        ClientNetPack.GameId = gameId;
    }

    //Set the package (master)
    public static void UpdateGameData(string json)
    {
        GameNetPack = JsonHelper.DeserializeObject<NetGamePack>(json);
    }

    //Set the package (client)
    public static void UpdateClientGameData(string json)
    {
        ClientNetPack = JsonHelper.DeserializeObject<NetClientGamePack>(json);
    }

    //Get the package (master)
    public static string GetJsonGameNetPack()
    {
        return JsonHelper.SerializeObject(GameNetPack);
    }

    //Get the package (client)
    public static string GetJsonClientGameNetPack()
    {
        return JsonHelper.SerializeObject(ClientNetPack);
    }

    //Set the status game 
    public static void SetGameStatus(NetGameStep step)
    {
        GameNetPack.GameStep = (int)step;
    }

    //set when the game started
    public static void SetGameStart(DateTime start)
    {
        GameNetPack.GameStart = start;
    }

    //get the game start
    public static DateTime GetStartTime()
    {
        return GameNetPack.GameStart;
    }

    //set when the cient send the last datetime update
    public static void SetClientLastUpdate(DateTime dateTime)
    {
        ClientNetPack.LastUpdate = dateTime;
    }

    //set when the master send the last datetime update
    public static void SetMasterLastUpdate(DateTime dateTime)
    {
        GameNetPack.LastUpdate = dateTime;
    }

    //set the winer
    public static void SetWinner(int GameWinner)
    {
        GameNetPack.GameWinner = GameWinner;
    }

    //get the winer
    public static int GetWinner()
    {
        return GameNetPack.GameWinner;
    }

    //get the game status
    public static NetGameStep GetGameStatus()
    {
        return (NetGameStep)GameNetPack.GameStep;
    }

    //Set the currents units in the game
    public static void SetGameUnits(List<NetUnitPack> netUnitPack)
    {
        GameNetPack.Units = netUnitPack;
    }

    //Get the current units in the game
    public static List<NetUnitPack> GetGameUnits()
    {
        return GameNetPack.Units;
    }

    //Set the deleted units of the game
    public static void SetGameDeletedUnits(List<int> netUnitsDeleted)
    {
        GameNetPack.DeleteIdsUnits = netUnitsDeleted;
    }

    //Get the deleted units of the game
    public static List<int> GetGameUnitsDeleted()
    {
        return GameNetPack.DeleteIdsUnits;
    }

    //Set the requested units from client
    public static void SetRequestedGameUnits(List<NetUnitPack> netUnitPack)
    {
        ClientNetPack.UnitsRequested = netUnitPack;
    }

    //Get the requested units of the game
    public static List<NetUnitPack> GetClientGameUnitsRequested()
    {
        return ClientNetPack.UnitsRequested;
    }

    //Set master game score
    public static void SetMasterScore(int score)
    {
        GameNetPack.MasterScore = score;
    }

    //Set client game score
    public static void SetClientScore(int score)
    {
        ClientNetPack.ClientScore = score;
    }

    //Set the basic master´s data (wallet id and username)
    public static void SetMasterData(string wid, string name)
    {
        GameNetPack.MasterWalletId = wid;
        GameNetPack.MasterPlayerName = name;
    }

    //Set the basic client´s data (wallet id and username)
    public static void SetClientData(string wid, string name)
    {
        GameNetPack.ClientWalletId = wid;
        GameNetPack.ClientPlayerName = name;
    }

    //Get the master wallet id
    public static string GetMasterWalletId()
    {
        return GameNetPack.MasterWalletId;
    }

    //Get the client wallet id
    public static string GetClientWalletId()
    {
        return GameNetPack.ClientWalletId;
    }

    //Returns the master NFT Character
    public static NFTsCharacter GetMasterNFTCharacter()
    {
        return GameNetPack.MasterCharacter;
    }

    //Returns the client NFT Character
    public static NFTsCharacter GetClientNFTCharacter()
    {
        return GameNetPack.ClientCharacter;
    }

    //Gets the master NFTs deck
    public static List<NetCardNft> GetMasterNFTDeck()
    {
        return GameNetPack.MasterDeck;
    }

    //Gets the client NFTs deck
    public static List<NetCardNft> GetClientNFTDeck()
    {
        return GameNetPack.ClientDeck;
    }

    //Get the enemy´s data
    public static UserGeneral GetVsData()
    {
        return GlobalManager.GMD.ImMaster ? 
            new UserGeneral() { 
                NikeName = GameNetPack.ClientPlayerName, 
                WalletId = GameNetPack.ClientWalletId,
                Xp = GameNetPack.ClientXp,
                Level = GameNetPack.ClientLvl,
                Avatar = GameNetPack.ClientAvatar
            } : 
            new UserGeneral() { 
                NikeName = GameNetPack.MasterPlayerName, 
                WalletId = GameNetPack.MasterWalletId,
                Xp = GameNetPack.MasterXp,
                Level = GameNetPack.MasterLvl,
                Avatar = GameNetPack.MasterAvatar
            };
    }

    //Get the VS player nft character
    public static NFTsCharacter GetVSnftCharacter()
    {
        return GlobalManager.GMD.ImMaster ? GameNetPack.ClientCharacter : GameNetPack.MasterCharacter;
    }

    //Get the VS player nfts deck
    public static List<NetCardNft> GetVSnftDeck()
    {
        return GlobalManager.GMD.ImMaster ? GameNetPack.ClientDeck : GameNetPack.MasterDeck;
    }

    //Check if the game lobby is full (ready to begin)
    public static bool GameRoomIsFull()
    {
        return !string.IsNullOrEmpty(GameNetPack.ClientWalletId) && !string.IsNullOrEmpty(GameNetPack.MasterWalletId);
    }

    // Stub implementations that do nothing but log
    public static void JSDashboardStarts() { Debug.Log("JSDashboardStarts - Deprecated"); }
    public static void JSSaveScore(int score) { Debug.Log($"JSSaveScore({score}) - Deprecated"); }
    public static void JSSavePlayerConfig(string json) { Debug.Log("JSSavePlayerConfig - Deprecated"); }
    public static void JSSavePlayerCharacter(int nftid) { Debug.Log($"JSSavePlayerCharacter({nftid}) - Deprecated"); }
    public static void JSSendMasterData(string json) { Debug.Log("JSSendMasterData - Deprecated"); }
    public static void JSSendClientData(string json) { Debug.Log("JSSendClientData - Deprecated"); }
    public static void JSGameStarts() { Debug.Log("JSGameStarts - Deprecated"); }
    public static void JSExitGame() { Debug.Log("JSExitGame - Deprecated"); }
    public static void JSSearchGame(string json) { Debug.Log("JSSearchGame - Deprecated"); }
    public static void JSCancelGame(int gameId) { Debug.Log($"JSCancelGame({gameId}) - Deprecated"); }
    public static void JSLoginPanel(string accountID) { Debug.Log($"JSLoginPanel({accountID}) - Deprecated"); }
    public static void JSWalletsLogin(string walletID) { Debug.Log($"JSWalletsLogin({walletID}) - Deprecated"); }
    public static void JSAnvilConnect() { Debug.Log("JSAnvilConnect - Deprecated"); }
    public static void JSGetAnvilNfts(string anvilUrl) { Debug.Log($"JSGetAnvilNfts({anvilUrl}) - Deprecated"); }
    public static void JSClaimNft(int nftIndex) { Debug.Log($"JSClaimNft({nftIndex}) - Deprecated"); }
    public static void JSClaimAllNft(string indexArray) { Debug.Log($"JSClaimAllNft({indexArray}) - Deprecated"); }
    public static void JSGoToMenu() { Debug.Log("JSGoToMenu - Deprecated"); }
}
}

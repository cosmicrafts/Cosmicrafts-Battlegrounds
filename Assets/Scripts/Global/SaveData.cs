namespace CosmicraftsSP {
using UnityEngine;
/*
 * This is the local data controller
 * Is used to save information, like the game configuration, on the device
 */
public static class SaveData 
{
    //The path of the game configuration file
    public readonly static string fullPathConfig = Application.persistentDataPath + "/" + "Config";

    public static bool error = false;
    //Load the configuration data

}
}
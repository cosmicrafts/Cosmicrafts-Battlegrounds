/*
 * Here we save the game configuration
 */

//Enum Login Types
public enum SocialLogs
{
    None,
    Facebook,
    Twitch
}

//Game configuration structure (will be encrypted)
[System.Serializable]
public class Config
{
    public int language = 0;

    public string UserName = string.Empty;

    //public string Password = string.Empty;

    public bool AutoLog = true;

    public SocialLogs TypeLog = SocialLogs.None;    
}

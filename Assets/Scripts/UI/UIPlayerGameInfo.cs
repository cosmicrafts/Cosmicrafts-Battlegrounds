using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cosmicrafts;
using Cosmicrafts.backend.Models;

/*
 * This code shows the player data on the in-game UI player banner
 */

public class UIPlayerGameInfo : MonoBehaviour
{
    //Player UI stats and icons references
    public TMP_Text PlayerName;
    public TMP_Text WalletId;
    public TMP_Text Level;
    public Image XpBar;
    public Image Avatar;

    private string FormatPrincipalId(string principalId)
    {
        if (string.IsNullOrEmpty(principalId) || principalId.Length < 8)
            return principalId;

        string firstPart = principalId.Substring(0, 5);
        string lastPart = principalId.Substring(principalId.Length - 3);
        return $"{firstPart}...{lastPart}";
    }

    private void OnEnable()
    {
        // Subscribe to player data events
        if (ICPService.Instance != null)
        {
            ICPService.Instance.OnPlayerDataReceived += UpdatePlayerInfo;
            
            // If we already have player data, update the UI
            if (ICPService.Instance.CurrentPlayer != null)
            {
                UpdatePlayerInfo(ICPService.Instance.CurrentPlayer);
            }
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        if (ICPService.Instance != null)
        {
            ICPService.Instance.OnPlayerDataReceived -= UpdatePlayerInfo;
        }
    }

    private void UpdatePlayerInfo(Cosmicrafts.backend.Models.Player player)
    {
        if (player == null) return;

        // Update username
        if (PlayerName != null)
        {
            PlayerName.text = player.Username;
        }

        // Update wallet ID (principal ID) with shortened format
        if (WalletId != null)
        {
            WalletId.text = FormatPrincipalId(ICPService.Instance.PrincipalId);
        }

        // Update level
        if (Level != null)
        {
            Level.text = $"{player.Level}";
        }

        // Update avatar if needed
        if (Avatar != null && player.Avatar != null)
        {
            // You'll need to implement avatar loading based on your avatar system
            // For example:
            // Avatar.sprite = ResourcesServices.LoadAvatarUser(player.Avatar.ToString());
        }
    }

    //Update the UI banner with a resume of some player data (for multiplayer)
    public void InitInfo(UserGeneral user)
    {
       // WalletId.text = Utils.GetWalletIDShort(user.WalletId);
       // PlayerName.text = user.NikeName;
       // Level.text = $"{Lang.GetText("mn_lvl")} {user.Level}";
        //XpBar.fillAmount = (float)user.Xp / (float)user.GetNextXpGoal();
        //Avatar.sprite = ResourcesServices.LoadAvatarUser(user.Avatar);
        //Avatar.sprite = ResourcesServices.LoadAvatarIcon(user.Avatar);
    }
}

namespace Cosmicrafts
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;
    using System.Collections;

    /*
     * This is the in-game UI controller
     * Contains the UI references and functions for update them
     * Only controls the game's data, the UI for the players and the tutorial are in other scripts
     */
    public class UIGameMng : MonoBehaviour
    {
        //Screens objects references
        public GameObject VictoryScreen;
        public GameObject DefeatScreen;
        public GameObject ResultsScreen;

        //UI modules objects references
        public GameObject TopMidInfo;
        public GameObject DeckPanel;

        //Cards objects references
        public UIGameCard[] UIDeck = new UIGameCard[8];

        //Time, energy number and energy bar references
        public TMP_Text TimeOut;
        public TMP_Text EnergyLabel;
        public Image EnergyBar;

        //Trigger grid for deploy cards
        public GameObject AreaDeploy;

        //Results Metrics text references
        public TMP_Text MTxtEnergyUsed;
        public TMP_Text MTxtEnergyGenerated;
        public TMP_Text MTxtEnergyWasted;
        public TMP_Text MTxtEnergyChargeRatePerSec;
        public TMP_Text MTxtXpEarned;
        public TMP_Text MTxtDamage;
        public TMP_Text MTxtDeploys;
        public TMP_Text MTxtKills;
        public TMP_Text MTxtSecRemaining;
        public TMP_Text MTxtScore;

        //Players panels references
        public UIPlayerGameInfo[] Players = new UIPlayerGameInfo[2];

        //HP and Shield Colors for every team
        Color FriendHpBarColor;
        Color FriendShieldBarColor;

        Color EnemyHpBarColor;
        Color EnemyShieldBarColor;
        
        // Keep track of whether player UI has been initialized
        private bool playerUIInitialized = false;

        // Respawn UI elements
        [Header("Respawn UI")]
        public GameObject respawnPanel;
        public TMP_Text respawnCountdownText;
        public Button respawnButton;
        private Coroutine countdownCoroutine;

        private void Awake()
        {
            // Set the UI controller
            GameMng.UI = this;

            // Init the HP and shield colors
            FriendHpBarColor = new Color(0.25f, 1f, 0.28f, 1f);
            FriendShieldBarColor = new Color(0.25f, 0.66f, 1f, 1f);
            EnemyHpBarColor = new Color(1f, 0.25f, 0.25f, 1f);
            EnemyShieldBarColor = new Color(1f, 0.8f, 0.25f, 1f);
        }

        private void Start()
        {
            // Start a coroutine to wait for GameMng.P to be set
            StartCoroutine(WaitForPlayerInitialization());
        }
        
        // Wait for player to be initialized before setting up player UI
        private IEnumerator WaitForPlayerInitialization()
        {
            // Wait until GameMng.P is available (check every 0.1 seconds)
            float waitTime = 5f; // Maximum wait time in seconds
            float elapsedTime = 0f;
            
            while (GameMng.P == null && elapsedTime < waitTime)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;
            }
            
            // Initialize player UI only if GameMng.P is available
            if (GameMng.P != null)
            {
                InitializePlayerUI();
            }
            else
            {
                Debug.LogWarning("Could not initialize player UI: GameMng.P is still null after waiting.");
                // Use default player data as fallback
                InitializeDefaultPlayerUI();
            }
        }
        
        // Initialize player UI with data from GameMng.P
        public void InitializePlayerUI()
        {
            if (playerUIInitialized || GameMng.P == null || Players == null || Players.Length == 0)
                return;
            
            // Ensure PlayerId is valid for array index
            int playerIndex = Mathf.Clamp(GameMng.P.ID - 1, 0, Players.Length - 1);
            
            // Hardcoded player data for now
            UserGeneral hardcodedPlayerData = new UserGeneral
            {
                NikeName = "Player1",
                WalletId = "Wallet1",
                Level = 10,
                Xp = 2000,
                Avatar = 1
            };

            UserProgress hardcodedPlayerProgress = new UserProgress
            {
                // Fill with appropriate hardcoded values
            };

            NFTsCharacter hardcodedPlayerCharacter = new NFTsCharacter
            {
                // Fill with appropriate hardcoded values
            };

            // Init the UI info of the player with hardcoded values
            if (Players[playerIndex] != null)
            {
                Players[playerIndex].InitInfo(hardcodedPlayerData, hardcodedPlayerProgress, hardcodedPlayerCharacter);
                playerUIInitialized = true;
            }
        }
        
        // Fallback initialization with default values
        private void InitializeDefaultPlayerUI()
        {
            if (playerUIInitialized || Players == null || Players.Length == 0)
                return;
                
            // Hardcoded player data as fallback
            UserGeneral hardcodedPlayerData = new UserGeneral
            {
                NikeName = "DefaultPlayer",
                WalletId = "DefaultWallet",
                Level = 1,
                Xp = 0,
                Avatar = 1
            };

            UserProgress hardcodedPlayerProgress = new UserProgress
            {
                // Default values
            };

            NFTsCharacter hardcodedPlayerCharacter = new NFTsCharacter
            {
                // Default values
            };

            // Init with default values for player index 0
            if (Players[0] != null)
            {
                Players[0].InitInfo(hardcodedPlayerData, hardcodedPlayerProgress, hardcodedPlayerCharacter);
                playerUIInitialized = true;
            }
        }

        // Shows the game over screen
        public void SetGameOver(Team winner)
        {
            TopMidInfo.SetActive(false);
            DeckPanel.SetActive(false);

            ResultsScreen.SetActive(true);
            if (GameMng.P != null && FactionManager.ConvertFactionToTeam(GameMng.P.MyFaction) == winner)
            {
                VictoryScreen.SetActive(true);
            }
            else
            {
                DefeatScreen.SetActive(true);
            }

            GameMng.MT.CalculateLastMetrics(winner);
            UpdateResults();
        }

        // Init the UI Cards
        public void InitGameCards(NFTsCard[] nftCard)
        {
            if (UIDeck == null || nftCard == null)
            {
                Debug.LogWarning("Cannot initialize game cards: UIDeck or nftCard is null");
                return;
            }
            
            int maxCards = Mathf.Min(UIDeck.Length, nftCard.Length);
            for (int i = 0; i < maxCards; i++)
            {
                if (UIDeck[i] != null && nftCard[i] != null)
                {
                    UIDeck[i].IdCardDeck = i; // Ensure index is set correctly
                    UIDeck[i].SpIcon.sprite = nftCard[i].IconSprite;
                    UIDeck[i].EnergyCost = nftCard[i].EnergyCost;
                    UIDeck[i].TextCost.text = nftCard[i].EnergyCost.ToString();
                }
            }
        }

        // Update the UI time
        public void UpdateTimeOut(string newtime)
        {
            if (TimeOut != null)
                TimeOut.text = newtime;
        }

        // Shows a card as selected
        public void SelectCard(int idc)
        {
            if (UIDeck == null || idc < 0 || idc >= UIDeck.Length || UIDeck[idc] == null)
                return;
                
            UIDeck[idc].SetSelection(true);
            
            if (AreaDeploy != null)
                AreaDeploy.SetActive(true);
        }

        // Deselects all cards
        public void DeselectCards()
        {
            if (UIDeck == null) return;
            
            foreach (UIGameCard card in UIDeck)
            {
                if (card != null)
                    card.SetSelection(false);
            }
            
            if (AreaDeploy != null)
                AreaDeploy.SetActive(false);
        }

        // Update the energy bar and text
        public void UpdateEnergy(float energy, float max)
        {
            if (EnergyLabel != null)
            {
                EnergyLabel.text = ((int)energy).ToString(energy == max ? "F0" : "F0");
            }
            
            if (EnergyBar != null)
            {
                EnergyBar.fillAmount = energy / max;
            }

            if (UIDeck != null)
            {
                foreach (UIGameCard card in UIDeck)
                {
                    if (card != null && card.TextCost != null)
                    {
                        card.TextCost.color = energy >= card.EnergyCost ? Color.white : Color.red;
                    }
                }
            }
        }

        // Update the results text panel with the game metrics
        public void UpdateResults()
        {
            if (GameMng.MT == null) return;
            
            if (MTxtEnergyUsed != null) MTxtEnergyUsed.text = GameMng.MT.GetEnergyUsed().ToString();
            if (MTxtEnergyGenerated != null) MTxtEnergyGenerated.text = GameMng.MT.GetEnergyGenerated().ToString("F0");
            if (MTxtEnergyWasted != null) MTxtEnergyWasted.text = GameMng.MT.GetEnergyWasted().ToString("F0");
            if (MTxtEnergyChargeRatePerSec != null) MTxtEnergyChargeRatePerSec.text = GameMng.MT.GetEnergyChargeRatePerSec().ToString() + "/s";

            if (MTxtXpEarned != null) MTxtXpEarned.text = "+" + GameMng.MT.GetScore().ToString();

            if (MTxtDamage != null) MTxtDamage.text = GameMng.MT.GetDamage().ToString();
            if (MTxtKills != null) MTxtKills.text = GameMng.MT.GetKills().ToString();
            if (MTxtDeploys != null) MTxtDeploys.text = GameMng.MT.GetDeploys().ToString();
            if (MTxtSecRemaining != null) MTxtSecRemaining.text = GameMng.MT.GetSecRemaining().ToString() + " s";

            if (MTxtScore != null) MTxtScore.text = GameMng.MT.GetScore().ToString();
        }

        // Returns the HP color for a unit
        public Color GetHpBarColor(bool isEnnemy)
        {
            return isEnnemy ? EnemyHpBarColor : FriendHpBarColor;
        }

        // Returns the Shield color for a unit
        public Color GetShieldBarColor(bool isEnnemy)
        {
            return isEnnemy ? EnemyShieldBarColor : FriendShieldBarColor;
        }

        // Shows the respawn UI with countdown
        public void ShowRespawnCountdown(float respawnTime)
        {
            if (respawnPanel != null)
            {
                respawnPanel.SetActive(true);
                
                // Set up the respawn button
                if (respawnButton != null)
                {
                    // Find player unit to connect the button
                    if (GameMng.P != null)
                    {
                        Unit playerUnit = GameMng.P.GetComponent<Unit>();
                        if (playerUnit != null)
                        {
                            // Clear previous listeners to avoid duplicates
                            respawnButton.onClick.RemoveAllListeners();
                            respawnButton.onClick.AddListener(playerUnit.TriggerRespawn);
                        }
                    }
                }
                
                // Start countdown
                if (countdownCoroutine != null)
                {
                    StopCoroutine(countdownCoroutine);
                }
                countdownCoroutine = StartCoroutine(UpdateRespawnCountdown(respawnTime));
            }
        }
        
        // Hide respawn UI
        public void HideRespawnUI()
        {
            if (respawnPanel != null)
            {
                respawnPanel.SetActive(false);
            }
            
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }
        }
        
        // Coroutine to update countdown text
        private IEnumerator UpdateRespawnCountdown(float totalTime)
        {
            float remainingTime = totalTime;
            
            while (remainingTime > 0)
            {
                if (respawnCountdownText != null)
                {
                    int seconds = Mathf.CeilToInt(remainingTime);
                    respawnCountdownText.text = $"Respawn in: {seconds}s";
                }
                
                yield return new WaitForSeconds(0.1f);
                remainingTime -= 0.1f;
            }
            
            if (respawnCountdownText != null)
            {
                respawnCountdownText.text = "Ready to Respawn";
            }
        }
    }
}

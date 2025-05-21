namespace Cosmicrafts
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.InputSystem;
    using System.Collections.Generic;

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
        
        // Currently selected card index (0-7) or -1 if none
        private int selectedCardIndex = -1;
        
        // Gameplay state
        private bool isGameOver = false;

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
            // The UIPlayerGameInfo component will automatically update when ICPService data is available
            
            // Register for primary action input
            InputManager.SubscribeToPrimaryAction(OnPrimaryAction);
            
            // Make sure all cards are deselected at start
            DeselectCards();
        }
        
        private void OnDestroy()
        {
            // Unregister input callbacks
            InputManager.UnsubscribeFromPrimaryAction(OnPrimaryAction);
        }
        
        private void Update()
        {
            // The UIGameCard instances handle number key presses through callbacks
            // and then call UIGameMng.DeployCard or UIGameMng.SelectCard as needed
        }
        
        private void OnPrimaryAction(InputAction.CallbackContext context)
        {
            if (!context.performed || isGameOver || selectedCardIndex < 0)
                return;
                
            // Use auto-deployment when a card is selected and the primary action is performed
            if (selectedCardIndex >= 0)
            {
                // Use the UI-specific pointer position method to avoid conflicts with joystick
                Vector2 pointerPos = InputManager.GetUIPointerPosition();
                Ray ray = Camera.main.ScreenPointToRay(pointerPos);
                RaycastHit hit;
                
                // Try to hit anything in the game world
                if (Physics.Raycast(ray, out hit, 100f))
                {
                    // Deploy the card at the hit position
                    DeployCard(selectedCardIndex, hit.point);
                }
                else
                {
                    // If we don't hit anything, just auto-deploy
                    DeployCard(selectedCardIndex, Vector3.zero);
                }
                
                // Deselect after deployment
                DeselectCards();
            }
        }

        // Shows the game over screen
        public void SetGameOver(Team winner)
        {
            isGameOver = true;
            TopMidInfo.SetActive(false);
            DeckPanel.SetActive(false);

            ResultsScreen.SetActive(true);
            if (winner == GameMng.P.MyTeam)
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
            for (int i = 0; i < nftCard.Length; i++)
            {
                UIDeck[i].SpIcon.sprite = nftCard[i].IconSprite;
                UIDeck[i].EnergyCost = nftCard[i].EnergyCost;
                UIDeck[i].TextCost.text = nftCard[i].EnergyCost.ToString();
            }
        }

        // Update the UI time
        public void UpdateTimeOut(string newtime)
        {
            TimeOut.text = newtime;
        }

        // Shows a card as selected
        public void SelectCard(int idc)
        {
            // Deselect any currently selected card
            DeselectCards();
            
            // Make sure the index is valid
            if (idc >= 0 && idc < UIDeck.Length)
            {
                UIDeck[idc].SetSelection(true);
                selectedCardIndex = idc;
                Debug.Log($"Card {idc} selected");
            }
        }

        // Deselects all cards
        public void DeselectCards()
        {
            foreach (UIGameCard card in UIDeck)
            {
                if (card != null)
                {
                    card.SetSelection(false);
                }
            }
            selectedCardIndex = -1;
        }
        
        // Deploy the selected card at a position
        public void DeployCard(int cardIndex, Vector3 position)
        {
            if (cardIndex < 0 || cardIndex >= UIDeck.Length || isGameOver)
                return;
                
            UIGameCard card = UIDeck[cardIndex];
            float currentEnergy = 0f;
            float maxEnergy = 100f; // Default max energy
            
            // Get actual energy from Player
            if (GameMng.P != null)
            {
                currentEnergy = GameMng.P.CurrentEnergy;
                maxEnergy = GameMng.P.MaxEnergy;
            }
            else
            {
                // Access energy through GameMng.MT if Player not available
                if (GameMng.MT != null)
                {
                    currentEnergy = GameMng.MT.GetEnergyWasted();
                }
            }
            
            int energyInt = Mathf.FloorToInt(currentEnergy);
            
            if (energyInt >= card.EnergyCost)
            {
                // When position is Vector3.zero, it means we want to auto-deploy
                if (GameMng.P != null)
                {
                    // Get the card from the player's deck if available
                    List<NFTsCard> playerDeck = GameMng.P.PlayerDeck;
                    if (playerDeck != null && cardIndex < playerDeck.Count)
                    {
                        NFTsCard nftCard = playerDeck[cardIndex];
                        
                        // Pass the position - if zero, Player.DeplyUnit will use random spawn point
                        GameMng.P.DeplyUnit(nftCard, position);
                        
                        // Update energy UI - handled by Player.RestEnergy, but update here to be safe
                        UpdateEnergy(GameMng.P.CurrentEnergy, GameMng.P.MaxEnergy);
                        
                        // Always deselect after deployment
                        DeselectCards();
                        return;
                    }
                }
                
                // Fallback legacy deployment logic (we shouldn't reach here normally)
                if (GameMng.P != null)
                {
                    // Subtract energy through the Player
                    GameMng.P.RestEnergy(card.EnergyCost);
                    currentEnergy = GameMng.P.CurrentEnergy;
                }
                else
                {
                    // Subtract energy directly if Player not available
                    currentEnergy -= card.EnergyCost;
                }
                
                // Update energy UI
                UpdateEnergy(currentEnergy, maxEnergy);
                
                // Add to metrics
                GameMng.MT.AddDeploys(1);
                
                // Always deselect after deployment
                DeselectCards();
            }
            else
            {
                Debug.Log($"Not enough energy to deploy card {cardIndex}. Need {card.EnergyCost}, have {energyInt}");
            }
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

            foreach (UIGameCard card in UIDeck)
            {
                if (card != null)
                {
                    card.TextCost.color = energy >= card.EnergyCost ? Color.white : Color.red;
                }
            }
        }

        // Update the results text panel with the game metrics 
        public void UpdateResults()
        {
            MTxtEnergyUsed.text = GameMng.MT.GetEnergyUsed().ToString();
            MTxtEnergyGenerated.text = GameMng.MT.GetEnergyGenerated().ToString("F0");
            MTxtEnergyWasted.text = GameMng.MT.GetEnergyWasted().ToString("F0");
            MTxtEnergyChargeRatePerSec.text = GameMng.MT.GetEnergyChargeRatePerSec().ToString() + "/s";

            MTxtXpEarned.text = "+" + GameMng.MT.GetScore().ToString();

            MTxtDamage.text = GameMng.MT.GetDamage().ToString();
            MTxtKills.text = GameMng.MT.GetKills().ToString();
            MTxtDeploys.text = GameMng.MT.GetDeploys().ToString();
            MTxtSecRemaining.text = GameMng.MT.GetSecRemaining().ToString() + " s";

            MTxtScore.text = GameMng.MT.GetScore().ToString();
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
    }
}

namespace Cosmicrafts
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.InputSystem;
    using System.Collections.Generic;
    using System;
    using System.Collections;

    /*
     * This is the in-game UI controller
     * Contains the UI references and functions for update them
     * Only controls the game's data, the UI for the players and the tutorial are in other scripts
     */
    public class UIGameMng : MonoBehaviour
    {
        // Event for XP updates
        public event Action<int, int, int> OnXPUpdated;

        //Screens objects references
        public GameObject VictoryScreen;
        public GameObject DefeatScreen;
        public GameObject ResultsScreen;

        //UI modules objects references
        public GameObject TopMidInfo;
        public GameObject DeckPanel;

        //Cards objects references
        public UIGameCard[] UIDeck = new UIGameCard[8];

        [Header("Energy System")]
        public TMP_Text EnergyLabel;
        public Image EnergyBar;
        public Image EnergyBarGhost;
        [SerializeField] private float energyAnimationSpeed = 10f;

        [Header("XP System UI")]
        public TMP_Text LevelLabel;
        public TMP_Text XPLabel;
        public Image XPBarFill;  // Main XP bar (replaces Slider)
        public Image XPBarGhost; // Ghost/Incremental bar

        [Header("Unit Counter")]
        public TMP_Text UnitCounterLabel; // New field for unit counter

        [Header("Warning System")]
        public GameObject Warning; // Warning message object with animation
        [SerializeField] private GameObject lowHealthWarning; // Red pulsing warning for low health
        [TextArea(1, 3)]
        public string UnitLimitWarningText = "Unit Limit Reached!";
        [TextArea(1, 3)]
        public string NotEnoughEnergyWarningText = "Not Enough Energy!";

        //XP Bar Animation
        [Header("XP Bar Animation")]
        [SerializeField] private float xpAnimationSpeed = 5f; // Default speed, adjust in inspector
        private float currentFill = 0f; // Current fill bar value
        private float targetFill = 0f;  // Target value for fill bar
        private int currentLevel = 1;   // Track current level for level up detection

        //XP Bar Colors
        [Header("XP Bar Colors")]
        public Color XPBarFillColor = new Color(0.25f, 0.66f, 1f, 1f);
        public Color XPBarGhostColor = new Color(0.25f, 0.66f, 1f, 0.5f);

        [Header("XP Gain Display")]
        [SerializeField] private GameObject xpGainPrefab; // Assign the CanvasDamage prefab in inspector

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

            // Initialize XP UI if player exists
            if (GameMng.P != null)
            {
                Debug.Log("[UIGameMng] Initializing XP UI");
                InitXP(GameMng.P.MaxXP);
                UpdateXP(GameMng.P.CurrentXP, GameMng.P.MaxXP, GameMng.P.PlayerLevel);
            }
            else
            {
                Debug.LogWarning("[UIGameMng] GameMng.P is null! Cannot initialize XP UI");
            }
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
            // Don't allow card deployment if game is over or player is dead
            if (cardIndex < 0 || cardIndex >= UIDeck.Length || isGameOver || 
                (GameMng.P != null && !GameMng.P.IsAlive))
            {
                Debug.Log($"Cannot deploy card: GameOver={isGameOver}, PlayerAlive={(GameMng.P != null ? GameMng.P.IsAlive : false)}");
                return;
            }
                
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
                        
                        // Store current unit count before deployment
                        int currentUnits = GameMng.P.GetActiveUnitsCount();
                        int maxUnits = GameMng.P.maxActiveUnits;
                        
                        // Try to deploy the unit
                        GameMng.P.DeplyUnit(nftCard, position);
                        
                        // If unit count didn't change and it's a ship card, show unit limit warning
                        if (nftCard.EntType == (int)NFTClass.Ship && 
                            GameMng.P.GetActiveUnitsCount() == currentUnits && 
                            currentUnits >= maxUnits)
                        {
                            ShowUnitLimitWarning();
                        }
                        
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
                // Show not enough energy warning
                ShowNotEnoughEnergyWarning();
                Debug.Log($"Not enough energy to deploy card {cardIndex}. Need {card.EnergyCost}, have {energyInt}");
            }
        }

        private void LateUpdate()
        {
            // Animate Fill Bar to catch up to Ghost
            if (XPBarFill != null && XPBarGhost != null)
            {
                // Smoothly animate fill towards target
                currentFill = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * xpAnimationSpeed);
                
                // Update fill bar to show progress
                XPBarFill.fillAmount = currentFill;
            }

            // Animate Energy Bar Ghost
            if (EnergyBar != null && EnergyBarGhost != null)
            {
                // Smoothly animate ghost towards current fill
                EnergyBarGhost.fillAmount = Mathf.Lerp(EnergyBarGhost.fillAmount, EnergyBar.fillAmount, Time.deltaTime * energyAnimationSpeed);
            }
        }

        // Update the energy bar and text
        public void UpdateEnergy(float energy, float max)
        {
            if (EnergyLabel != null)
            {
                EnergyLabel.text = ((int)energy).ToString();
            }
            
            if (EnergyBar != null)
            {
                // Get energy values directly from Player
                if (GameMng.P != null)
                {
                    // Set fill amount immediately for main bar
                    EnergyBar.fillAmount = energy / max;
                }
            }

            // Update card costs colors based on current energy
            foreach (UIGameCard card in UIDeck)
            {
                if (card != null)
                {
                    card.TextCost.color = energy >= card.EnergyCost ? Color.white : Color.red;
                }
            }
        }

        // Update UI elements based on player state
        public void UpdatePlayerState(bool isAlive)
        {
            // Debug.Log($"[UIGameMng] Updating UI for player state: {(isAlive ? "Alive" : "Dead")}");
            
            // Update deck visibility
            if (DeckPanel != null)
            {
                DeckPanel.SetActive(true); // Always keep deck panel visible
            }
            
            // Update energy UI
            if (EnergyBar != null)
            {
                EnergyBar.gameObject.SetActive(isAlive);
            }
            if (EnergyLabel != null)
            {
                EnergyLabel.gameObject.SetActive(isAlive);
            }
            
            // Update unit counter visibility
            if (UnitCounterLabel != null)
            {
                UnitCounterLabel.gameObject.SetActive(isAlive);
            }

            // Hide warning when player dies
            if (Warning != null)
            {
                Warning.SetActive(false);
            }
            
            // Hide low health warning when player dies
            ShowLowHealthWarning(false);
            
            // Update card states and interactions
            foreach (UIGameCard card in UIDeck)
            {
                if (card != null)
                {
                    // Keep cards visible but disable interactions when dead
                    card.gameObject.SetActive(true);
                    
                    // Disable card interactions when dead
                    if (!isAlive)
                    {
                        // Deselect any selected card
                        DeselectCards();
                        // Set card to appear disabled (grayed out)
                        card.SetSelection(false);
                    }
                }
            }
        }

        // Initialize XP bars - exactly like UIUnit.Init()
        public void InitXP(int maxXP)
        {
            if (XPBarFill != null && XPBarGhost != null)
            {
                currentFill = 0f;
                targetFill = 0f;
                XPBarFill.fillAmount = 0f;
                XPBarGhost.fillAmount = 0f;
            }
        }

        public void UpdateXP(int currentXP, int maxXP, int level)
        {
            if (LevelLabel != null)
            {
                LevelLabel.text = $"{level}";
            }
            
            if (XPLabel != null)
            {
                XPLabel.text = $"{currentXP}/{maxXP} XP";
            }
            
            if (XPBarFill != null && XPBarGhost != null)
            {
                // Calculate the new target value
                float newTarget = (float)currentXP / maxXP;
                
                // Check if we leveled up
                bool isLevelUp = level > currentLevel;
                currentLevel = level;
                
                if (isLevelUp)
                {
                    // On level up, set ghost to 0 and fill to current
                    XPBarGhost.fillAmount = 0f;
                    currentFill = 0f;
                    
                    // Update bot levels when player levels up
                    if (GameMng.GM != null)
                    {
                        BotSpawner botSpawner = FindFirstObjectByType<BotSpawner>();
                        if (botSpawner != null)
                        {
                            botSpawner.UpdateBotLevels();
                        }
                    }
                }
                
                // Instantly set ghost bar to new value
                XPBarGhost.fillAmount = newTarget;
                
                // Set the target for fill bar to animate to
                targetFill = newTarget;
                
                // Set colors
                XPBarFill.color = XPBarFillColor;
                XPBarGhost.color = XPBarGhostColor;
            }

            // Notify subscribers of XP update
            OnXPUpdated?.Invoke(currentXP, maxXP, level);
        }

        // Call this when XP is gained from destroying a unit
        public void ShowXPGain(float xpAmount, Vector3 position)
        {
            if (xpGainPrefab != null)
            {
                // Position the XP gain number above the target
                GameObject xpGainObj = Instantiate(xpGainPrefab, position, Quaternion.identity);
                
                // Make sure it's not parented to the unit
                xpGainObj.transform.SetParent(null);
                
                CanvasDamage xpGain = xpGainObj.GetComponent<CanvasDamage>();
                if (xpGain != null)
                {
                    // Set the XP gain text and colors - exactly like Projectile.cs
                    xpGain.SetDamage(xpAmount, false, false, true); // Set xpGain to true
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

        // Add new method to update unit counter
        public void UpdateUnitCounter(int activeUnits, int maxUnits)
        {
            if (UnitCounterLabel != null)
            {
                UnitCounterLabel.text = $"{activeUnits}/{maxUnits} Units";
            }
        }

        // Add simple method to show unit limit warning
        public void ShowUnitLimitWarning()
        {
            if (Warning != null)
            {
                // Set the warning text
                TMP_Text warningText = Warning.GetComponent<TMP_Text>();
                if (warningText != null)
                {
                    warningText.text = UnitLimitWarningText;
                }

                // Force the warning to be active
                Warning.SetActive(true);

                // Get the animation component
                Animation anim = Warning.GetComponent<Animation>();
                if (anim != null)
                {
                    // Force stop any existing animation
                    anim.Stop();
                    // Force play the animation from the beginning
                    anim.Play("Warning");
                }
                else
                {
                    Debug.LogError("Warning GameObject is missing Animation component!");
                }
            }
            else
            {
                Debug.LogError("Warning GameObject reference is missing in UIGameMng!");
            }
        }

        // Add method to show not enough energy warning
        public void ShowNotEnoughEnergyWarning()
        {
            if (Warning != null)
            {
                // Set the warning text
                TMP_Text warningText = Warning.GetComponent<TMP_Text>();
                if (warningText != null)
                {
                    warningText.text = NotEnoughEnergyWarningText;
                }

                // If already active, disable and enable to restart animation
                if (Warning.activeSelf)
                {
                    Warning.SetActive(false);
                }
                Warning.SetActive(true);
            }
        }

        public void ShowLowHealthWarning(bool show)
        {
            if (lowHealthWarning != null)
            {
                lowHealthWarning.SetActive(show);
            }
        }
    }
}

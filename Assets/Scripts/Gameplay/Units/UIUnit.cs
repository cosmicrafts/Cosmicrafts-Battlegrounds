namespace Cosmicrafts
{
    using UnityEngine;
    using TMPro;
    using UnityEngine.UI;

    /*
     * This script manages the UI of the ships and stations
     * Shows HP Bars, Shields, etc
     */
    public class UIUnit : MonoBehaviour
    {
        // The main game object canvas reference
        public GameObject Canvas;

        // HP bar image
        public Image Hp;
        public Image GHp;

        // Shield bar image
        public Image Shield;
        public Image GShield;

        public float DifDmgSpeed = 10f;

        // Reference to the TMP text to display the level
        public TextMeshProUGUI LevelText;

        // Reference to the Animation component
        public Animation Animation;

        // UI positioning
        [Header("UI Positioning")]
        [SerializeField] private Vector3 uiOffset = Vector3.up * 0.5f; // Smaller offset for isometric
        private Transform parentTransform;
        private Camera mainCamera;

        float GhostHp;
        float GhostSH;

        // Variables to store previous HP state for comparison
        private float previousHp;
        private float previousShield;

        // Variables to track damage state
        private bool isCurrentlyTakingDamage = false;
        private float damageStartTime = 0f;
        private const float damageResetTime = 1f; // Time before considering it a new damage sequence

        // Player 1 Colors
        [Header("Player 1 Colors")]
        public Color Player1HpColor = Color.red;
        public Color Player1ShieldColor = Color.magenta;
        public Color Player1DifHpColor = Color.yellow;
        public Color Player1DifShieldColor = Color.cyan;

        // Player 2 Colors
        [Header("Player 2 Colors")]
        public Color Player2HpColor = Color.blue;
        public Color Player2ShieldColor = Color.cyan;
        public Color Player2DifHpColor = Color.green;
        public Color Player2DifShieldColor = Color.white;

        [Header("UI Scaling")]
        [SerializeField] private float baseOrthographicSize = 60f; // Match this with CameraController's defaultZoom
        [SerializeField] private float minScaleMultiplier = 0.5f;
        [SerializeField] private float maxScaleMultiplier = 2f;
        [SerializeField] private bool maintainConstantScale = true;
        
        private Vector3 originalScale;

        void Awake()
        {
            // Cache references for performance
            parentTransform = transform.parent;
            mainCamera = Camera.main;
            
            // Store the original scale from the prefab
            originalScale = transform.localScale;
        }

        void Start()
        {
            // Get the Unit component
            Unit unit = GetComponentInParent<Unit>();

            // Set team-specific colors
            if (unit != null)
            {
                if (unit.MyTeam == Team.Blue)
                {
                    // Set Player 2 (Blue team) colors
                    Hp.color = Player2HpColor;
                    Shield.color = Player2ShieldColor;
                    GHp.color = Player2DifHpColor;
                    GShield.color = Player2DifShieldColor;
                }
                else if (unit.MyTeam == Team.Red)
                {
                    // Set Player 1 (Red team) colors
                    Hp.color = Player1HpColor;
                    Shield.color = Player1ShieldColor;
                    GHp.color = Player1DifHpColor;
                    GShield.color = Player1DifShieldColor;
                }

                // Set the level text
                if (LevelText != null)
                {
                    LevelText.text = unit.GetLevel().ToString();
                }
            }

            // Initialize previousHp and previousShield with current values
            previousHp = Hp.fillAmount;
            previousShield = Shield.fillAmount;
        }

        private void LateUpdate()
        {
            if (!mainCamera || !parentTransform) return;

            // Update position
            transform.position = parentTransform.position + uiOffset;
            
            // Match camera rotation
            transform.rotation = mainCamera.transform.rotation;

            // Scale UI based on camera zoom
            if (maintainConstantScale && mainCamera.orthographic)
            {
                float currentOrthographicSize = mainCamera.orthographicSize;
                float scaleFactor = currentOrthographicSize / baseOrthographicSize;
                
                // Clamp the scale multiplier
                scaleFactor = Mathf.Clamp(scaleFactor, minScaleMultiplier, maxScaleMultiplier);
                
                // Apply the new scale while preserving the original proportions
                transform.localScale = Vector3.Scale(originalScale, Vector3.one * scaleFactor);
            }

            // Check for damage or healing
            bool isDamaged = (Hp.fillAmount < previousHp || Shield.fillAmount < previousShield);
            bool isHealing = (Hp.fillAmount > previousHp || Shield.fillAmount > previousShield);

            if (isDamaged)
            {
                // If we're not already in a damage state, trigger the animation
                if (!isCurrentlyTakingDamage)
                {
                    OnDamageTaken();
                    isCurrentlyTakingDamage = true;
                    damageStartTime = Time.time;
                }
                // Update the damage start time if we're still taking damage
                else
                {
                    damageStartTime = Time.time;
                }
            }
            else if (isHealing || Time.time > damageStartTime + damageResetTime)
            {
                // Reset the damage state if we're healing or enough time has passed
                isCurrentlyTakingDamage = false;
            }

            // Lerp Ghost Bars
            GhostHp = Mathf.Lerp(GhostHp, Hp.fillAmount, Time.deltaTime * DifDmgSpeed);
            GhostSH = Mathf.Lerp(GhostSH, Shield.fillAmount, Time.deltaTime * DifDmgSpeed);
            GHp.fillAmount = GhostHp;
            GShield.fillAmount = GhostSH;

            // Update previous state for the next frame
            previousHp = Hp.fillAmount;
            previousShield = Shield.fillAmount;
        }

        public void Init(int maxhp, int maxshield)
        {
            GhostHp = maxhp;
            GhostSH = maxshield;
        }

        public void SetHPBar(float percent)
        {
            Hp.fillAmount = percent;
        }

        public void SetShieldBar(float percent)
        {
            Shield.fillAmount = percent;
        }

        public void SetColorBars(bool imEnnemy)
        {
            Hp.color = GameMng.UI.GetHpBarColor(imEnnemy);
            Shield.color = GameMng.UI.GetShieldBarColor(imEnnemy);
        }

        public void HideUI()
        {
            Canvas.SetActive(false);
        }

        // Method to trigger the animation when the unit takes damage
        public void OnDamageTaken()
        {
            if (Animation != null && Animation["ShowBars"] != null)
            {
                Animation.Play("ShowBars");
            }
        }
    }
}

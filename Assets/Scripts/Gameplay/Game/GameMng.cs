namespace Cosmicrafts
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using System.Linq;
    using System.Collections;

    public class GameMng : MonoBehaviour
    {
        public static GameMng GM;
        public static Player P; // Player component reference
        public static GameMetrics MT;
        public static UIGameMng UI;
        public Vector3[] BS_Positions; // Base stations positions [0] = Enemy (Red), [1] = Player (Blue)

        [Header("Character Settings")]
        public CharacterBaseSO playerCharacterSO; // Player character definition
        public CharacterBaseSO enemyCharacterSO;  // Enemy character definition (should have Unit and Bot components on its prefab)

        [Header("Faction Visual Settings")]
        [Tooltip("Color used for Player faction units")]
        public Color playerFactionColor = Color.black;
        [Tooltip("Color used for Enemy faction units")]
        public Color enemyFactionColor = Color.red;
        [Tooltip("Color used for Neutral faction units (if any)")]
        public Color neutralFactionColor = Color.gray;
        [Tooltip("Global outline thickness multiplier")]
        [Range(0.0001f, 0.001f)]
        public float outlineThicknessMultiplier = 0.00042f;

        [Header("Respawn Settings")]
        [Tooltip("Seconds to wait before respawn UI is shown")]
        public float respawnDelay = 5f; 
        [Tooltip("Whether player must click to respawn or respawns automatically")]
        public bool requireConfirmationToRespawn = true;

        [Header("Targeting System")]
        [Tooltip("Maximum number of strategic targets per faction")]
        public int maxTargetsPerFaction = 5;
        [Tooltip("Whether units should automatically target closest strategic target")]
        public bool autoTargetNearest = true;

        // Tracking lists
        private List<Unit> units = new List<Unit>();
        private List<Spell> spells = new List<Spell>();
        private int idCounter = 0;
        private Dictionary<string, NFTsCard> allPlayersNfts = new Dictionary<string, NFTsCard>();

        // New enhanced targeting system
        private Dictionary<Faction, List<Unit>> strategicTargets = new Dictionary<Faction, List<Unit>>();
        
        // Old base stations array - keeping for backwards compatibility
        public Unit[] Targets = new Unit[2]; // Array for managing base stations [0] = Enemy, [1] = Player

        // Game state
        private bool GameOver = false;
        private bool isRespawning = false;
        private Unit pendingRespawnUnit = null;
        private Coroutine respawnCoroutine = null;

        private void Awake()
        {
            Debug.Log("--GAME MANAGER AWAKE--");
            if (GM != null && GM != this)
            {
                Destroy(gameObject);
                return;
            }
            GM = this;
            DontDestroyOnLoad(gameObject); // Optional: Keep GameMng across scenes

            Debug.Log("--GAME VARIABLES INITIALIZING--");
            MT = new GameMetrics();
            MT.InitMetrics();
            
            // Initialize strategic targets dictionary
            strategicTargets[Faction.Player] = new List<Unit>();
            strategicTargets[Faction.Enemy] = new List<Unit>();
            strategicTargets[Faction.Neutral] = new List<Unit>();
            
            Debug.Log("--GAME VARIABLES READY--");
        }

        private void Start()
        {
            Debug.Log("--GAME MANAGER START--");

            // Spawn Player Base
            if (playerCharacterSO != null)
            {
                SpawnPlayerBase();
            }
            else
            {
                Debug.LogError("Player Character SO not assigned in GameMng Inspector! Player base won't spawn.");
            }

            // Spawn Enemy Base
            if (enemyCharacterSO != null)
            {
                SpawnEnemyBase();
            }
            else
            {
                Debug.LogError("Enemy Character SO not assigned in GameMng Inspector! Enemy base won't spawn.");
            }

            // Ensure both bases spawned before declaring ready
            if (Targets[0] != null && Targets[1] != null)
            {
                Debug.Log("--GAME MANAGER READY--");
            }
            else
            {
                 Debug.LogError("--GAME MANAGER FAILED TO INITIALIZE BASES!--");
            }
        }

        // --- New Player Spawning Function ---
        private void SpawnPlayerBase()
        {
            if (playerCharacterSO == null || playerCharacterSO.BasePrefab == null)
            {
                Debug.LogError("Cannot spawn player base: Player Character SO or its BasePrefab is null!");
                return;
            }

            int playerId = 1;
            Faction playerFaction = Faction.Player;
            int playerBaseIndex = 1; // Blue base position index

            GameObject playerObject = Instantiate(playerCharacterSO.BasePrefab, BS_Positions[playerBaseIndex], Quaternion.identity);
            Player playerComponent = playerObject.GetComponent<Player>();
            Unit playerUnit = playerObject.GetComponent<Unit>();

            if (playerComponent == null || playerUnit == null)
            {
                Debug.LogError($"Player base prefab '{playerCharacterSO.BasePrefab.name}' is missing required components (Player, Unit)! Destroying instance.");
                Destroy(playerObject);
                return;
            }
            
            // --- Initialize Unit ---
            playerUnit.IsDeath = false; // Ensure alive state from the start
            
            // Set faction (this will automatically set MyTeam and PlayerId through properties)
            playerUnit.MyFaction = playerFaction;
            
            playerUnit.setId(GenerateUnitId());

            // Apply SO overrides for stats BEFORE base initialization
            playerCharacterSO.ApplyOverridesToUnit(playerUnit);

            // Ensure minimum health/shield AFTER overrides
            if (playerUnit.HitPoints <= 0) {
                 Debug.LogWarning($"Player base HP was {playerUnit.HitPoints} after SO override, setting to 100.");
                 playerUnit.SetMaxHitPoints(100);
                 playerUnit.HitPoints = 100;
            }
            if (playerUnit.Shield < 0) { // Allow 0 shield, but not negative
                 Debug.LogWarning($"Player base Shield was {playerUnit.Shield} after SO override, setting to 0.");
                 playerUnit.SetMaxShield(0);
                 playerUnit.Shield = 0;
            }

            // --- Initialize Player ---
            playerComponent.ID = playerId;
            playerComponent.MyFaction = playerFaction;
            // GameMng.P should be set in Player's Start method

            // --- Register ---
            Targets[playerBaseIndex] = playerUnit;
            AddUnit(playerUnit);
            
            // Add to strategic targets
            AddStrategicTarget(playerUnit);
            
            // Subscribe to death event
            playerUnit.OnUnitDeath += HandlePlayerBaseStationDeath;

            Debug.Log($"Player base spawned: {playerUnit.gameObject.name} | HP: {playerUnit.HitPoints}/{playerUnit.GetMaxHitPoints()} | Shield: {playerUnit.Shield}/{playerUnit.GetMaxShield()}");
        }

        // --- New Enemy Spawning Function ---
        private void SpawnEnemyBase()
        {
             if (enemyCharacterSO == null || enemyCharacterSO.BasePrefab == null)
            {
                Debug.LogError("Cannot spawn enemy base: Enemy Character SO or its BasePrefab is null!");
                return;
            }

            // Use Faction enum directly instead of separate Team and PlayerId values
            Faction enemyFaction = Faction.Enemy;
            int enemyBaseIndex = 0; // Red base position index

            GameObject enemyObject = Instantiate(enemyCharacterSO.BasePrefab, BS_Positions[enemyBaseIndex], Quaternion.identity);
            Unit enemyUnit = enemyObject.GetComponent<Unit>();

            if (enemyUnit == null) // Unit is essential
            {
                Debug.LogError($"Enemy base prefab '{enemyCharacterSO.BasePrefab.name}' is missing Unit component! Destroying instance.");
                Destroy(enemyObject);
                return;
            }

            // --- Initialize Unit ---
            enemyUnit.IsDeath = false; // Ensure alive state
            
            // Set faction (this will automatically set MyTeam and PlayerId through properties)
            enemyUnit.MyFaction = enemyFaction;
            
            enemyUnit.setId(GenerateUnitId());

            // Apply SO overrides for stats
            enemyCharacterSO.ApplyOverridesToUnit(enemyUnit);

            // Ensure minimum health/shield AFTER overrides
             if (enemyUnit.HitPoints <= 0) {
                 Debug.LogWarning($"Enemy base HP was {enemyUnit.HitPoints} after SO override, setting to 100.");
                 enemyUnit.SetMaxHitPoints(100);
                 enemyUnit.HitPoints = 100;
            }
            if (enemyUnit.Shield < 0) {
                 Debug.LogWarning($"Enemy base Shield was {enemyUnit.Shield} after SO override, setting to 0.");
                 enemyUnit.SetMaxShield(0);
                 enemyUnit.Shield = 0;
            }

            // --- Register ---
            Targets[enemyBaseIndex] = enemyUnit;
            AddUnit(enemyUnit);
            
            // Add to strategic targets
            AddStrategicTarget(enemyUnit);
            
            // Optionally subscribe to enemy death: enemyUnit.OnUnitDeath += HandleEnemyBaseStationDeath;

            Debug.Log($"Enemy base spawned: {enemyUnit.gameObject.name} | HP: {enemyUnit.HitPoints}/{enemyUnit.GetMaxHitPoints()} | Shield: {enemyUnit.Shield}/{enemyUnit.GetMaxShield()} | Bot Component: N/A");
        }

        // Handle player base station death - make this much more direct and aggressive
        private void HandlePlayerBaseStationDeath(Unit baseStation)
        {
            if (P == null || baseStation.GetComponent<Player>() != P || isRespawning)
            {
                // Ignore if it's not the player, or if already respawning, or if player isn't set yet
                return;
            }

            Debug.Log("Player base station destroyed - initiating soul transformation...");
            
            // Don't deactivate immediately - let the soul transformation happen visually
            // baseStation.gameObject.SetActive(false);  // Commented out to keep soul visible
            
            // Track the unit being respawned
            pendingRespawnUnit = baseStation;
            
            // Start the reset process with longer delay to allow for soul experience
            respawnCoroutine = StartCoroutine(ShowRespawnPrompt(baseStation));
        }

        // Updated to show respawn prompt instead of automatic respawn
        private IEnumerator ShowRespawnPrompt(Unit playerUnit)
        {
            isRespawning = true;

            // Allow time for the soul experience before showing respawn UI
            yield return new WaitForSeconds(respawnDelay);

            // Show respawn UI
            if (UI != null)
            {
                // If we're requiring confirmation, show the respawn UI with a button
                if (requireConfirmationToRespawn)
                {
                    UI.ShowRespawnPrompt();
                    // The UI button will call ForcePlayerRespawn() when clicked
                }
                else
                {
                    // If no confirmation required, start respawn countdown
                    UI.ShowRespawnCountdown(respawnDelay);
                    yield return new WaitForSeconds(respawnDelay);
                    StartCoroutine(CompletePlayerRespawn(playerUnit));
                }
            }
            else
            {
                // Fallback if UI is missing - respawn directly
                yield return new WaitForSeconds(respawnDelay);
                StartCoroutine(CompletePlayerRespawn(playerUnit));
            }
        }
        
        // Separate coroutine for the actual respawn mechanics
        private IEnumerator CompletePlayerRespawn(Unit playerUnit)
        {
            if (GameOver) // Check if game ended during delay
            {
                isRespawning = false;
                pendingRespawnUnit = null;
                yield break;
            }

            // Determine the correct spawn position based on the player's faction
            Team playerTeam = FactionManager.ConvertFactionToTeam(P.MyFaction);
            int playerBaseIndex = playerTeam == Team.Blue ? 1 : 0;
            Vector3 respawnPosition = BS_Positions[playerBaseIndex];
            
            // Force teleport to respawn position BEFORE activating
            playerUnit.transform.position = respawnPosition;
            playerUnit.transform.rotation = Quaternion.identity;

            // Reset health, shield, and state flags
            playerUnit.IsDeath = false;
            playerUnit.HitPoints = playerUnit.GetMaxHitPoints();
            playerUnit.Shield = playerUnit.GetMaxShield();
            
            // Reset visual elements and components
            if (playerUnit.UI != null && playerUnit.UI.Canvas != null) 
            {
                playerUnit.UI.Canvas.SetActive(true);
                playerUnit.UI.SetHPBar(1f);
                playerUnit.UI.SetShieldBar((float)playerUnit.Shield / (float)playerUnit.GetMaxShield());
            }
            
            // Reset shield visual
            if (playerUnit.ShieldGameObject != null)
            {
                playerUnit.ShieldGameObject.SetActive(false);
            }
            
            // Make sure ALL colliders are enabled
            Collider[] colliders = playerUnit.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                col.enabled = true;
            }
            
            // Reset shooter
            Shooter shooter = playerUnit.GetComponent<Shooter>();
            if (shooter != null)
            {
                shooter.CanAttack = true;
                shooter.ResetShooter();
            }
            
            // Reset animator
            if (playerUnit.GetAnimator() != null)
            {
                Animator anim = playerUnit.GetAnimator();
                anim.Rebind();
                anim.Update(0f);
                anim.ResetTrigger("Die");
                anim.SetBool("Idle", true);
            }
            
            // Reset mesh appearance (remove soul effect)
            if (playerUnit.Mesh != null)
            {
                // Make sure mesh is visible
                playerUnit.Mesh.SetActive(true);
                
                // Reset any material changes from soul state
                Renderer[] renderers = playerUnit.Mesh.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in renderers)
                {
                    // Reset material properties if they were modified
                    Material[] materials = rend.materials;
                    foreach (Material mat in materials)
                    {
                        // Reset transparency
                        if (mat.HasProperty("_Mode"))
                        {
                            mat.SetFloat("_Mode", 0); // Opaque mode
                            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                            mat.SetInt("_ZWrite", 1);
                            mat.DisableKeyword("_ALPHATEST_ON");
                            mat.DisableKeyword("_ALPHABLEND_ON");
                            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            mat.renderQueue = -1;
                        }
                        
                        // Reset color tint
                        if (mat.HasProperty("_Color"))
                        {
                            mat.color = Color.white;
                        }
                    }
                }
                
                // Reset transform position in case it was modified by floating animation
                playerUnit.Mesh.transform.localPosition = Vector3.zero;
                playerUnit.Mesh.transform.localRotation = Quaternion.identity;
            }

            // Reset camera to normal view
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                CameraController cameraController = mainCamera.GetComponent<CameraController>();
                if (cameraController != null)
                {
                    cameraController.ResetCamera();
                }
            }

            // AFTER everything is set up, activate the player object
            playerUnit.gameObject.SetActive(true);
            
            // Hide respawn UI
            if (UI != null)
            {
                UI.HideRespawnUI();
            }
            
            // CRITICAL FIX: Force enemy detector to reset and re-detect enemies in range
            StartCoroutine(ResetEnemyDetection(playerUnit));
            
            Debug.Log($"PLAYER RESET COMPLETE - HP: {playerUnit.HitPoints}/{playerUnit.GetMaxHitPoints()}");
            isRespawning = false;
            pendingRespawnUnit = null;
        }
        
        // Helper method to force enemy detection refresh after respawn
        private IEnumerator ResetEnemyDetection(Unit playerUnit)
        {
            // Wait one frame to ensure everything is initialized
            yield return null;
            
            // Get the shooter component
            Shooter shooter = playerUnit.GetComponent<Shooter>();
            if (shooter != null && shooter.EnemyDetector != null)
            {
                // Temporarily disable and re-enable the enemy detector collider
                // This forces OnTriggerEnter to fire again for all overlapping objects
                bool wasEnabled = shooter.EnemyDetector.enabled;
                shooter.EnemyDetector.enabled = false;
                
                // Wait another frame
                yield return null;
                
                // Re-enable and force radius update
                shooter.EnemyDetector.enabled = wasEnabled;
                shooter.EnemyDetector.radius = shooter.GetWorldDetectionRange();
                
                // Find all enemy units in range manually
                List<Unit> units = GetUnitsListClone();
                foreach (Unit unit in units)
                {
                    if (unit != null && !unit.IsDeath && unit.MyFaction != playerUnit.MyFaction)
                    {
                        float distance = Vector3.Distance(playerUnit.transform.position, unit.transform.position);
                        if (distance <= shooter.GetWorldDetectionRange())
                        {
                            shooter.AddEnemy(unit);
                        }
                    }
                }
                
                Debug.Log($"Player Shooter: Re-detected {(shooter.GetIdTarget() != 0 ? "found target" : "no target found")}");
            }
        }

        public Unit CreateUnit(GameObject obj, Vector3 position, Team team, string nftKey = "none", int playerId = -1)
        {
            if (obj == null) {
                Debug.LogError("CreateUnit called with a null prefab!");
                return null;
            }
            GameObject unitObj = Instantiate(obj, position, Quaternion.identity);
            Unit unit = unitObj.GetComponent<Unit>();

            if (unit != null)
            {
                // CRITICAL: Set IsDeath flag first
                unit.IsDeath = false;

                // Initialize base stats before applying NFTs
                unit.SetMaxHitPoints(Mathf.Max(10, unit.GetMaxHitPoints())); // Sensible minimum
                unit.HitPoints = unit.GetMaxHitPoints();
                unit.SetMaxShield(Mathf.Max(0, unit.GetMaxShield())); // Allow 0 shield
                unit.Shield = unit.GetMaxShield();

                // Set faction - convert from legacy team and player ID
                Faction unitFaction = team == Team.Blue ? Faction.Player : Faction.Enemy;
                if (playerId != -1) {
                    // If explicit player ID provided, use that to determine faction
                    unitFaction = playerId == 1 ? Faction.Player : Faction.Enemy;
                }
                
                // Set the faction directly
                unit.MyFaction = unitFaction;

                unit.setId(GenerateUnitId());

                // Apply NFT data if provided - fix PlayerId warning
                int derivedPlayerId = unit.MyFaction == Faction.Player ? 1 : 2;
                NFTsUnit nftData = GetNftCardData(nftKey, derivedPlayerId) as NFTsUnit;
                if (nftData != null) {
                    unit.SetNfts(nftData); // Assuming SetNfts applies stats from the NFT

                    // Verify health/shield AFTER NFT application
                    if (unit.HitPoints <= 0)
                    {
                        Debug.LogWarning($"Unit HP was {unit.HitPoints} after NFT '{nftKey}', forcing to 10.");
                        unit.SetMaxHitPoints(Mathf.Max(10, unit.GetMaxHitPoints())); // Re-ensure minimum if NFT reduced it
                        unit.HitPoints = 10;
                    }
                     if (unit.Shield < 0)
                    {
                        Debug.LogWarning($"Unit Shield was {unit.Shield} after NFT '{nftKey}', forcing to 0.");
                        unit.SetMaxShield(Mathf.Max(0, unit.GetMaxShield()));
                        unit.Shield = 0;
                    }
                }

                // Register with game manager
                AddUnit(unit);

                //Debug.Log($"Unit created: {unit.gameObject.name} | HP: {unit.HitPoints}/{unit.GetMaxHitPoints()} | Shield: {unit.Shield}/{unit.GetMaxShield()} | Team: {unit.MyFaction}");
            }
            else
            {
                Debug.LogError($"Failed to get Unit component from instantiated object: {obj.name}");
                Destroy(unitObj); // Clean up unusable object
            }

            return unit;
        }

        public Spell CreateSpell(GameObject obj, Vector3 position, Team team, string nftKey = "none", int playerId = -1)
        {
            if (obj == null) {
                Debug.LogError("CreateSpell called with a null prefab!");
                return null;
            }
            
            GameObject spellObj = Instantiate(obj, position, Quaternion.identity);
            Spell spell = spellObj.GetComponent<Spell>();
            
            if (spell != null)
            {
                // Convert from legacy team/playerId to unified faction system
                Faction spellFaction = team == Team.Blue ? Faction.Player : Faction.Enemy;
                if (playerId != -1) {
                    // If explicit player ID provided, use that to determine faction
                    spellFaction = playerId == 1 ? Faction.Player : Faction.Enemy;
                }
                
                // Set faction directly
                spell.MyFaction = spellFaction;
                
                // Use setId instead of Init
                spell.setId(GenerateUnitId());
                
                if(!string.IsNullOrEmpty(nftKey)) {
                    // Apply NFT data if available
                    NFTsSpell nFTsSpell = GetNftSpellData(nftKey, spellFaction == Faction.Player ? 1 : 2);
                    if (nFTsSpell != null) {
                        spell.SetNfts(nFTsSpell);
                    }
                }
                
                AddSpell(spell);
            }
            
            return spell;
        }

        public void AddUnit(Unit unit)
        {
             if (unit == null) {
                Debug.LogWarning("Attempted to add a null unit.");
                return;
            }
            if (!units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        public void AddSpell(Spell spell)
        {
            if (spell == null) {
                Debug.LogWarning("Attempted to add a null spell.");
                return;
            }
            if (!spells.Contains(spell))
            {
                spells.Add(spell);
            }
        }

        // New function to manage strategic targets
        public void AddStrategicTarget(Unit unit)
        {
            if (unit == null || unit.IsDeath) return;

            // Get the appropriate faction list
            Faction faction = unit.MyFaction;
            List<Unit> targetList = strategicTargets[faction];

            // Check if this unit is already in the list
            if (!targetList.Contains(unit))
            {
                // Add and enforce maximum limit
                targetList.Add(unit);
                if (targetList.Count > maxTargetsPerFaction)
                {
                    // Remove oldest target if exceeding max
                    targetList.RemoveAt(0);
                }
            }
        }

        // Get nearest strategic target for a unit to move against
        public Unit GetNearestStrategicTarget(Unit forUnit)
        {
            if (forUnit == null) return null;

            // Get opposing faction
            Faction enemyFaction = forUnit.MyFaction == Faction.Player ? Faction.Enemy : Faction.Player;
            
            // If enemy faction has no targets, fallback to base stations
            if (strategicTargets[enemyFaction].Count == 0)
            {
                // Legacy targets array fallback
                int targetIndex = forUnit.MyFaction == Faction.Player ? 0 : 1;
                return Targets[targetIndex];
            }

            // Find nearest target from enemy faction's strategic targets
            Unit nearestTarget = null;
            float nearestDistance = float.MaxValue;

            foreach (Unit target in strategicTargets[enemyFaction])
            {
                if (target != null && !target.IsDeath)
                {
                    float distance = Vector3.Distance(forUnit.transform.position, target.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestTarget = target;
                    }
                }
            }

            // If no valid target found from strategic list, fallback to base stations
            if (nearestTarget == null)
            {
                int targetIndex = forUnit.MyFaction == Faction.Player ? 0 : 1;
                nearestTarget = Targets[targetIndex];
            }

            return nearestTarget;
        }

        public void DeleteUnit(Unit unit)
        {
            if (unit != null && units.Contains(unit))
            {
                units.Remove(unit);
                
                // Remove from strategic targets if present
                if (strategicTargets.ContainsKey(unit.MyFaction))
                {
                    strategicTargets[unit.MyFaction].Remove(unit);
                }
                
                //Debug.Log($"DeleteUnit called for {unit.gameObject.name} - Team: {unit.MyFaction}, ID: {unit.getId()}");
                Destroy(unit.gameObject);
            }
        }

        public void DeleteSpell(Spell spell)
        {
            if (spell != null && spells.Contains(spell))
            {
                spells.Remove(spell);
                Destroy(spell.gameObject);
            }
        }

        public void KillUnit(Unit unit)
        {
            if (unit != null && !unit.IsDeath) // Only kill if not already dead
            {
                unit.Die(); // Trigger death sequence
                // Let the Die() method handle removal/destruction usually via OnUnitDeath event
            }
        }

        public void EndGame(Faction winner)
        {
            // Prevent the game from actually ending for infinite play
            Team legacyTeam = winner == Faction.Player ? Team.Blue : Team.Red;
            Debug.Log($"Game would have ended with {winner} faction winning, but continuing for infinite play.");
            // GameOver = true; // Keep commented out for infinite play

            if (UI != null && UI.ResultsScreen != null) // Check if UI and ResultsScreen exist
            {
                UI.SetGameOver(legacyTeam); // Pass legacy team for backward compatibility
            }
            else
            {
                Debug.LogWarning("Cannot show game over screen: UI Manager or Results Screen not found.");
            }
        }
        
        // Overload for backward compatibility
        public void EndGame(Team winner)
        {
            Faction winnerFaction = winner == Team.Blue ? Faction.Player : Faction.Enemy;
            EndGame(winnerFaction);
        }

        public Color GetColorUnit(Faction faction, int playerId = -1)
        {
            // Use configurable colors from inspector
            switch (faction)
            {
                case Faction.Player:
                    return playerFactionColor;
                case Faction.Enemy:
                    return enemyFactionColor;
                case Faction.Neutral:
                    return neutralFactionColor;
                default:
                    return Color.white; // Fallback
            }
        }
        
        // Backward compatibility method
        public Color GetColorUnit(Team team, int playerId)
        {
            return GetColorUnit(team == Team.Blue ? Faction.Player : Faction.Enemy, playerId);
        }

        // New method to get outline thickness for units
        public float GetOutlineThickness(float unitSize)
        {
            return unitSize * outlineThicknessMultiplier;
        }

        public Vector3 GetDefaultTargetPosition(Faction faction)
        {
            // Check if we have strategic targets for opposing faction
            Faction opposingFaction = faction == Faction.Player ? Faction.Enemy : Faction.Player;
            
            if (strategicTargets[opposingFaction].Count > 0)
            {
                // Find nearest strategic target
                Unit nearestTarget = null;
                float nearestDist = float.MaxValue;
                
                // Get a position to use as reference (usually player position or center of map)
                Vector3 referencePos = BS_Positions[faction == Faction.Player ? 1 : 0];
                
                // If player exists, use player position as reference for enemy units
                if (faction == Faction.Enemy && P != null)
                {
                    referencePos = P.transform.position;
                }
                
                foreach (Unit target in strategicTargets[opposingFaction])
                {
                    if (target != null && !target.IsDeath)
                    {
                        float dist = Vector3.Distance(referencePos, target.transform.position);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestTarget = target;
                        }
                    }
                }
                
                if (nearestTarget != null)
                {
                    return nearestTarget.transform.position;
                }
            }
            
            // Fallback to legacy base positions
            int index = faction == Faction.Player ? 0 : 1; // Player targets Enemy (0), Enemy targets Player (1)
            if (Targets[index] != null)
                return Targets[index].transform.position;
            else // Fallback if target base doesn't exist yet
                return BS_Positions[index];
        }
        
        // Backward compatibility method
        public Vector3 GetDefaultTargetPosition(Team team)
        {
            return GetDefaultTargetPosition(team == Team.Blue ? Faction.Player : Faction.Enemy);
        }

        public Transform GetFinalTransformTarget(Faction faction)
        {
            if (GameOver) return transform; // Return GameMng transform if game over
            
            if (autoTargetNearest)
            {
                // Get opposing faction
                Faction opposingFaction = faction == Faction.Player ? Faction.Enemy : Faction.Player;
                
                // Check if we have any strategic targets for the opposing faction
                if (strategicTargets[opposingFaction].Count > 0)
                {
                    // Find nearest valid target
                    Unit nearestTarget = null;
                    float nearestDist = float.MaxValue;
                    
                    // Get a position to use as reference (usually player position or center of map)
                    Vector3 referencePos = BS_Positions[faction == Faction.Player ? 1 : 0];
                    
                    foreach (Unit target in strategicTargets[opposingFaction])
                    {
                        if (target != null && !target.IsDeath)
                        {
                            float dist = Vector3.Distance(referencePos, target.transform.position);
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                nearestTarget = target;
                            }
                        }
                    }
                    
                    if (nearestTarget != null)
                    {
                        return nearestTarget.transform;
                    }
                }
            }

            // Legacy fallback behavior
            int targetIndex = faction == Faction.Player ? 0 : 1; // Player targets Enemy (0), Enemy targets Player (1)
            return Targets[targetIndex] != null ? Targets[targetIndex].transform : transform; // Fallback to GameMng transform
        }
        
        // Backward compatibility method
        public Transform GetFinalTransformTarget(Team team)
        {
            return GetFinalTransformTarget(team == Team.Blue ? Faction.Player : Faction.Enemy);
        }

        public bool IsGameOver()
        {
            return GameOver;
        }

        public bool MainStationsExist()
        {
            // Check if both base stations (Targets) exist and are not marked as dead
            return Targets[0] != null && !Targets[0].IsDeath && Targets[1] != null && !Targets[1].IsDeath;
        }

        private int GenerateUnitId()
        {
            idCounter++;
            return idCounter;
        }

         // --- NFT Data Management ---
        // Consider moving NFT logic to a separate manager if it gets complex

        public void AddNftCardData(NFTsUnit nFTsCard, int playerId)
        {
            if (nFTsCard == null) return;
            string finalKey = $"{playerId}_{nFTsCard.KeyId}";
            if (!allPlayersNfts.ContainsKey(finalKey))
            {
                allPlayersNfts.Add(finalKey, nFTsCard);
            }
            else
            {
                //Debug.LogWarning($"Duplicate NFT Unit key ignored: {finalKey}");
            }
        }

        public void AddNftCardData(NFTsSpell nFTsSpell, int playerId)
        {
             if (nFTsSpell == null) return;
            string finalKey = $"{playerId}_{nFTsSpell.KeyId}";
             if (!allPlayersNfts.ContainsKey(finalKey))
            {
                allPlayersNfts.Add(finalKey, nFTsSpell);
            }
             else
            {
                //Debug.LogWarning($"Duplicate NFT Spell key ignored: {finalKey}");
            }
        }

        private NFTsUnit GetNftCardData(string nftKey, int playerId)
        {
             if (string.IsNullOrEmpty(nftKey) || nftKey == "none") return null;
            string finalKey = $"{playerId}_{nftKey}";
            return allPlayersNfts.TryGetValue(finalKey, out NFTsCard card) ? card as NFTsUnit : null;
        }

        private NFTsSpell GetNftSpellData(string nftKey, int playerId)
        {
             if (string.IsNullOrEmpty(nftKey) || nftKey == "none") return null;
            string finalKey = $"{playerId}_{nftKey}";
            return allPlayersNfts.TryGetValue(finalKey, out NFTsCard card) ? card as NFTsSpell : null;
        }

        // --- Utility and Information Methods ---

        public int CountUnits()
        {
            // Filter out potentially destroyed units before counting
            units.RemoveAll(unit => unit == null);
            return units.Count;
        }

        public int CountUnits(Faction faction)
        {
            return units.Count(unit => unit.MyFaction == faction);
        }
        
        // Backward compatibility method
        public int CountUnits(Team team)
        {
            return CountUnits(team == Team.Blue ? Faction.Player : Faction.Enemy);
        }

        // Get remaining seconds (restored for backward compatibility)
        public int GetRemainingSecs()
        {
            // Game has no time limit, so return a default high value
            return 999; 
        }

        // Helper to get the player unit instance
        public Unit GetPlayerUnit()
        {
            return (P != null) ? P.GetComponent<Unit>() : null;
        }

         // Helper to get the enemy base unit instance
        public Unit GetEnemyBaseUnit()
        {
             // Return Targets[0] directly, checking for null
             return (Targets != null && Targets.Length > 0) ? Targets[0] : null;
        }

        // Add the missing GetUnitsListClone method
        public List<Unit> GetUnitsListClone()
        {
            // Create a copy of the units list to avoid modification issues during iteration
            return new List<Unit>(units);
        }

        // Force an immediate player respawn (called by respawn button)
        public void ForcePlayerRespawn()
        {
            if (isRespawning && pendingRespawnUnit != null)
            {
                // Stop the current respawn coroutine
                if (respawnCoroutine != null)
                {
                    StopCoroutine(respawnCoroutine);
                }
                
                // Skip the wait and respawn immediately
                StartCoroutine(CompletePlayerRespawn(pendingRespawnUnit));
            }
        }
    }
}

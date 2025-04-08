using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;
using EdjCase.ICP.Candid.Models;

// Type aliases
using TokenId = EdjCase.ICP.Candid.Models.UnboundedUInt;
using UnboundedUInt = EdjCase.ICP.Candid.Models.UnboundedUInt;

/// <summary>
/// Repository for NFT data including characters, units, and avatars
/// </summary>
public class NFTRepository : BaseRepository
{
    // Collections of NFTs by type
    public List<NFTData> Characters { get; private set; } = new List<NFTData>();
    public List<NFTData> Units { get; private set; } = new List<NFTData>();
    public List<NFTData> Avatars { get; private set; } = new List<NFTData>();
    public List<NFTData> Chests { get; private set; } = new List<NFTData>();
    
    // Current selected items
    public NFTData SelectedAvatar { get; private set; }
    
    // Player's deck
    public List<TokenId> CurrentDeck { get; private set; } = new List<TokenId>();
    
    // Events
    public event Action<List<NFTData>> OnCharactersLoaded;
    public event Action<List<NFTData>> OnUnitsLoaded;
    public event Action<List<NFTData>> OnAvatarsLoaded;
    public event Action<List<NFTData>> OnChestsLoaded;
    public event Action<List<TokenId>> OnDeckLoaded;
    public event Action<NFTData> OnAvatarSelected;
    public event Action<NFTData> OnNFTUpdated;
    
    /// <summary>
    /// Data structure to store NFT information
    /// </summary>
    public class NFTData
    {
        public TokenId Id { get; set; }
        public TokenMetadata Metadata { get; set; }
        public bool IsSelected { get; set; }
        public int Level { get; set; }
        public bool IsInDeck { get; set; }
        
        public override string ToString()
        {
            return $"NFT {Id}: {Metadata?.ToString() ?? "Unknown"} (Level {Level})";
        }
    }
    
    /// <summary>
    /// Load all NFT data from blockchain
    /// </summary>
    public override async Task LoadAsync(BackendApiClient canister)
    {
        if (canister == null) throw new ArgumentNullException(nameof(canister));
        
        try
        {
            Log("Loading NFT data from blockchain...");
            
            // Get player's principal ID from GameDataManager to ensure it's available
            var principalId = GameDataManager.Instance?.Player?.CurrentPlayer?.Id;
            if (principalId == null)
            {
                LogError("Cannot load NFTs: Player ID not available");
                return;
            }
            
            // Load characters
            await LoadCharacters(canister, principalId);
            
            // Load units
            await LoadUnits(canister, principalId);
            
            // Load avatars
            await LoadAvatars(canister, principalId);
            
            // Load chests
            await LoadChests(canister, principalId);
            
            // Load current deck
            await LoadDeck(canister, principalId);
            
            // Load selected avatar
            await LoadSelectedAvatar(canister);
            
            NotifyDataLoaded();
            Log("All NFT data loaded successfully");
        }
        catch (Exception e)
        {
            LogError($"Error loading NFT data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh NFT data from blockchain
    /// </summary>
    public override async Task RefreshAsync(BackendApiClient canister)
    {
        await LoadAsync(canister);
        NotifyDataUpdated();
    }
    
    /// <summary>
    /// Load character NFTs
    /// </summary>
    private async Task LoadCharacters(BackendApiClient canister, Principal principalId)
    {
        try
        {
            var charactersResult = await canister.GetCharacters(principalId);
            Characters.Clear();
            
            foreach (var item in charactersResult)
            {
                Characters.Add(new NFTData
                {
                    Id = item.F0,
                    Metadata = item.F1,
                    Level = 1, // Default level, may need to be updated with actual data
                });
            }
            
            Log($"Loaded {Characters.Count} character NFTs");
            OnCharactersLoaded?.Invoke(Characters);
        }
        catch (Exception e)
        {
            LogError($"Error loading character NFTs: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load unit NFTs
    /// </summary>
    private async Task LoadUnits(BackendApiClient canister, Principal principalId)
    {
        try
        {
            var unitsResult = await canister.GetUnits(principalId);
            Units.Clear();
            
            foreach (var item in unitsResult)
            {
                Units.Add(new NFTData
                {
                    Id = item.F0,
                    Metadata = item.F1,
                    Level = 1, // Default level, may need to be updated with actual data
                });
            }
            
            Log($"Loaded {Units.Count} unit NFTs");
            OnUnitsLoaded?.Invoke(Units);
        }
        catch (Exception e)
        {
            LogError($"Error loading unit NFTs: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load avatar NFTs
    /// </summary>
    private async Task LoadAvatars(BackendApiClient canister, Principal principalId)
    {
        try
        {
            var avatarsResult = await canister.GetAvatars(principalId);
            Avatars.Clear();
            
            foreach (var item in avatarsResult)
            {
                Avatars.Add(new NFTData
                {
                    Id = item.F0,
                    Metadata = item.F1,
                    Level = 1, // Default level, may need to be updated with actual data
                });
            }
            
            Log($"Loaded {Avatars.Count} avatar NFTs");
            OnAvatarsLoaded?.Invoke(Avatars);
        }
        catch (Exception e)
        {
            LogError($"Error loading avatar NFTs: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load chest NFTs
    /// </summary>
    private async Task LoadChests(BackendApiClient canister, Principal principalId)
    {
        try
        {
            var chestsResult = await canister.GetChests(principalId);
            Chests.Clear();
            
            foreach (var item in chestsResult)
            {
                Chests.Add(new NFTData
                {
                    Id = item.F0,
                    Metadata = item.F1,
                    Level = 1, // Default level, may need to be updated with actual data
                });
            }
            
            Log($"Loaded {Chests.Count} chest NFTs");
            OnChestsLoaded?.Invoke(Chests);
        }
        catch (Exception e)
        {
            LogError($"Error loading chest NFTs: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load player's current deck
    /// </summary>
    private async Task LoadDeck(BackendApiClient canister, Principal principalId)
    {
        try
        {
            var deckResult = await canister.GetPlayerDeck(principalId);
            CurrentDeck.Clear();
            
            if (deckResult.HasValue)
            {
                CurrentDeck.AddRange(deckResult.ValueOrDefault);
                
                // Mark NFTs that are in the deck
                foreach (var tokenId in CurrentDeck)
                {
                    var unit = Units.FirstOrDefault(u => u.Id.Equals(tokenId));
                    if (unit != null)
                    {
                        unit.IsInDeck = true;
                    }
                }
                
                Log($"Loaded player deck with {CurrentDeck.Count} cards");
                OnDeckLoaded?.Invoke(CurrentDeck);
            }
            else
            {
                Log("No deck found for player");
            }
        }
        catch (Exception e)
        {
            LogError($"Error loading player deck: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load selected avatar
    /// </summary>
    private async Task LoadSelectedAvatar(BackendApiClient canister)
    {
        try
        {
            var selectedAvatarResult = await canister.GetSelectedAvatar();
            if (selectedAvatarResult.HasValue)
            {
                var avatarId = selectedAvatarResult.ValueOrDefault;
                SelectedAvatar = Avatars.FirstOrDefault(a => a.Id.Equals(avatarId));
                
                if (SelectedAvatar != null)
                {
                    SelectedAvatar.IsSelected = true;
                    Log($"Selected avatar: {SelectedAvatar.Metadata?.ToString() ?? "Unknown"}");
                    OnAvatarSelected?.Invoke(SelectedAvatar);
                }
            }
        }
        catch (Exception e)
        {
            LogError($"Error loading selected avatar: {e.Message}");
        }
    }
    
    /// <summary>
    /// Change selected avatar
    /// </summary>
    public async Task<bool> UpdateAvatar(BackendApiClient canister, UnboundedUInt avatarId)
    {
        try
        {
            var result = await canister.UpdateAvatar(avatarId);
            if (result.ReturnArg0)
            {
                Log($"Avatar updated successfully");
                
                // Reset current selection
                if (SelectedAvatar != null)
                {
                    SelectedAvatar.IsSelected = false;
                }
                
                // Set new selection
                var newSelectedAvatar = Avatars.FirstOrDefault(a => a.Id.Equals(avatarId));
                if (newSelectedAvatar != null)
                {
                    newSelectedAvatar.IsSelected = true;
                    SelectedAvatar = newSelectedAvatar;
                    OnAvatarSelected?.Invoke(SelectedAvatar);
                }
                
                return true;
            }
            else
            {
                LogError($"Error updating avatar: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception updating avatar: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Update player's deck
    /// </summary>
    public async Task<bool> UpdateDeck(BackendApiClient canister, List<TokenId> newDeck)
    {
        try
        {
            // Create the appropriate argument type
            var deckArg = new BackendApiClient.StoreCurrentDeckArg0();
            deckArg.AddRange(newDeck);
            
            var result = await canister.StoreCurrentDeck(deckArg);
            if (result)
            {
                Log("Deck updated successfully");
                
                // Update local deck data
                CurrentDeck.Clear();
                CurrentDeck.AddRange(newDeck);
                
                // Reset IsInDeck flags
                foreach (var unit in Units)
                {
                    unit.IsInDeck = false;
                }
                
                // Set IsInDeck flags for new deck
                foreach (var tokenId in CurrentDeck)
                {
                    var unit = Units.FirstOrDefault(u => u.Id.Equals(tokenId));
                    if (unit != null)
                    {
                        unit.IsInDeck = true;
                    }
                }
                
                OnDeckLoaded?.Invoke(CurrentDeck);
                return true;
            }
            else
            {
                LogError("Failed to update deck");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception updating deck: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Open a chest NFT
    /// </summary>
    public async Task<bool> OpenChest(BackendApiClient canister, TokenId chestId)
    {
        try
        {
            var result = await canister.OpenChest(chestId);
            if (result.ReturnArg0)
            {
                Log($"Chest opened successfully: {result.ReturnArg1}");
                
                // Remove chest from list
                Chests.RemoveAll(c => c.Id.Equals(chestId));
                
                // Refresh NFT data to get new items
                await RefreshAsync(canister);
                
                return true;
            }
            else
            {
                LogError($"Error opening chest: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception opening chest: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Upgrade an NFT
    /// </summary>
    public async Task<bool> UpgradeNFT(BackendApiClient canister, TokenId nftId)
    {
        try
        {
            var result = await canister.UpgradeNFT(nftId);
            if (result.ReturnArg0)
            {
                Log($"NFT upgraded successfully: {result.ReturnArg1}");
                
                // Update local NFT data
                var nft = FindNFTById(nftId);
                if (nft != null)
                {
                    nft.Level++;
                    OnNFTUpdated?.Invoke(nft);
                }
                
                return true;
            }
            else
            {
                LogError($"Error upgrading NFT: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception upgrading NFT: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Find an NFT by ID across all collections
    /// </summary>
    public NFTData FindNFTById(TokenId id)
    {
        return Characters.FirstOrDefault(c => c.Id.Equals(id)) ??
               Units.FirstOrDefault(u => u.Id.Equals(id)) ??
               Avatars.FirstOrDefault(a => a.Id.Equals(id)) ??
               Chests.FirstOrDefault(c => c.Id.Equals(id));
    }
    
    /// <summary>
    /// Clear all NFT data
    /// </summary>
    public override void Clear()
    {
        Characters.Clear();
        Units.Clear();
        Avatars.Clear();
        Chests.Clear();
        CurrentDeck.Clear();
        SelectedAvatar = null;
        IsLoaded = false;
        Log("NFT data cleared");
    }
} 
using UnityEngine;

namespace Cosmicrafts.Items
{
    /// <summary>
    /// Base ScriptableObject for all equippable items in the game.
    /// Contains common data shared by all items.
    /// </summary>
    // CreateAssetMenu attribute allows easy creation of Item assets in the editor
    [CreateAssetMenu(fileName = "NewItem", menuName = "Cosmicrafts/Items/Generic Item", order = 0)]
    public class ItemSO : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("The displayed name of the item.")]
        public string itemName = "New Item";
        
        [Tooltip("In-game description for tooltips or UI.")]
        [TextArea(3, 5)]
        public string description = "Item Description";
        
        [Tooltip("Icon used in inventory or UI.")]
        public Sprite icon = null;
        
        [Tooltip("The slot this item occupies when equipped.")]
        public EquipmentSlot equipSlot = EquipmentSlot.None;
        
        [Header("Visuals")]
        [Tooltip("Optional prefab to attach/swap on the unit model when equipped.")]
        public GameObject visualPrefab = null;
        
        [Header("Gameplay - Base Class (Override in subclasses)")]
        [Tooltip("Can this item be equipped? (e.g., quest items might not be)")]
        public bool isEquippable = true;
        
        [Tooltip("Value for selling or crafting.")]
        public int itemValue = 0;
        
        // --- Virtual methods for stat calculation/behavior modification ---
        // Subclasses will override these to provide specific bonuses.
        
        /// <summary>
        /// Called when the item is equipped.
        /// Use this to apply passive effects or grant abilities.
        /// </summary>
        /// <param name="equipment">The equipment component managing this item.</param>
        public virtual void OnEquip(UnitEquipment equipment)
        {
            // Base implementation does nothing.
        }
        
        /// <summary>
        /// Called when the item is unequipped.
        /// Use this to remove passive effects or revoke abilities.
        /// </summary>
        /// <param name="equipment">The equipment component managing this item.</param>
        public virtual void OnUnequip(UnitEquipment equipment)
        {
            // Base implementation does nothing.
        }
        
        /// <summary>
        /// Gets the list of stat modifiers provided by this item.
        /// </summary>
        /// <returns>A list of StatModifier objects.</returns>
        public virtual List<StatModifier> GetStatModifiers()
        {
            return new List<StatModifier>(); // Base item provides no modifiers
        }
        
        // Placeholder for StatModifier - we will define this properly next.
        // We need this here temporarily so the GetStatModifiers method compiles.
        public class StatModifier { }
    }
} 
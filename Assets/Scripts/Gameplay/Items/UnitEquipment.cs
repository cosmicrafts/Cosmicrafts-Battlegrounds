using UnityEngine;
using System.Collections.Generic;
using System;

namespace Cosmicrafts.Items
{
    /// <summary>
    /// Manages the items equipped to a Unit.
    /// Handles equipping, unequipping, and provides access to equipped items.
    /// </summary>
    [AddComponentMenu("Cosmicrafts/Items/Unit Equipment")]
    public class UnitEquipment : MonoBehaviour
    {
        // Event triggered when equipment changes (item equipped or unequipped)
        public event Action OnEquipmentChanged;
        
        // Dictionary to store equipped items, keyed by slot
        private Dictionary<EquipmentSlot, ItemSO> _equippedItems = new Dictionary<EquipmentSlot, ItemSO>();
        
        // Reference to the Unit this component is attached to (cached for performance)
        private Unit _unit;
        
        void Awake()
        {
            _unit = GetComponent<Unit>();
            if (_unit == null)
            {
                Debug.LogError("[UnitEquipment] Requires a Unit component on the same GameObject!", this);
                enabled = false;
            }
            
            // Initialize the dictionary with empty slots
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot != EquipmentSlot.None)
                {
                    _equippedItems[slot] = null;
                }
            }
        }
        
        /// <summary>
        /// Equips an item to its designated slot.
        /// Handles unequipping any item currently in that slot.
        /// </summary>
        /// <param name="itemToEquip">The ItemSO to equip.</param>
        /// <returns>True if equipped successfully, false otherwise.</returns>
        public bool EquipItem(ItemSO itemToEquip)
        {
            if (itemToEquip == null || !itemToEquip.isEquippable || itemToEquip.equipSlot == EquipmentSlot.None)
            {
                Debug.LogWarning("[UnitEquipment] Cannot equip null, non-equippable, or slotless item.");
                return false;
            }
            
            EquipmentSlot targetSlot = itemToEquip.equipSlot;
            ItemSO currentlyEquipped = _equippedItems[targetSlot];
            
            // If an item is already in the slot, unequip it first
            if (currentlyEquipped != null)
            {
                // Consider if unequipping should return the item to an inventory (future feature)
                UnequipItem(targetSlot);
            }
            
            // Place the new item in the slot
            _equippedItems[targetSlot] = itemToEquip;
            
            // Call the item's OnEquip logic
            itemToEquip.OnEquip(this);
            
            // Trigger equipment changed event
            OnEquipmentChanged?.Invoke();
            
            Debug.Log($"[UnitEquipment] Equipped '{itemToEquip.itemName}' to slot {targetSlot} on {_unit.name}");
            return true;
        }
        
        /// <summary>
        /// Unequips the item from the specified slot.
        /// </summary>
        /// <param name="slot">The slot to unequip from.</param>
        /// <returns>The ItemSO that was unequipped, or null if the slot was empty.</returns>
        public ItemSO UnequipItem(EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.None || _equippedItems[slot] == null)
            {
                // Nothing to unequip
                return null;
            }
            
            ItemSO unequippedItem = _equippedItems[slot];
            
            // Call the item's OnUnequip logic
            unequippedItem.OnUnequip(this);
            
            // Clear the slot
            _equippedItems[slot] = null;
            
            // Trigger equipment changed event
            OnEquipmentChanged?.Invoke();
            
            Debug.Log($"[UnitEquipment] Unequipped '{unequippedItem.itemName}' from slot {slot} on {_unit.name}");
            
            // Return the unequipped item (e.g., to potentially return to inventory)
            return unequippedItem;
        }
        
        /// <summary>
        /// Gets the item currently equipped in the specified slot.
        /// </summary>
        /// <param name="slot">The slot to check.</param>
        /// <returns>The equipped ItemSO, or null if the slot is empty.</returns>
        public ItemSO GetItemInSlot(EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.None)
                return null;
                
            return _equippedItems[slot];
        }
        
        /// <summary>
        /// Gets a read-only dictionary of all currently equipped items.
        /// </summary>
        public IReadOnlyDictionary<EquipmentSlot, ItemSO> GetAllEquippedItems()
        {
            return _equippedItems;
        }
        
        /// <summary>
        /// Provides access to the Unit this equipment belongs to.
        /// </summary>
        public Unit GetOwnerUnit()
        {
            return _unit;
        }
        
        // --- Methods for Stat Calculation (To be expanded in Phase 2) ---
        
        /// <summary>
        /// Calculates the total bonus for a specific stat from all equipped items.
        /// (This will be replaced by a more robust StatModifier system later)
        /// </summary>
        /// <param name="statType">Identifier for the stat (e.g., "Damage", "MaxHP").</param>
        /// <returns>The total calculated bonus.</returns>
        public float GetTotalStatBonus(string statType)
        {
            float totalBonus = 0f;
            // In Phase 2, this will iterate through _equippedItems
            // and sum up relevant StatModifiers.
            return totalBonus;
        }
    }
} 
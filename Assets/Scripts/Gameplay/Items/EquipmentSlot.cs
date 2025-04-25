namespace Cosmicrafts.Items
{
    /// <summary>
    /// Defines the possible slots where an item can be equipped on a Unit.
    /// </summary>
    public enum EquipmentSlot
    {
        None = 0, // Default/Error
        WeaponPrimary = 1,
        WeaponSecondary = 2,
        Engine = 3,
        Armor = 4,
        Utility1 = 5,
        Utility2 = 6,
        // Add more slots as needed (e.g., ShieldGenerator, SpecialAbility)
    }
} 
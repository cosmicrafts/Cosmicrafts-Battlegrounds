using System;
using EdjCase.ICP.Candid.Models;

/// <summary>
/// Extension methods for common types used in the app
/// </summary>
public static class ExtensionMethods
{
    /// <summary>
    /// Convert an UnboundedUInt to a ulong value
    /// </summary>
    public static ulong ToUInt64(this UnboundedUInt value)
    {
        try
        {
            // Try parsing the string representation
            if (value == null)
                return 0;
                
            var valueStr = value.ToString();
            if (ulong.TryParse(valueStr, out ulong result))
                return result;
                
            // Fallback for very large numbers
            return ulong.MaxValue;
        }
        catch
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Convert an UnboundedUInt to an int value (with possible truncation)
    /// </summary>
    public static int ToInt32(this UnboundedUInt value)
    {
        try
        {
            // Try parsing the string representation
            if (value == null)
                return 0;
                
            var valueStr = value.ToString();
            if (int.TryParse(valueStr, out int result))
                return result;
                
            // Fallback for large numbers - clamp to max int
            return int.MaxValue;
        }
        catch
        {
            return 0;
        }
    }
} 
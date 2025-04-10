using System;

/*
 * This script has some general utils functions
 */
public static class Utils
{
    //Simple string encryptation
    public static string GeneratePasswordFromID(string s)
    {
        string result = s.Substring(s.Length / 2);
        result = "tiap" + result;
        char[] arr = result.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }

    //Reduce the number of characters of a long string (adding ... at the end)
    public static string GetWalletIDShort(string walletId)
    {
        if (string.IsNullOrEmpty(walletId))
        {
            return string.Empty;
        }

        if (walletId.Length > 8)
        {
            return $"{walletId.Substring(0, 4)}...{walletId.Substring(walletId.Length - 4, 4)}";
        } else
        {
            return walletId;
        }
    }
}

/* * * * *
 * URLParameters.cs
 * ----------------
 *
 * This singleton script provides easy access to any URL components in a Web-build.
 * MODIFIED VERSION: DllImport calls have been removed for better cross-platform compatibility
 * 
 * * * * */
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

public class URLParameters : MonoBehaviour
{
    [System.Serializable]
    public struct TestData
    {
        public string Protocol;
        public string Hostname;
        public string Port;
        public string Pathname;
        public string Search;
        public string Hash;
    }
    
    public TestData testData;
    private static TestData m_Data;
    
    public static string Protocol { get { return location_protocol(); } }
    public static string Hostname { get { return location_hostname(); } }
    public static string Port { get { return location_port(); } }
    public static string Pathname { get { return location_pathname(); } }
    public static string Search { get { return location_search(); } set { location_set_search(value); } }
    public static string Hash { get { return location_hash(); } set { location_set_hash(value); } }

    public static string Host { get { return location_host(); } }
    public static string Origin { get { return location_origin(); } }
    public static string Href { get { return location_href(); } }

    private static char[] m_SplitChars = new char[] { '&' };
    private static Dictionary<string, string> ParseURLParams(string aText)
    {
        if (aText == null || aText.Length <= 1)
            return new Dictionary<string, string>();
        // skip "?" / "#" and split parameters at "&"
        var parameters = aText.Substring(1).Split(m_SplitChars);
        var res = new Dictionary<string, string>(parameters.Length);
        foreach (var p in parameters)
        {
            int pos = p.IndexOf('=');
            if (pos > 0)
                res[p.Substring(0, pos)] = p.Substring(pos + 1);
            else
                res[p] = "";
        }
        return res;
    }

    public static Dictionary<string, string> GetSearchParameters()
    {
        return ParseURLParams(Search);
    }
    
    public static Dictionary<string, string> GetHashParameters()
    {
        return ParseURLParams(Hash);
    }

    // Simple stubs for all platforms
    public static string location_protocol() { return m_Data.Protocol; }
    public static string location_hostname() { return m_Data.Hostname; }
    public static string location_port() { return m_Data.Port; }
    public static string location_pathname() { return m_Data.Pathname; }
    public static string location_search() { return m_Data.Search; }
    public static string location_hash() { return m_Data.Hash; }
    public static string location_host() { return m_Data.Hostname + (string.IsNullOrEmpty(m_Data.Port) ? "" : (":" + m_Data.Port)); }
    public static string location_origin() { return m_Data.Protocol + "//" + location_host(); }
    public static string location_href() { return location_origin() + m_Data.Pathname + m_Data.Search + m_Data.Hash; }
    public static void location_set_search(string aSearch) { m_Data.Search = aSearch; }
    public static void location_set_hash(string aHash) { m_Data.Hash = aHash; }

    public void Awake()
    {
        m_Data = testData;
    }
}

public static class DictionaryStringStringExt
{
    public static double GetDouble(this Dictionary<string, string> aDict, string aKey, double aDefault)
    {
        string str;
        if (!aDict.TryGetValue(aKey, out str))
            return aDefault;
        double val;
        if (!double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
            return aDefault;
        return val;
    }
    
    public static int GetInt(this Dictionary<string, string> aDict, string aKey, int aDefault)
    {
        string str;
        if (!aDict.TryGetValue(aKey, out str))
            return aDefault;
        int val;
        if (!int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
            return aDefault;
        return val;
    }
}
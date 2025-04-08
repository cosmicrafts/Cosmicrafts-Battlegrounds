using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ICPTestPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button togglePanelButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button getPlayerButton;
    
    private ICPLoginTest loginTest;
    
    void Start()
    {
        // Find or add ICPLoginTest component
        loginTest = GetComponent<ICPLoginTest>();
        if (loginTest == null)
        {
            loginTest = gameObject.AddComponent<ICPLoginTest>();
        }
        
        // Set up the login test references
        if (loginTest != null)
        {
            // Use reflection to set the serialized fields
            System.Type type = loginTest.GetType();
            System.Reflection.FieldInfo statusTextField = type.GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo getPlayerButtonField = type.GetField("getPlayerButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo usernameInputField = type.GetField("usernameInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (statusTextField != null) statusTextField.SetValue(loginTest, statusText);
            if (getPlayerButtonField != null) getPlayerButtonField.SetValue(loginTest, getPlayerButton);
            if (usernameInputField != null) usernameInputField.SetValue(loginTest, usernameInput);
        }
        
        // Add toggle panel button listener
        if (togglePanelButton != null && panelRoot != null)
        {
            togglePanelButton.onClick.AddListener(() => {
                panelRoot.SetActive(!panelRoot.activeSelf);
            });
        }
    }
    
    private void OnDestroy()
    {
        // Clean up button listeners
        if (togglePanelButton != null)
        {
            togglePanelButton.onClick.RemoveAllListeners();
        }
    }
}
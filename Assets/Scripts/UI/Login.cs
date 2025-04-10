using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using UnityEngine.SceneManagement;
namespace CosmicraftsSP {
public class Login : MonoBehaviour
{

    public TMP_InputField inputNameField;
    
    string playerName;
    private string account;
    
    //[SerializeField] Text walletID;
    [SerializeField] GameObject UIReward;
    [SerializeField] GameObject ClosedBetaScreen;
    string mainScene = "Menu";
    
    [SerializeField] private Animator chooseLoginAnim;
    [SerializeField] private Animator chooseUserAnim;
    
    /*public void OnConnectToWallet()
    {
        GameNetwork.JSLoginPanel(account); //esto aca se la manda a pk// reset login message
        //LoginPanel.SetActive(true);
    }*/

    public void OnRecibeNameData(string usser)//esta es de PK
    {
        Debug.Log("OnRecibeNameData: " + usser);
        if (string.IsNullOrEmpty(usser))
        {
            LoadingPanel.instance.DesactiveLoadingPanel();
            chooseUserAnim.Play("ChooseUsername_Intro");
        }
        else
        {
            LoadingPanel.instance.ActiveLoadingPanel();
            SceneManager.LoadScene(mainScene);
        }

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnNameData(0);
        }
    }

    public void OnNameData(int usser)//esta es de PK
    {
        Debug.Log("OnNameData  " + usser);
        if (usser == 3)
        {
            LoadingPanel.instance.DesactiveLoadingPanel();
            UIReward.SetActive(false);
            ClosedBetaScreen.SetActive(true);
        }
        if (usser == 2)
        {
            LoadingPanel.instance.DesactiveLoadingPanel();
            UIReward.SetActive(true);
        }
        if (usser == 1)
        {
            LoadingPanel.instance.ActiveLoadingPanel();
            SceneManager.LoadScene(mainScene);
            //tutorial
        }
        else
        {
            LoadingPanel.instance.DesactiveLoadingPanel();
            chooseUserAnim.Play("ChooseUsername_Intro");
            //walletID.text = ""+ account;
        }

    }
    public void BackLoginMenu()
    {
        chooseUserAnim.Play("ChooseUsername_Outro"); 
        chooseLoginAnim.Play("ChooseLogin_Intro");
    }

    public void SetPlayerName()
    {
        if (inputNameField.text != null)
        {
            playerName = inputNameField.text;
            PlayerPrefs.SetString("AccounName", playerName);
            GameNetwork.JSLoginPanel(playerName);
            LoadingPanel.instance.ActiveLoadingPanel();
        }
    }

    public void StoickLogin()
    {
        GameNetwork.JSWalletsLogin("stoicWallet");
        LoadingPanel.instance.ActiveLoadingPanel();
        chooseLoginAnim.Play("ChooseLogin_Outro");
        
    }
    public void IdentityLogin()
    {
        GameNetwork.JSWalletsLogin("identityWallet");
        LoadingPanel.instance.ActiveLoadingPanel();
        chooseLoginAnim.Play("ChooseLogin_Outro");
    }
    public void InfinityLogin()
    {
        GameNetwork.JSWalletsLogin("infinityWallet");
        LoadingPanel.instance.ActiveLoadingPanel();
        chooseLoginAnim.Play("ChooseLogin_Outro");
    }
    public void PlugLogin()
    {
        LoadingPanel.instance.ActiveLoadingPanel();
        chooseLoginAnim.Play("ChooseLogin_Outro");
        GameNetwork.JSWalletsLogin("plugWallet");
    }

}
}
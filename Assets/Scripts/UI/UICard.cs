namespace Cosmicrafts {
    using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
/*
 * This code represents a instance of a card on the deck collection menu
 * The card has the UI refrences from the inspector, to show the NFT data
 */
public class UICard : MonoBehaviour
{
    //NFT data source
    protected NFTsCard Data;

    [Header("Text Components")]
    [Tooltip("Card name text component")]
    [SerializeField] protected TMP_Text Txt_Name;
    [Tooltip("Energy cost text component")]
    [SerializeField] protected TMP_Text Txt_Cost;
    [Tooltip("Card type text component")]
    [SerializeField] protected TMP_Text Txt_Type;
    [Tooltip("Card details/description text component")]
    [SerializeField] protected TMP_Text Txt_Details;
    
    [Header("UI Components")]
    [Tooltip("Card icon image component")]
    [SerializeField] public Image Icon;
    [Tooltip("Card animator component")]
    [SerializeField] public Animator animator;

    //Is a skill or unit
    protected bool IsSkill;

    //This card is selected?
    [HideInInspector]
    public bool IsSelected;
    //Type of card (skill, ship or station)
    [HideInInspector]
    public NFTClass TypeCard;
    //The name of the card
    [HideInInspector]
    public string NameCard;
    //The NFT card key
    [HideInInspector]
    public string KeyName;

    //The Index of the card in the current deck
    public int DeckSlot;

    //Initialize the references and values of the card from the NFT data
    public virtual void SetData(NFTsCard data)
    {
        //Set the basic data
        Data = data;
        IsSelected = false;
        IsSkill = data as NFTsSpell != null;
        NameCard = Lang.GetEntityName(data.KeyId); 
        KeyName = data.KeyId;

        //Set the name and description of the NFT
        if (Txt_Name != null) Txt_Name.text = Lang.GetEntityName(data.KeyId);
        if (Txt_Cost != null) Txt_Cost.text = data.EnergyCost.ToString();
        if (Txt_Details != null)
        {
            Txt_Details.text = Lang.GetEntityDescription(data.KeyId);
        }
        //Set the icon of the NFT
        if (Icon != null) Icon.sprite = ResourcesServices.ValidateSprite(data.IconSprite);
        //Set type of the NFT
        if (data as NFTsSpell != null)
        {
            //This card is a spell
            TypeCard = NFTClass.Skill;
            if (Txt_Type != null) Txt_Type.text = Lang.GetText("mn_skill");
        } else
        {
            //This card is a unit
            NFTsUnit nFTsUnit = data as NFTsUnit;
            TypeCard = (NFTClass)nFTsUnit.EntType;
            if (Txt_Type != null) Txt_Type.text = Lang.GetText(TypeCard == NFTClass.Station ? "mn_station" : "mn_ship");
        }
    }

    //Returns the NFTs data
    public NFTsCard GetData()
    {
        return Data;
    }

    //Selects this card
    public void SelectCard()
    {
        IsSelected = true;
        if (Txt_Name != null) Txt_Name.color = Color.green;
    }

    //Removes the selection of this card
    public void DeselectCard()
    {
        IsSelected = false;
        if (Txt_Name != null) Txt_Name.color = Color.white;
    }

    //Refresh icon sprite
    public void RefreshIcon()
    {
        if (Icon != null && Data != null)
            Icon.sprite = ResourcesServices.ValidateSprite(Data.IconSprite);
    }
}
}
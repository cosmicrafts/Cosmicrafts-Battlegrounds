using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName =("Nueva Nave") ,menuName =("Crear Nueva Nave"))]
public class ShipsDataBase : ScriptableObject
{

    #region DataBase
    //----me base en el string correspondiente al GameCard-----------------------------------------------------------
    [Header("Asignar el NFT KEY para la carta")]
    [SerializeField]
    protected string nftsKey;

    //----me base en el string correspondiente al GameCard-----------------------------------------------------------

    [Header("Asignar el nombre para la carta")]
    [SerializeField]
    protected string cardName;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el HitPoint de la carta")]
    [Range(1, 9999)]
    [SerializeField]
    protected int hitPoints;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el Max Hp de la carta")]
    [Range(1, 9999)]
    [SerializeField]
    protected int maxHp;

  

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el Escudo de la carta")]
    [SerializeField]
    [Range(1, 9999)]
    protected int shield;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el Maximo de escudo")]
    [SerializeField]
    protected int maxShield;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el delay para el escudo")]
    [SerializeField]
    [Range(1, 10)]
    protected float shieldDelay;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar la energia requerida")]
    [SerializeField]
    [Range(1, 9999)]
    protected  int energy;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el tamaño para la nave")]
    [SerializeField]
    [Range(0, 10)]
    protected float size;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el tamaño para el buff de la zona de invocacion")]
    [SerializeField]
    [Range(0, 30)]
    protected float spawnAreaSize;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el valor para el tiempo de carga del escudo")]
    [SerializeField]
    protected float shieldLoad = 0f;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el valor para tiempo de recarga del escudo")]
    [SerializeField]
    protected float shieldCharge = 0f;

    //----me base en el int correspondiente al Unit-----------------------------------------------------------
    [Header("Asignar el valor para la velocidad  del escudo")]
    [SerializeField]
    protected float shieldSpeed = 1f;

    #endregion

    #region Variables de Lectura

    public string NFTKEY => nftsKey; //Consulta el  nftsKey; 

    public string CardName => cardName; //Consultar el Player ID ;
    public int HitPoins => hitPoints;//Consultar el hitPoints ;
    public int MaxHp => maxHp;//Consultar el  MaxHp Iniciales ;

    public int Shield => shield; //Consultar el Escudo hitPoints ;

    public int MaxShield => maxShield;//Consultar el Maximo Escudo  ;

    public float ShieldDelay => shieldDelay;//Consultar el Delay Escudo ;
    public int Energy => energy;//Consultar el consumo de energia ;
    public float Size => size;//Consultar el Tamaño ;

    public float SpawnAreaSize => spawnAreaSize;//Consultar el tamaño para el buff de area de invocacion ;
    public float ShieldLoad => shieldLoad;//Consultar el la carga del escudo ;
    public float ShieldCharge => shieldCharge;//Consultar la recarga del escudo;
    public float ShieldSpeed => shieldSpeed;//Consultar la velocidad del escudo  ;
    #endregion
}

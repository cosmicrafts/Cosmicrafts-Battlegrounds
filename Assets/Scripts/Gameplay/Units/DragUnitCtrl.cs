namespace Cosmicrafts {
using UnityEngine;
using EPOOutline;

/*
 * This script controls and validates the draging cards to deploy them (in-game)
 * Should be attached to a UI GameObject
 */

public class DragUnitCtrl : MonoBehaviour
{
    // Static instance that can be accessed from any script
    public static DragUnitCtrl Instance { get; private set; }

    //The number of valid detected areas for deploying
    int areas;
    //The enemy base position
    Vector3 target;

    //The preview 3d model and effects of the card to deploy
    public MeshRenderer MyMesh;
    public MeshFilter MyMeshFilter;
    public Outlinable Outline;
    GameObject currentPreview;

    //The energy cost of the current draging card
    public float TargetCost;

    //The default outline color of the model
    Color DefaultColor;

    private void Awake()
    {
        // Set static instance - handle duplicate instances
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple DragUnitCtrl instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    private void Start()
    {
        //Initialize variables
        areas = 0;
        
        // Get player reference and targets from GameMng
        if (GameMng.P != null && GameMng.GM != null)
        {
            target = GameMng.GM.GetDefaultTargetPosition(GameMng.P.MyTeam);
        }
        else
        {
            Debug.LogWarning("DragUnitCtrl: GameMng.P or GameMng.GM is null. Using default target position.");
            target = Vector3.zero;
        }
        
        DefaultColor = Color.green;
        
        // Ensure the drag visual is initially inactive
        if (MyMesh != null && MyMesh.gameObject != null)
        {
            MyMesh.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        //Update the outline color (green when the draging card can be deployed on the current position)
        if (GameMng.P != null && Outline != null)
        {
            DefaultColor = TargetCost > GameMng.P.CurrentEnergy ? Color.blue : Color.green;
            SetStatusColor(areas > 0 ? DefaultColor : Color.red);
        }
    }

    private void FixedUpdate()
    {
        //Update the position and rotation of the draging card
        transform.position = CMath.GetMouseWorldPos();
        
        // Make the preview face the target
        if (target != null)
        {
            transform.LookAt(CMath.LookToY(transform.position, target));
        }
    }

    //spawnable area detected
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Spawnarea"))
        {
            areas++;
        }
    }

    //spawnable area lost
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Spawnarea"))
        {
            areas--;
        }
    }

    //Return if the player can deploys on the current position
    public bool IsValid()
    {
        return areas > 0;
    }

    //Set the current draging status color
    void SetStatusColor(Color color)
    {
        if (Outline != null)
        {
            Outline.OutlineParameters.Color = color;
        }
    }

    //Set the current preview from a mesh and material
    public void SetMeshAndTexture(Mesh mesh, Material mat)
    {
        if (MyMesh != null && MyMeshFilter != null)
        {
            MyMesh.material = mat;
            MyMeshFilter.mesh = mesh;
        }
    }

    //Shows and hides the current preview game object
    public void setMeshActive(bool active)
    {
        if (MyMesh != null && MyMesh.gameObject != null)
        {
            MyMesh.gameObject.SetActive(active);
        }
        
        if (!active && currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    //Set the current preview from a game object
    public void CreatePreviewObj(GameObject preview)
    {
        if (preview == null)
        {
            Debug.LogWarning("DragUnitCtrl: Attempted to create preview from null GameObject");
            return;
        }
        
        // Destroy previous preview if it exists
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }
        
        currentPreview = Instantiate(preview, transform);

        // Remove any components that could interfere with the preview
        UnitAnimLis unitAnimLis = currentPreview.GetComponent<UnitAnimLis>();
        if(unitAnimLis != null) { DestroyImmediate(unitAnimLis, true);}

        SphereCollider sphereCollider = currentPreview.GetComponent<SphereCollider>(); 
        if(sphereCollider != null) { DestroyImmediate(sphereCollider, true);}
    }
}
}
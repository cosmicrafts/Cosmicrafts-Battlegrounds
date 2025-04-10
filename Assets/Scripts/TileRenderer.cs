using System.Collections.Generic;
using UnityEngine;

public class SpaceProceduralGenerator : MonoBehaviour
{
    [Header("Space Tile Settings")]
    public Sprite spaceTileSpriteBackground; 
    public float tileWorldSize = 16f; 
    public int gridSize = 4;

    [Header("Customization Options")]
    public Color backgroundColor = Color.white;
    public float backgroundScaleFactor = 1f;

    private Transform player;
    private Queue<GameObject> chunkPool = new Queue<GameObject>();
    private GameObject[,] activeChunks;
    private Vector2 gridOrigin;

    private float moveThreshold;

    private void Start()
    {
        player = Camera.main.transform;
        if (player == null)
        {
            Debug.LogWarning("Main camera not found.");
            return;
        }

        activeChunks = new GameObject[gridSize, gridSize];
        gridOrigin = SnapToGrid(player.position - new Vector3((gridSize / 2) * tileWorldSize, (gridSize / 2) * tileWorldSize, 0));
        moveThreshold = tileWorldSize / 4f;
        InitializeGrid();
    }

    private void Update()
    {
        Vector2 playerPosition = new Vector2(player.position.x, player.position.y);
        Vector2 offset = playerPosition - gridOrigin * tileWorldSize;

        if (Mathf.Abs(offset.x) >= moveThreshold || Mathf.Abs(offset.y) >= moveThreshold)
        {
            Vector2 newGridOrigin = SnapToGrid(player.position - new Vector3((gridSize / 2) * tileWorldSize, (gridSize / 2) * tileWorldSize, 0));

            if (newGridOrigin != gridOrigin)
            {
                Vector2 direction = newGridOrigin - gridOrigin;
                ShiftGrid(direction);
                gridOrigin = newGridOrigin;
            }
        }
    }

    void InitializeGrid()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                Vector2 chunkCoord = gridOrigin + new Vector2(x, y);
                activeChunks[x, y] = GetChunkFromPool(chunkCoord);
            }
        }
    }

    void ShiftGrid(Vector2 direction)
    {
        if (direction.x != 0)
        {
            int shiftX = (int)direction.x;
            if (shiftX > 0)
            {
                MoveColumnLeft();
                AddNewColumn(gridSize - 1, gridOrigin.x + gridSize);
            }
            else if (shiftX < 0)
            {
                MoveColumnRight();
                AddNewColumn(0, gridOrigin.x - 1);
            }
        }

        if (direction.y != 0)
        {
            int shiftY = (int)direction.y;
            if (shiftY > 0)
            {
                MoveRowDown();
                AddNewRow(gridSize - 1, gridOrigin.y + gridSize);
            }
            else if (shiftY < 0)
            {
                MoveRowUp();
                AddNewRow(0, gridOrigin.y - 1);
            }
        }
    }

    void MoveColumnLeft()
    {
        for (int y = 0; y < gridSize; y++)
        {
            ReturnChunkToPool(activeChunks[0, y]);
            for (int x = 1; x < gridSize; x++)
            {
                activeChunks[x - 1, y] = activeChunks[x, y];
            }
        }
    }

    void MoveColumnRight()
    {
        for (int y = 0; y < gridSize; y++)
        {
            ReturnChunkToPool(activeChunks[gridSize - 1, y]);
            for (int x = gridSize - 2; x >= 0; x--)
            {
                activeChunks[x + 1, y] = activeChunks[x, y];
            }
        }
    }

    void MoveRowDown()
    {
        for (int x = 0; x < gridSize; x++)
        {
            ReturnChunkToPool(activeChunks[x, 0]);
            for (int y = 1; y < gridSize; y++)
            {
                activeChunks[x, y - 1] = activeChunks[x, y];
            }
        }
    }

    void MoveRowUp()
    {
        for (int x = 0; x < gridSize; x++)
        {
            ReturnChunkToPool(activeChunks[x, gridSize - 1]);
            for (int y = gridSize - 2; y >= 0; y--)
            {
                activeChunks[x, y + 1] = activeChunks[x, y];
            }
        }
    }

    void AddNewColumn(int colIndex, float newX)
    {
        for (int y = 0; y < gridSize; y++)
        {
            Vector2 newChunkCoord = new Vector2(newX, gridOrigin.y + y);
            activeChunks[colIndex, y] = GetChunkFromPool(newChunkCoord);
        }
    }

    void AddNewRow(int rowIndex, float newY)
    {
        for (int x = 0; x < gridSize; x++)
        {
            Vector2 newChunkCoord = new Vector2(gridOrigin.x + x, newY);
            activeChunks[x, rowIndex] = GetChunkFromPool(newChunkCoord);
        }
    }

GameObject GetChunkFromPool(Vector2 chunkCoord)
{
    GameObject chunk;
    if (chunkPool.Count > 0)
    {
        chunk = chunkPool.Dequeue();
        chunk.SetActive(true);
    }
    else
    {
        chunk = new GameObject("Chunk");
        var renderer = chunk.AddComponent<SpriteRenderer>();
        renderer.sprite = spaceTileSpriteBackground;
        renderer.color = backgroundColor;
        chunk.transform.localScale = new Vector3(backgroundScaleFactor, backgroundScaleFactor, 1);
    }

    // ✅ **Fix Positioning: Lay tiles flat on the ground instead of stacking them vertically**
    Vector3 newPosition = new Vector3(
        chunkCoord.x * tileWorldSize, // X stays the same
        -100,                            // Y is now -100 (flat ground)
        chunkCoord.y * tileWorldSize  // Use Y as Z so it lays flat
    );

    chunk.transform.position = newPosition;

    // ✅ **Fix Rotation: Make the tile face upward like a floor**
    chunk.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Rotate 90° on X-axis

    return chunk;
}


    void ReturnChunkToPool(GameObject chunk)
    {
        chunk.SetActive(false);
        chunkPool.Enqueue(chunk);
    }

    Vector2 SnapToGrid(Vector3 position)
    {
        float x = Mathf.FloorToInt(position.x / tileWorldSize);
        float y = Mathf.FloorToInt(position.y / tileWorldSize);
        return new Vector2(x, y);
    }

    private void OnDestroy()
    {
        // No more NativeArray cleanup needed
    }
}

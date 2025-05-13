// TileRenderer.cs
using UnityEngine;

public class TileRenderer : MonoBehaviour
{
    public MapManager mapManager;           // 연결된 MapManager
    public GameObject tilePrefab;           // SpriteRenderer 포함된 프리팹
    public Transform tileParent;            // 시각화 타일들을 담을 부모 오브젝트

    private GameObject[,] tileVisuals;

    void Awake()
    {
        InitVisuals();  // 타일 생성은 Awake에서 미리
    }

    void Start()
    {
        if (mapManager == null)
        {
            Debug.LogError("TileRenderer: mapManager가 설정되지 않았습니다!");
            return;
        }

        // InitVisuals();
        RedrawAllTiles();
    }

    public void InitVisuals()
    {
        int width = mapManager.width;
        int height = mapManager.height;
        tileVisuals = new GameObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject tile = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, tileParent);
                tile.name = $"Tile ({x},{y})";
                tileVisuals[x, y] = tile;

            }
        }
    }

    public void RedrawAllTiles()
    {
        for (int x = 0; x < mapManager.width; x++)
        {
            for (int y = 0; y < mapManager.height; y++)
            {
                int state = mapManager.GetTile(new Vector2Int(x, y));
                Color color = GetColorForState(state);
                tileVisuals[x, y].GetComponent<SpriteRenderer>().color = color;
            }
        }
    }

    private Color GetColorForState(int state)
    {
        switch (state)
        {
            case 0: return Color.gray;
            case 1: return Color.green;
            case 2: return Color.red;
            case 3: return Color.blue;
            default: return Color.black;
        }
    }
}

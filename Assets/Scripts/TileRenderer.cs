using Unity.VisualScripting;
using UnityEngine;

public class TileRenderer : MonoBehaviour
{
    public MapManager mapManager;      // 연결된 MapManager
    public GameObject tilePrefab;      // SpriteRenderer 포함 프리팹
    public Transform tileParent;       // 시각화 타일들을 담을 부모 오브젝트
    public Sprite baseTileSprite;      // 기본 타일 이미지

    private GameObject[,] tileVisuals;

    void Awake()
    {
        InitVisuals();  // 타일 오브젝트 미리 생성
    }

    void Start()
    {
        if (mapManager == null)
        {
            Debug.LogError("TileRenderer: mapManager가 설정되지 않았습니다!");
            return;
        }
        // 초기 한 번만 전체 타일 그리기
        RedrawAllTiles();
    }

    /// <summary>
    /// 타일 오브젝트 생성
    /// </summary>
    public void InitVisuals()
    {
        int w = mapManager.width;
        int h = mapManager.height;
        tileVisuals = new GameObject[w, h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var tile = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, tileParent);
                tile.name = $"Tile ({x},{y})";
                
                // 기본 스프라이트 설정
                var spriteRenderer = tile.GetComponent<SpriteRenderer>();
                if (baseTileSprite != null)
                {
                    spriteRenderer.sprite = baseTileSprite;
                }
                
                tileVisuals[x, y] = tile;
            }
        }
    }

    /// <summary>
    /// 현재 state==1(초록)인 타일의 개수만 세어 반환합니다.
    /// (색칠 없이 단순 카운트용)
    /// </summary>
    public int GetGreenCount()
    {
        int count = 0;
        for (int x = 0; x < mapManager.width; x++)
        {
            for (int y = 0; y < mapManager.height; y++)
            {
                if (mapManager.GetTile(new Vector2Int(x, y)) == 1)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 전체 타일을 다시 그리면서
    /// state==1(초록)인 타일 개수를 세어 반환합니다.
    /// (화면 갱신 + 카운트)
    /// </summary>
    public int RedrawAllTilesAndGetGreenCount()
    {
        int greenCount = 0;

        for (int x = 0; x < mapManager.width; x++)
        {
            for (int y = 0; y < mapManager.height; y++)
            {
                int state = mapManager.GetTile(new Vector2Int(x, y));
                if (state == 1) greenCount++;

                var sr = tileVisuals[x, y].GetComponent<SpriteRenderer>();
                sr.color = GetColorForState(state);
            }
        }

        return greenCount;
    }

    /// 기존: 단순 전체 타일 다시 그리기
    public void RedrawAllTiles()
    {
        for (int x = 0; x < mapManager.width; x++)
        {
            for (int y = 0; y < mapManager.height; y++)
            {
                int state = mapManager.GetTile(new Vector2Int(x, y));
                tileVisuals[x, y].GetComponent<SpriteRenderer>().color = GetColorForState(state);
            }
        }
    }

    public int GetPlayerTileCount(int playerId)
    {
        int count = 0;
        for (int x = 0; x < mapManager.width; x++)
        {
            for (int y = 0; y < mapManager.height; y++)
            {
                if (mapManager.GetTile(new Vector2Int(x, y)) == playerId)
                    count++;
            }
        }
        return count;
    }

    /// 타일 상태에 따른 색 반환
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

using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BasePlayerController : MonoBehaviour
{
    // PlayerController.cs의 변수들과 대응
    public float moveSpeed = 15f;
    public bool isMainPlayer = false; // 새로 추가된 변수

    // PlayerController.cs의 private 변수들이 protected로 변경됨
    protected Vector2Int gridPosition;        // private Vector2Int gridPosition;
    public Vector2Int direction;           // private Vector2Int direction = Vector2Int.zero; // protected에서 public으로 변경
    protected Vector2Int queuedDirection;     // private Vector2Int queuedDirection = Vector2Int.zero;
    public bool isMoving;                  // private bool isMoving = false;
    protected Vector3 targetPosition;         // private Vector3 targetPosition;

    // PlayerController.cs의 컴포넌트 참조들
    protected LineTrailWithCollision trail;   // private LineTrailWithCollision trail;
    protected CornerPointTracker cornerTracker; // private CornerPointTracker cornerTracker;
    protected LoopDetector loopDetector;     // private LoopDetector loopDetector;
    protected MapManager mapManager;          // private MapManager mapManager;
    public bool wasInsideOwnedArea = false;        // private bool wasInsideOwnedArea = false;

    // PlayerController.cs의 Start() 함수에 대응
    protected virtual void Start()
    {
        gridPosition = Vector2Int.RoundToInt(transform.position);
        transform.position = new Vector3(gridPosition.x, gridPosition.y, -1f);
        targetPosition = transform.position;

        InitializeComponents();

        // 메인 플레이어면 카메라 설정
        if (isMainPlayer)
        {
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.parent = transform;
                camera.transform.localPosition = new Vector3(0, 0, -10);
            }
        }

        // wasInsideOwnedArea = mapManager.GetTile(gridPosition) == cornerTracker.playerId;
    }

    // PlayerController.cs에서 컴포넌트 초기화 부분을 분리
    protected virtual void InitializeComponents()
    {
        // 자신의 컴포넌트들은 GetComponent 사용  s
        // 🔧 자식 오브젝트 "TrailDrawer"에서 LineTrailWithCollision 가져오기
        Transform trailObj = transform.Find("TrailDrawer");
        if (trailObj != null)
        {
            trail = trailObj.GetComponent<LineTrailWithCollision>();
        }
        cornerTracker = GetComponent<CornerPointTracker>();

        // 전역 매니저만 Find 사용
        loopDetector = FindFirstObjectByType<LoopDetector>();
        mapManager = FindFirstObjectByType<MapManager>();
    }

    // PlayerController.cs의 Update() 함수에 대응
    protected virtual void Update()
    {
        HandleMovement();  // Update() 내부의 이동 처리 부분
    }

    // PlayerController.cs의 키보드 입력 처리 부분을 추상화
    protected abstract void HandleInput();

    // PlayerController.cs의 이동 처리 로직을 분리
    protected virtual void HandleMovement()
    {
        HandleInput();

        // 방향이 바뀔 때만 코너 저장
        if (!isMoving && queuedDirection != Vector2Int.zero && queuedDirection != -direction)
        {
            // 내 영역 밖에 있을 때만 코너 저장
            if (direction != Vector2Int.zero && queuedDirection != direction && !wasInsideOwnedArea)
            {
                cornerTracker?.AddCorner(gridPosition);
                Debug.Log($"현재 코너 점 개수: {cornerTracker?.GetPoints().Count}");
            }

            direction = queuedDirection;
            gridPosition += direction;
            targetPosition = new Vector3(gridPosition.x, gridPosition.y, -2f);
            isMoving = true;

            // 내 영역 밖에 있을 때만 궤적 활성화
            if (trail != null && !trail.trailActive && !wasInsideOwnedArea)
                trail.trailActive = true;
        }

        // 이동 처리
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime); if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;

                // 맵 경계 체크 - 경계를 벗어나면 사망
                if (!mapManager.InBounds(gridPosition))
                {
                    Debug.Log($"💀 플레이어 {cornerTracker.playerId}가 맵 경계를 벗어남! 위치: ({gridPosition.x}, {gridPosition.y})");
                    if (GameController.Instance != null)
                    {
                        GameController.Instance.KillPlayer(cornerTracker.playerId);
                    }
                    return; // 사망 처리 후 더 이상 진행하지 않음
                }

                int currentTile = mapManager.GetTile(gridPosition);
                bool isInsideOwnedArea = currentTile == cornerTracker.playerId;

                // 내 영역 밖으로 나갈 때 점 추가
                if (wasInsideOwnedArea && !isInsideOwnedArea)
                {
                    Debug.Log("📌 내 영역을 벗어남 - 이전 점과 현재 점 추가");
                    Vector2Int previousPos = gridPosition - direction; // 이전 위치 (내 땅)
                    cornerTracker?.AddCorner(previousPos);            // 이전 점 추가
                    cornerTracker?.AddCorner(gridPosition);
                    Debug.Log($"추가된 점들: 이전=({previousPos.x}, {previousPos.y}), 현재=({gridPosition.x}, {gridPosition.y})");
                    if (trail != null) trail.trailActive = true;
                }

                // 내 영역 안으로 들어올 때 코너 추가 및 폐곡선 검사
                if (!wasInsideOwnedArea && isInsideOwnedArea)
                {
                    Debug.Log("📌 내 영역 안으로 들어옴 - 코너 추가 및 폐곡선 검사");
                    cornerTracker?.AddCorner(gridPosition);
                    loopDetector?.CheckLoop(cornerTracker);
                    // cornerTracker?.DisplayCornersFor1Second();
                    trail?.ResetTrail();
                    if (trail != null) trail.trailActive = false;
                }

                wasInsideOwnedArea = isInsideOwnedArea;
            }
        }
    }

    // 선을 밟았을 때 선의 주인을 죽이는 공통 로직
    // 각 플레이어마다 on
    protected void CheckTrailCollision(Collider2D other)
    {
        float distance = Vector2.Distance(transform.position, other.transform.position);
        if (distance > 1f) return; // 너무 멀면 무시

        var trail = other.GetComponent<LineTrailWithCollision>();
        if (trail == null || trail.cornerTracker == null) return;

        int myId = cornerTracker.playerId; // ✅ safer
        int trailOwner = trail.cornerTracker.playerId;


        if (GameController.Instance != null)
        {
            Debug.Log($"💥 플레이어 {myId}가 플레이어 {trailOwner}의 선을 밟음 → {trailOwner} 죽음!");
            GameController.Instance.KillPlayer(trailOwner); // 선의 주인을 죽임
        }
    }
}
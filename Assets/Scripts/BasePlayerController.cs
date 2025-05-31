using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BasePlayerController : MonoBehaviour
{
    // PlayerController.cs의 변수들과 대응
    public float moveSpeed = 100f;
    public bool isMainPlayer = false; // 새로 추가된 변수

    // PlayerController.cs의 private 변수들이 protected로 변경됨
    protected Vector2Int gridPosition;        // private Vector2Int gridPosition;
    public Vector2Int direction;           // private Vector2Int direction = Vector2Int.zero; // protected에서 public으로 변경
    protected Vector2Int queuedDirection;     // private Vector2Int queuedDirection = Vector2Int.zero;
    protected bool isMoving;                  // private bool isMoving = false;
    protected Vector3 targetPosition;         // private Vector3 targetPosition;

    // PlayerController.cs의 컴포넌트 참조들
    protected LineTrailWithCollision trail;   // private LineTrailWithCollision trail;
    protected CornerPointTracker cornerTracker; // private CornerPointTracker cornerTracker;
    protected LoopDetector loopDetector;     // private LoopDetector loopDetector;
    protected MapManager mapManager;          // private MapManager mapManager;
    protected bool wasInsideOwnedArea;        // private bool wasInsideOwnedArea = false;

    // PlayerController.cs의 Start() 함수에 대응
    protected virtual void Start()
    {
        gridPosition = Vector2Int.RoundToInt(transform.position);
        transform.position = (Vector2)gridPosition;
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

        wasInsideOwnedArea = mapManager.GetTile(gridPosition) == cornerTracker.playerId;
    }

    // PlayerController.cs에서 컴포넌트 초기화 부분을 분리
    protected virtual void InitializeComponents()
    {
        trail = GetComponent<LineTrailWithCollision>();
        cornerTracker = GetComponent<CornerPointTracker>();
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
        // Update() 내부의 이동 관련 코드
        // - 방향 전환 체크
        // - 이동 처리
        // - 영역 진입/이탈 체크
        // - 궤적 활성화/비활성화
    }
}
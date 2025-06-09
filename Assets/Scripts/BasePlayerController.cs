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
        targetPosition = transform.position; InitializeComponents();

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

    /// <summary>
    /// 플레이어를 완전히 새로 스폰시킵니다 (ML-Agent 재시작용)
    /// </summary>
    public virtual void FullRespawn(Vector2Int newPosition)
    {
        Debug.Log($"플레이어 {cornerTracker?.playerId} 완전 재스폰 시작: 위치 {newPosition}");

        // 1. 위치 이동
        gridPosition = newPosition;
        transform.position = new Vector3(gridPosition.x, gridPosition.y, -1f);
        targetPosition = transform.position;

        // 2. 이동 상태 초기화
        direction = Vector2Int.zero;
        queuedDirection = Vector2Int.zero;
        isMoving = false;
        wasInsideOwnedArea = true; // 새로 스폰될 때는 자신의 영토에서 시작

        // 3. 궤적 초기화
        if (trail != null)
        {
            trail.ResetTrail();
            trail.trailActive = false;
        }
        // 4. 코너 포인트 초기화
        if (cornerTracker != null)
        {
            cornerTracker.Clear();
        }

        // 5. 맵에서 새 영토 생성
        if (mapManager != null && cornerTracker != null)
        {
            mapManager.RespawnPlayerTerritory(cornerTracker.playerId, newPosition);
        }

        Debug.Log($"플레이어 {cornerTracker?.playerId} 완전 재스폰 완료");
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
        HandleInput();        // 방향이 바뀔 때만 코너 저장 (180도 회전 제한 제거)
        if (!isMoving && queuedDirection != Vector2Int.zero)
        {
            // 내 영역 밖에 있을 때만 코너 저장
            if (direction != Vector2Int.zero && queuedDirection != direction && !wasInsideOwnedArea)
            {
                cornerTracker?.AddCorner(gridPosition);
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
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;                // 맵 경계 체크 - 경계를 벗어나면 사망
                if (!mapManager.InBounds(gridPosition))
                {
                    if (GameController.Instance != null)
                    {
                        GameController.Instance.KillPlayer(cornerTracker.playerId);
                    }
                    return; // 사망 처리 후 더 이상 진행하지 않음
                }
                int currentTile = mapManager.GetTile(gridPosition);
                bool isInsideOwnedArea = currentTile == cornerTracker.playerId;                // 항상 궤적 충돌 체크 (내 영역 안에서도 상대방 궤적을 끊을 수 있음)
                int existingTrail = mapManager.GetTrail(gridPosition);
                if (existingTrail > 0)
                {
                    if (existingTrail == cornerTracker.playerId)
                    {
                        // 자신의 꼬리를 밟으면 자신이 죽음
                        Debug.Log($"플레이어 {cornerTracker.playerId}: 자신의 꼬리를 밟아 사망!");
                        if (GameController.Instance != null)
                        {
                            GameController.Instance.KillPlayer(cornerTracker.playerId);
                        }
                        return; // 사망 처리 후 더 이상 진행하지 않음
                    }
                    else
                    {
                        // 다른 플레이어의 궤적을 밟으면 해당 플레이어가 죽음
                        Debug.Log($"플레이어 {cornerTracker.playerId}: 플레이어 {existingTrail}의 궤적을 끊음!");
                        if (GameController.Instance != null)
                        {
                            GameController.Instance.KillPlayer(existingTrail);
                        }
                        // 궤적을 끊었으므로 해당 위치의 궤적 제거
                        mapManager.SetTrail(gridPosition, 0);
                    }
                }

                // 내 영역 밖에 있을 때만 자신의 궤적 설정
                if (!isInsideOwnedArea)
                {
                    mapManager.SetTrail(gridPosition, cornerTracker.playerId);
                }// 내 영역 밖으로 나갈 때 점 추가
                if (wasInsideOwnedArea && !isInsideOwnedArea)
                {
                    Vector2Int previousPos = gridPosition - direction; // 이전 위치 (내 땅)
                    cornerTracker?.AddCorner(previousPos);            // 이전 점 추가
                    cornerTracker?.AddCorner(gridPosition);
                    if (trail != null) trail.trailActive = true;
                }                // 내 영역 안으로 들어올 때 코너 추가 및 폐곡선 검사
                if (!wasInsideOwnedArea && isInsideOwnedArea)
                {
                    cornerTracker?.AddCorner(gridPosition);
                    loopDetector?.CheckLoop(cornerTracker);
                    trail?.ResetTrail();
                    if (trail != null) trail.trailActive = false;

                    // 내 영역으로 들어올 때 내 궤적 제거
                    mapManager.ClearPlayerTrails(cornerTracker.playerId);
                }

                // 🔧 이전 위치와 현재 위치가 모두 내 영역일 때 꼭짓점 집합 정리
                if (wasInsideOwnedArea && isInsideOwnedArea)
                {
                    // 꼭짓점이 1개 이상 남아있다면 비우기 (초기 위치 문제 해결)
                    if (cornerTracker?.cornerPoints.Count > 0)
                    {
                        Debug.Log($"[BasePlayerController] 플레이어 {cornerTracker.playerId}: 영역 내부 이동 중 꼭짓점 집합 정리 (기존 {cornerTracker.cornerPoints.Count}개)");
                        cornerTracker.Clear();
                    }
                }

                wasInsideOwnedArea = isInsideOwnedArea;
            }
        }
    }
}
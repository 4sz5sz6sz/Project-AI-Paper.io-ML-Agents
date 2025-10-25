using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BasePlayerController : MonoBehaviour
{
    // PlayerController.cs의 변수들과 대응

    public bool isRespawningNow = false; 
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
    protected MyAgent agent;

    // PlayerController.cs의 Start() 함수에 대응
    protected virtual void Start()
    {
        gridPosition = Vector2Int.RoundToInt(transform.position);
        transform.position = new Vector3(gridPosition.x, gridPosition.y, -1f);
        targetPosition = transform.position; InitializeComponents();
        // wasInsideOwnedArea = mapManager.GetTile(gridPosition) == cornerTracker.playerId;

        Vector2Int spawnPos = GetPlayerSpawnPosition(cornerTracker?.playerId ?? 1);
        FullRespawn(spawnPos);
    }

    // PlayerController.cs에서 컴포넌트 초기화 부분을 분리
    protected virtual void InitializeComponents()
    {
        if (trail == null)
        {
            Transform trailObj = transform.Find("TrailDrawer");
            if (trailObj != null)
            {
                trail = trailObj.GetComponent<LineTrailWithCollision>();
            }
        }

        if (cornerTracker == null)
            cornerTracker = GetComponent<CornerPointTracker>();

        if (loopDetector == null)
            loopDetector = FindFirstObjectByType<LoopDetector>();

        if (mapManager == null)
            mapManager = FindFirstObjectByType<MapManager>();
    }

    /// <summary>
    /// 플레이어를 완전히 새로 스폰시킵니다 (ML-Agent 재시작용)
    /// </summary>
    public virtual void FullRespawn(Vector2Int newPosition)
    {        // 모든 플레이어에 대해 리스폰 딜레이 적용
        isRespawningNow = true;
        
        // 기존 리스폰 로직 실행
        gridPosition = newPosition;
        transform.position = new Vector3(gridPosition.x, gridPosition.y, -1f);
        targetPosition = transform.position;
        direction = Vector2Int.zero;
        queuedDirection = Vector2Int.zero;
        isMoving = false;
        wasInsideOwnedArea = true;

        trail.ResetTrail();
        trail.trailActive = false;

        if (mapManager != null)
        {
            mapManager.ClearPlayerTrails(cornerTracker?.playerId ?? -1);
            mapManager.ClearPlayerTerritory(cornerTracker?.playerId ?? -1);
        }

        if (cornerTracker != null)
        {
            cornerTracker.Clear();
        }

        if (mapManager != null && cornerTracker != null)
        {
            mapManager.RespawnPlayerTerritory(cornerTracker.playerId, newPosition);
        }        // AI는 2초, 플레이어는 1초 딜레이
        // 스프라이트 투명도 설정
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        if (cornerTracker?.playerId == 1)
        {
            Invoke("EnableMovement", 1f); // 플레이어는 1초
            // 플레이어는 투명도 변경 없음
        }
        else
        {
            // AI는 50% 투명도로 설정
            foreach (var renderer in renderers)
            {
                var color = renderer.color;
                color.a = 0.5f;  // 50% 투명도
                renderer.color = color;
            }
            Invoke("EnableMovement", 2f); // AI는 2초
        }
    }    private void EnableMovement()
    {
        isRespawningNow = false;
        
        // AI 플레이어의 경우 투명도 원복
        if (cornerTracker?.playerId != 1)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (var renderer in renderers)
            {
                var color = renderer.color;
                color.a = 1f;  // 완전 불투명으로 복구
                renderer.color = color;
            }
        }
    }

    // PlayerController.cs의 Update() 함수에 대응
    protected virtual void Update()
    {
        HandleMovement();  // Update() 내부의 이동 처리 부분
    }

    // PlayerController.cs의 키보드 입력 처리 부분을 추상화
    protected abstract void HandleInput();

    protected Vector2Int GetPlayerSpawnPosition(int playerId)
    {
        Vector2Int spawnPos;
        Debug.Log($"플레이어 {playerId} 스폰 위치 결정");
        switch (playerId)
        {
            case 1:
                spawnPos = new Vector2Int(30, 30);
                break;
            case 2:
                spawnPos = new Vector2Int(30, 70);
                break;
            case 3:
                spawnPos = new Vector2Int(70, 30);
                break;
            case 4:
                spawnPos = new Vector2Int(70, 70);
                break;
            default:
                spawnPos = new Vector2Int(70, 20); // 예외 처리용 중앙 스폰
                break;
        }
        return spawnPos;
    }

    // PlayerController.cs의 이동 처리 로직을 분리
    protected virtual void HandleMovement()
    {
        if (isRespawningNow) return; // 리스폰 중이면 움직임 처리하지 않음

        HandleInput();        // 방향이 바뀔 때만 코너 저장 (180도 회전 제한 제거)
        if (agent == null && cornerTracker?.playerId != 1) // 플레이어 1은 ML-Agent가 아니므로 예외 처리
        {
            agent = GetComponent<MyAgent>();
            Debug.Log($"플레이어 {cornerTracker?.playerId} 에이전트 컴포넌트 찾음: {agent != null}");
        }

        //격자 칸에 도달 했을 때만 한번씩 실행되는 부분
        //새로운 점을 지정하고 그 방향으로 움직이도록 함 
        if (!isMoving && queuedDirection != Vector2Int.zero)
        {
            // 내 영역 밖에 있을 때만 코너 저장
            if (direction != Vector2Int.zero && queuedDirection != direction && !wasInsideOwnedArea)
            {
                cornerTracker?.AddCorner(gridPosition);
            }

            //매 칸에 도착 했을 때 보상함수 주도록 하기 
            if (agent != null) // 플레이어 1은 ML-Agent가 아니므로 예외 처리
            {
                agent.RequestDecision(); // ML-Agent에게 결정 요청
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
                        GameController.Instance.KillPlayer(cornerTracker.playerId, 1); // 1은 맵 경계 충돌 사망

                        // 플레이어 스폰 위치 가져오기
                        Vector2Int spawnPos = GetPlayerSpawnPosition(cornerTracker.playerId);
                        FullRespawn(spawnPos);
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

                        if (GameController.Instance != null)
                        {
                            GameController.Instance.KillPlayer(cornerTracker.playerId, 2); // 2는 자신의 꼬리 밟음 사망
                            // 플레이어 스폰 위치 가져오기
                            Vector2Int spawnPos = GetPlayerSpawnPosition(cornerTracker.playerId);
                            FullRespawn(spawnPos);
                        }
                        return; // 사망 처리 후 더 이상 진행하지 않음
                    }
                    else
                    {
                        // 다른 플레이어의 궤적을 밟으면 해당 플레이어가 죽음
                        if (GameController.Instance != null)
                        {
                            Debug.Log($"플레이어 {cornerTracker.playerId}: 플레이어 {existingTrail}의 궤적을 끊음!");
                            GameController.Instance.KillPlayer(existingTrail, 3, cornerTracker.playerId); // 3은 다른 플레이어에게 궤적을 밟혀 사망, 마지막 파라미터는 처치한 플레이어 ID

                            // existingTrail의 주인인 플레이어의 BasePlayerController를 찾아서 respawn
                            GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");

                            foreach (GameObject player in allPlayers)
                            {
                                var tracker = player.GetComponent<CornerPointTracker>();

                                if (tracker != null && tracker.playerId == existingTrail)
                                {
                                    var aiController = player.GetComponent<AIPlayerController>();
                                    var playerController = player.GetComponent<PlayerController>();
                                    
                                    if (aiController != null)
                                    {
                                        Vector2Int otherSpawnPos = aiController.GetPlayerSpawnPosition(tracker.playerId);
                                        aiController.FullRespawn(otherSpawnPos);
                                    }
                                    else if (playerController != null)
                                    {
                                        Vector2Int otherSpawnPos = playerController.GetPlayerSpawnPosition(tracker.playerId);
                                        playerController.FullRespawn(otherSpawnPos);
                                    }
                                    break;
                                }
                            }
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
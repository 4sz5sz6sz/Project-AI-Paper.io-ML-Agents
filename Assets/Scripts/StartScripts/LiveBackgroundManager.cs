using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 메인 메뉴 배경에 실제 게임플레이를 블러 처리해서 표시합니다.
/// JM 씬을 Additive로 로드하여 배경으로 사용하고, UI는 StartScene에 표시합니다.
/// </summary>
public class LiveBackgroundManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("배경으로 사용할 게임 씬 이름")]
    public string gameSceneName = "JM";
    
    [Tooltip("배경 게임의 타임스케일 (느리게 재생)")]
    [Range(0.1f, 1f)]
    public float backgroundTimeScale = 0.5f;
    
    [Tooltip("배경 블러 강도 (Post-Processing)")]
    [Range(0f, 10f)]
    public float blurIntensity = 5f;

    [Header("UI Overlay")]
    [Tooltip("UI 오버레이 어두운 정도")]
    [Range(0f, 0.95f)]
    public float overlayDarkness = 0.6f;
    
    public GameObject uiOverlay; // 어두운 오버레이 패널

    [Header("Camera Settings")]
    [Tooltip("AI 플레이어 전환 간격 (초)")]
    public float cameraSwitchInterval = 2f;
    
    [Tooltip("추적할 AI 플레이어 ID (2, 3, 4)")]
    public int[] aiPlayerIDs = new int[] { 2, 3, 4 };

    private Scene backgroundScene;
    private bool isBackgroundLoaded = false;
    private int currentAIIndex = 0;
    private float timeSinceLastSwitch = 0f;

    void Start()
    {
        StartCoroutine(LoadBackgroundScene());
        SetupUIOverlay();
    }

    void Update()
    {
        // 배경 씬이 로드되면 자동으로 AI 플레이어 전환
        if (isBackgroundLoaded)
        {
            timeSinceLastSwitch += Time.unscaledDeltaTime;
            
            if (timeSinceLastSwitch >= cameraSwitchInterval)
            {
                SwitchToNextAIPlayer();
                timeSinceLastSwitch = 0f;
            }
        }
    }

    /// <summary>
    /// 다음 AI 플레이어로 카메라 전환 (GameController의 키 입력 시뮬레이션)
    /// </summary>
    void SwitchToNextAIPlayer()
    {
        currentAIIndex = (currentAIIndex + 1) % aiPlayerIDs.Length;
        int targetPlayerID = aiPlayerIDs[currentAIIndex];
        
        // GameController의 SwitchCameraToPlayer를 호출하기 위해
        // 해당 플레이어 키를 시뮬레이션 (2, 3, 4번 키)
        StartCoroutine(SimulateKeyPress(targetPlayerID));
    }

    /// <summary>
    /// 키 입력 시뮬레이션 - GameController가 처리하도록
    /// </summary>
    IEnumerator SimulateKeyPress(int playerID)
    {
        // GameController의 HandleCameraControl이 Input.GetKeyDown을 감지하도록
        // 배경 씬의 GameController에게 직접 카메라 전환 요청
        GameController gameController = FindBackgroundGameController();
        
        if (gameController != null)
        {
            // Reflection으로 SwitchCameraToPlayer 호출
            var method = typeof(GameController).GetMethod("SwitchCameraToPlayer", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (method != null)
            {
                method.Invoke(null, new object[] { playerID });
            }
        }
        
        yield return null;
    }

    /// <summary>
    /// 배경 씬의 GameController 찾기
    /// </summary>
    GameController FindBackgroundGameController()
    {
        if (!isBackgroundLoaded) return null;
        
        GameObject[] rootObjects = backgroundScene.GetRootGameObjects();
        foreach (GameObject obj in rootObjects)
        {
            GameController gc = obj.GetComponentInChildren<GameController>();
            if (gc != null) return gc;
        }
        return null;
    }

    IEnumerator LoadBackgroundScene()
    {
        // JM 씬을 Additive 모드로 로드 (현재 씬과 함께 실행)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Additive);
        
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        backgroundScene = SceneManager.GetSceneByName(gameSceneName);
        isBackgroundLoaded = true;

        // 배경 게임을 느리게 실행
        Time.timeScale = backgroundTimeScale;

        Debug.Log($"[LiveBackground] {gameSceneName} 씬이 배경으로 로드되었습니다.");

        // 배경 씬의 중복 컴포넌트 제거
        RemoveDuplicateComponents();

        // 배경 카메라 설정 조정
        AdjustBackgroundCamera();

        // 배경 씬의 UI를 숨기기 (게임 UI가 보이지 않도록)
        HideBackgroundUI();
    }

    /// <summary>
    /// 배경 씬의 중복 컴포넌트 제거 (Audio Listener, Event System)
    /// </summary>
    void RemoveDuplicateComponents()
    {
        if (!isBackgroundLoaded) return;

        GameObject[] rootObjects = backgroundScene.GetRootGameObjects();
        
        foreach (GameObject obj in rootObjects)
        {
            // Audio Listener 제거
            AudioListener[] listeners = obj.GetComponentsInChildren<AudioListener>(true);
            foreach (var listener in listeners)
            {
                Destroy(listener);
                Debug.Log($"[LiveBackground] '{listener.gameObject.name}'의 Audio Listener를 제거했습니다.");
            }

            // Event System 제거
            UnityEngine.EventSystems.EventSystem[] eventSystems = obj.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true);
            foreach (var eventSystem in eventSystems)
            {
                Destroy(eventSystem.gameObject);
                Debug.Log($"[LiveBackground] '{eventSystem.gameObject.name}' Event System을 제거했습니다.");
            }
        }
    }

    /// <summary>
    /// 배경 씬의 카메라를 StartScene 카메라보다 뒤로 배치
    /// </summary>
    void AdjustBackgroundCamera()
    {
        if (!isBackgroundLoaded) return;

        GameObject[] rootObjects = backgroundScene.GetRootGameObjects();
        
        foreach (GameObject obj in rootObjects)
        {
            Camera cam = obj.GetComponent<Camera>();
            if (cam != null && cam != Camera.main)
            {
                // 배경 카메라의 Depth를 낮춰서 뒤로 보냄
                cam.depth = -10;
                
                // 배경만 렌더링하도록 설정 (UI는 제외)
                cam.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
                
                Debug.Log($"[LiveBackground] 배경 카메라 '{obj.name}' Depth를 -10으로 설정했습니다.");
            }
        }

        // StartScene의 메인 카메라 Depth 확인
        if (Camera.main != null)
        {
            Camera.main.depth = 0;
            Camera.main.clearFlags = CameraClearFlags.Depth; // 배경 카메라 위에 렌더링
        }
    }

    void SetupUIOverlay()
    {
        if (uiOverlay != null)
        {
            var image = uiOverlay.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                Color overlayColor = Color.black;
                overlayColor.a = overlayDarkness;
                image.color = overlayColor;
            }
        }
    }

    /// <summary>
    /// 배경 씬의 UI Canvas를 숨김 (점수, 게임 UI 등)
    /// </summary>
    void HideBackgroundUI()
    {
        if (!isBackgroundLoaded) return;

        GameObject[] rootObjects = backgroundScene.GetRootGameObjects();
        
        foreach (GameObject obj in rootObjects)
        {
            // Canvas 오브젝트 찾아서 비활성화
            Canvas canvas = obj.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = false;
                Debug.Log($"[LiveBackground] 배경 씬의 Canvas '{obj.name}'를 숨겼습니다.");
            }
        }
    }

    /// <summary>
    /// 게임 시작 시 호출 (Start 버튼 클릭 시)
    /// </summary>
    public void StartGame()
    {
        // 타임스케일 복원
        Time.timeScale = 1f;

        // 배경 씬 언로드
        if (isBackgroundLoaded)
        {
            SceneManager.UnloadSceneAsync(backgroundScene);
        }

        // 실제 게임 씬으로 이동
        SceneManager.LoadScene(gameSceneName);
    }

    void OnDestroy()
    {
        // 타임스케일 복원
        Time.timeScale = 1f;
    }
}

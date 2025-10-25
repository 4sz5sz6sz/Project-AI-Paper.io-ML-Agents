using UnityEngine;

/// <summary>
/// 치트키 입력 감지 및 관리
/// "godmode" 입력 시 카메라 전환 기능 활성화/비활성화
/// "timestop" 입력 시 타이머 정지
/// </summary>
public class CheatCodeManager : MonoBehaviour
{
    private string inputBuffer = "";
    private const string CHEAT_CODE_GODMODE = "godmode";
    private const string CHEAT_CODE_TIMESTOP = "timestop";
    private const float INPUT_TIMEOUT = 2f; // 2초 동안 입력 없으면 버퍼 초기화
    private float lastInputTime = 0f;

    // static이 아닌 일반 변수로 변경 (씬마다 독립적)
    private bool isGodModeEnabled = false;
    private bool isTimeStopEnabled = false;
    
    // 외부에서 접근할 수 있는 속성
    public static bool IsGodModeEnabled 
    { 
        get 
        {
            CheatCodeManager instance = FindObjectOfType<CheatCodeManager>();
            return instance != null && instance.isGodModeEnabled;
        }
    }

    public static bool IsTimeStopEnabled
    {
        get
        {
            CheatCodeManager instance = FindObjectOfType<CheatCodeManager>();
            return instance != null && instance.isTimeStopEnabled;
        }
    }

    void Start()
    {
        // 게임 시작 시 항상 비활성화 상태로 초기화
        isGodModeEnabled = false;
        isTimeStopEnabled = false;
        Debug.Log("[CHEAT] 치트키 시스템 초기화 - God Mode: OFF, Time Stop: OFF");
    }

    void Update()
    {
        // 입력 타임아웃 체크
        if (Time.time - lastInputTime > INPUT_TIMEOUT && inputBuffer.Length > 0)
        {
            inputBuffer = "";
        }

        // Shift 키가 눌려있을 때만 입력 감지
        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Shift 키를 떼면 버퍼 초기화
        if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
        {
            if (inputBuffer.Length > 0)
            {
                Debug.Log("[CHEAT] Shift 키 해제 - 버퍼 초기화됨");
                inputBuffer = "";
            }
        }

        if (isShiftPressed && Input.anyKeyDown)
        {
            foreach (char c in Input.inputString)
            {
                if (char.IsLetter(c))
                {
                    inputBuffer += char.ToLower(c);
                    lastInputTime = Time.time;
                    
                    // 실시간 버퍼 상태 출력
                    // Debug.Log($"[CHEAT] 버퍼: '{inputBuffer}' (목표: '{CHEAT_CODE_GODMODE}' 또는 '{CHEAT_CODE_TIMESTOP}')");

                    // 치트 코드 확인
                    if (inputBuffer.Contains(CHEAT_CODE_GODMODE))
                    {
                        ToggleGodMode();
                        inputBuffer = "";
                    }
                    else if (inputBuffer.Contains(CHEAT_CODE_TIMESTOP))
                    {
                        ToggleTimeStop();
                        inputBuffer = "";
                    }

                    // 버퍼가 너무 길어지면 초기화
                    int maxLength = Mathf.Max(CHEAT_CODE_GODMODE.Length, CHEAT_CODE_TIMESTOP.Length);
                    if (inputBuffer.Length > maxLength + 5)
                    {
                        Debug.Log("[CHEAT] 버퍼 초기화됨 (너무 길어짐)");
                        inputBuffer = "";
                    }
                }
            }
        }
        
        // Shift 상태 표시 (디버그용)
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            Debug.Log("[CHEAT] Shift 키 눌림! 치트 입력 모드 활성화");
        }
    }

    void ToggleGodMode()
    {
        isGodModeEnabled = !isGodModeEnabled;
        
        if (isGodModeEnabled)
        {
            Debug.Log("🎮 [CHEAT] God Mode 활성화! 카메라 전환 키(1,2,3,4) 사용 가능");
        }
        else
        {
            Debug.Log("🎮 [CHEAT] God Mode 비활성화! 카메라 전환 키 잠김");
        }
    }

    void ToggleTimeStop()
    {
        isTimeStopEnabled = !isTimeStopEnabled;
        
        if (isTimeStopEnabled)
        {
            Debug.Log("⏱️ [CHEAT] Time Stop 활성화! 타이머가 멈췄습니다.");
        }
        else
        {
            Debug.Log("⏱️ [CHEAT] Time Stop 비활성화! 타이머가 재개됩니다.");
        }
    }

    void OnGUI()
    {
        int yOffset = 10;
        
        // God Mode 표시
        if (isGodModeEnabled)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(10, yOffset, 300, 30), "GOD MODE: ON (1,2,3,4 키 활성화)");
            yOffset += 30;
        }
        
        // Time Stop 표시
        if (isTimeStopEnabled)
        {
            GUI.color = Color.cyan;
            GUI.Label(new Rect(10, yOffset, 300, 30), "TIME STOP: ON (타이머 멈춤)");
            yOffset += 30;
        }
        
        // 버퍼 상태 실시간 표시
        if (inputBuffer.Length > 0)
        {
            GUI.color = Color.cyan;
            GUI.Label(new Rect(10, yOffset, 400, 30), $"치트 입력 중: {inputBuffer}");
        }
    }
}

using UnityEngine;

/// <summary>
/// 치트키 입력 감지 및 관리
/// "godmode" 입력 시 카메라 전환 기능 활성화/비활성화
/// </summary>
public class CheatCodeManager : MonoBehaviour
{
    private string inputBuffer = "";
    private const string CHEAT_CODE = "godmode";
    private const float INPUT_TIMEOUT = 2f; // 2초 동안 입력 없으면 버퍼 초기화
    private float lastInputTime = 0f;

    public static bool IsGodModeEnabled { get; private set; } = false;

    void Update()
    {
        // 입력 타임아웃 체크
        if (Time.time - lastInputTime > INPUT_TIMEOUT && inputBuffer.Length > 0)
        {
            inputBuffer = "";
        }

        // Shift 키가 눌려있을 때만 입력 감지
        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (isShiftPressed && Input.anyKeyDown)
        {
            foreach (char c in Input.inputString)
            {
                if (char.IsLetter(c))
                {
                    inputBuffer += char.ToLower(c);
                    lastInputTime = Time.time;
                    
                    // 실시간 버퍼 상태 출력
                    Debug.Log($"[CHEAT] 버퍼: '{inputBuffer}' (목표: '{CHEAT_CODE}')");

                    // 치트 코드 확인
                    if (inputBuffer.Contains(CHEAT_CODE))
                    {
                        ToggleGodMode();
                        inputBuffer = "";
                    }

                    // 버퍼가 너무 길어지면 초기화
                    if (inputBuffer.Length > CHEAT_CODE.Length + 5)
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
        IsGodModeEnabled = !IsGodModeEnabled;
        
        if (IsGodModeEnabled)
        {
            Debug.Log("🎮 [CHEAT] God Mode 활성화! 카메라 전환 키(1,2,3,4) 사용 가능");
        }
        else
        {
            Debug.Log("🎮 [CHEAT] God Mode 비활성화! 카메라 전환 키 잠김");
        }
    }

    void OnGUI()
    {
        // 디버그용 표시 (배포 시 제거 가능)
        if (IsGodModeEnabled)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(10, 10, 300, 30), "🎮 GOD MODE: ON (1,2,3,4 키 활성화)");
        }
        
        // 버퍼 상태 실시간 표시
        if (inputBuffer.Length > 0)
        {
            GUI.color = Color.cyan;
            GUI.Label(new Rect(10, 40, 400, 30), $"치트 입력 중: {inputBuffer}");
        }
    }
}

using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI text;  // Inspector에 할당

    private int score = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        UpdateText();
    }

    /// <summary>
    /// 외부에서 “총 점수(총 초록 타일 개수)”를 설정할 때 호출
    /// </summary>
    public void SetScore(int total)
    {
        score = total;
        UpdateText();
    }

    private void UpdateText()
    {
        text.text = "Score :" + score;
    }
}

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PersistentTrail : MonoBehaviour
{
    [Header("궤적 설정")]
    [Tooltip("궤적이 사라지지 않도록 무한대로 설정")]
    public float trailTime = Mathf.Infinity;
    [Tooltip("궤적 너비")]
    public float trailWidth = 0.8f;
    [Tooltip("궤적 투명도(0~1)")]
    [Range(0f,1f)]
    public float alpha = 0.7f;

    void Start()
    {
        //TrailRenderer 추가
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth   = trailWidth;
        trail.numCapVertices = 5;
        trail.numCornerVertices = 5;

        //기본 스프라이트 쉐이더 사용
        trail.material = new Material(Shader.Find("Sprites/Default"));

        //플레이어 색상 읽어오기
        Color playerColor = GetComponent<SpriteRenderer>().color;
        playerColor.a = 1f;  //궤적 색상은 불투명 색으로 지정하고, 알파는 Gradient에서 제어

        //Gradient 설정 (항상 같은 투명도)
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(playerColor, 0f),
                new GradientColorKey(playerColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha, 1f)
            }
        );
        trail.colorGradient = gradient;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
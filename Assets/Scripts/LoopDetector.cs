// LoopDetector.cs
using UnityEngine;

public class LoopDetector : MonoBehaviour
{
    public MapManager mapManager;

    public void CheckLoop(CornerPointTracker tracker)
    {
        var points = tracker.GetPoints();
        // if (points.Count < 2)
        // {
        //     Debug.Log("⛔ 폐곡선 검사 실패: 점 개수 부족");
        //     return;
        // }

        Vector2Int last = points[^1];
        int tile = mapManager.GetTile(last);

        Debug.Log($"🔍 폐곡선 검사: 마지막점={last}, tile={tile}, playerId={tracker.playerId}");

        if (tile == tracker.playerId)
        {
            Debug.Log("✅ 폐곡선 충족! FinalizePolygon 호출");
            tracker.FinalizePolygon();
        }
        else
        {
            Debug.Log("❌ 폐곡선 조건 불충족: 내 땅 아님");
        }
    }
}

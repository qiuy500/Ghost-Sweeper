using UnityEngine;

/// <summary>
/// 俯視角攝影機：預設固定在網格中心上方；勾選 followPlayer 則平滑跟隨玩家。
/// pitch = 90 為完全垂直俯視；改成 60~75 會有斜角 3D 感。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("引用")]
    public GridManager grid;
    public Transform target;            // 玩家

    [Header("設定")]
    public bool followPlayer = false;   // false = 固定看整張地圖
    [Range(30f, 90f)] public float pitch = 90f;   // 俯角
    public float distance = 13f;        // 與焦點的距離
    public float smooth = 6f;           // 跟隨平滑度

    private void LateUpdate()
    {
        if (grid == null) return;

        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);

        // 焦點：玩家或地圖中心
        Vector3 focus = (followPlayer && target != null) ? target.position : grid.GridCenter;

        // 攝影機位於焦點的「後上方」：forward 向下時，-forward 就是向上
        Vector3 desiredPos = focus - rot * Vector3.forward * distance;

        transform.rotation = rot;
        transform.position = followPlayer
            ? Vector3.Lerp(transform.position, desiredPos, smooth * Time.deltaTime)
            : desiredPos;
    }
}

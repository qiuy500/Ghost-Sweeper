using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家控制：WASD / 方向鍵在網格間平滑移動（一次一格），
/// 右鍵（滑鼠指向格子）或 Space（正前方格子）放置 / 取消警告旗幟。
/// 使用舊版 Input Manager（UnityEngine.Input），請確認
/// Project Settings > Player > Active Input Handling 為 "Both" 或 "Input Manager (Old)"。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("引用")]
    public GridManager grid;
    public Camera mainCamera;          // 留空會自動使用 Camera.main
    public Transform visualRoot;       // 玩家外觀根物件（會依面向旋轉，內含 Body 與 Nose）

    [Header("移動")]
    public float moveSpeed = 5f;               // 每秒移動幾格
    public bool blockFlaggedTiles = true;      // 有旗幟的格子不可踏入（需先取消旗幟）

    [Header("插旗")]
    public int flagRange = 2;                  // 滑鼠插旗的有效範圍（格，棋盤距離）

    /// <summary>玩家目前所在格子座標（Vector2Int.y 代表 z）。</summary>
    public Vector2Int GridPos { get; private set; }

    /// <summary>玩家面向（格子方向）。</summary>
    public Vector2Int Facing { get; private set; } = Vector2Int.up;

    /// <summary>是否接受輸入（Game Over / Victory 時關閉）。</summary>
    public bool InputEnabled { get; set; }

    /// <summary>抵達新格子時觸發（參數：該格 Tile）。</summary>
    public event System.Action<Tile> OnTileEntered;

    /// <summary>旗幟被切換時觸發（參數：Tile、切換後是否有旗幟）。</summary>
    public event System.Action<Tile, bool> OnFlagToggled;

    private bool isMoving = false;

    private void Update()
    {
        if (!InputEnabled || grid == null) return;
        if (isMoving) return;

        HandleMoveInput();
        HandleFlagInput();
    }

    // ================= 移動 =================

    private void HandleMoveInput()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D、左/右
        float v = Input.GetAxisRaw("Vertical");   // W/S、上/下

        // 一次只處理一個方向（水平優先），避免斜向移動
        Vector2Int dir = Vector2Int.zero;
        if (Mathf.Abs(h) > 0.5f) dir = new Vector2Int(h > 0f ? 1 : -1, 0);
        else if (Mathf.Abs(v) > 0.5f) dir = new Vector2Int(0, v > 0f ? 1 : -1);

        if (dir == Vector2Int.zero) return;

        // 先轉向（即使前方被擋住也能轉向，方便對該方向插旗）
        Facing = dir;
        UpdateFacingVisual();

        Vector2Int next = GridPos + dir;
        if (!CanEnter(next)) return;

        StartCoroutine(MoveTo(next));
    }

    private bool CanEnter(Vector2Int coord)
    {
        if (!grid.IsWalkable(coord.x, coord.y)) return false;

        if (blockFlaggedTiles)
        {
            Tile t = grid.GetTile(coord.x, coord.y);
            if (t != null && t.HasFlag) return false;
        }
        return true;
    }

    private IEnumerator MoveTo(Vector2Int next)
    {
        isMoving = true;
        Vector3 target = grid.GridToWorld(next.x, next.y);

        // 平滑移動到目標格
        while ((transform.position - target).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * grid.tileSize * Time.deltaTime);
            yield return null;
        }

        transform.position = target; // 精準吸附到格子中心
        GridPos = next;
        isMoving = false;

        // 通知外部（GameManager 判斷幽靈/鑰匙/出口；DetectorSystem 更新數字）
        if (OnTileEntered != null) OnTileEntered(grid.GetTile(next.x, next.y));
    }

    private void UpdateFacingVisual()
    {
        if (visualRoot == null) return;
        visualRoot.rotation = Quaternion.LookRotation(new Vector3(Facing.x, 0f, Facing.y));
    }

    // ================= 插旗 =================

    private void HandleFlagInput()
    {
        // Space：正前方格子
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryToggleFlag(GridPos + Facing);
        }
        // 滑鼠右鍵：滑鼠指向的格子
        else if (Input.GetMouseButtonDown(1))
        {
            Vector2Int coord;
            if (TryGetMouseGrid(out coord)) TryToggleFlag(coord);
        }
    }

    /// <summary>用射線與「地面平面 y=0」求交點，換算成格子座標（不需要 Collider）。</summary>
    private bool TryGetMouseGrid(out Vector2Int coord)
    {
        coord = Vector2Int.zero;

        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);

        float enter;
        if (!ground.Raycast(ray, out enter)) return false;

        coord = grid.WorldToGrid(ray.GetPoint(enter));
        return true;
    }

    private void TryToggleFlag(Vector2Int coord)
    {
        if (coord == GridPos) return; // 不能在自己腳下插旗

        int dist = Mathf.Max(Mathf.Abs(coord.x - GridPos.x), Mathf.Abs(coord.y - GridPos.y));
        if (dist > flagRange) return; // 超出範圍

        Tile tile = grid.GetTile(coord.x, coord.y);
        if (tile == null || tile.type == TileType.Wall) return;

        bool placed = tile.ToggleFlag();
        if (OnFlagToggled != null) OnFlagToggled(tile, placed);
    }

    // ================= 重置 =================

    /// <summary>把玩家放回起點並重置狀態（遊戲開始 / 重來時呼叫）。</summary>
    public void ResetToStart()
    {
        StopAllCoroutines();
        isMoving = false;

        GridPos = grid.StartCoord;
        Facing = Vector2Int.up;
        transform.position = grid.GridToWorld(GridPos.x, GridPos.y);
        UpdateFacingVisual();

        // 對起點格也觸發一次，讓偵測器顯示初始數字
        if (OnTileEntered != null) OnTileEntered(grid.GetTile(GridPos.x, GridPos.y));
    }
}

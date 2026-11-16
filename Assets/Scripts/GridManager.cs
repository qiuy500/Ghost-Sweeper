using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 網格管理器：負責生成可自訂長寬的網格地圖、隨機配置幽靈/鑰匙/出口/牆壁，
/// 並提供「周圍幽靈數」「最近幽靈距離」等查詢功能。
/// 注意：本物件請放在世界座標原點 (0,0,0)，旋轉 (0,0,0)，縮放 (1,1,1)。
/// 座標約定：格子座標 (x, z) 對應世界座標 (x * tileSize, 0, z * tileSize)；
///           程式中以 Vector2Int 儲存時，Vector2Int.y 代表 z。
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("網格尺寸")]
    public int width = 10;          // X 方向格數
    public int height = 10;         // Z 方向格數
    public float tileSize = 1f;     // 每格邊長（世界單位）

    [Header("物件數量")]
    public int ghostCount = 12;     // 幽靈數量
    public int wallCount = 6;       // 內部牆壁數量（外圍邊牆另外自動建立）

    [Header("玩家起點（格子座標）")]
    public Vector2Int startCoord = Vector2Int.zero;

    [Header("隨機設定")]
    public bool useFixedSeed = false;   // 勾選後每次都生成相同地圖，方便測試與 Demo
    public int seed = 12345;
    public int maxAttempts = 200;       // 為了保證「可通關」最多重試幾次

    [Header("視覺")]
    public bool glowKeyAndExit = true;  // 鑰匙與出口是否自發光（取消勾選 = 困難模式，需靠燈光才看得到）

    // ---- 內部資料 ----
    private Tile[,] tiles;
    private readonly List<Tile> ghostTiles = new List<Tile>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    public Vector2Int StartCoord { get { return startCoord; } }
    public int TotalGhosts { get { return ghostTiles.Count; } }

    /// <summary>網格中心的世界座標（給攝影機使用）。</summary>
    public Vector3 GridCenter
    {
        get { return new Vector3((width - 1) * tileSize * 0.5f, 0f, (height - 1) * tileSize * 0.5f); }
    }

    // ================= 生成 =================

    /// <summary>生成（或重新生成）整張地圖。</summary>
    public void GenerateGrid()
    {
        ClearGrid();

        // 參數保護
        width = Mathf.Max(4, width);
        height = Mathf.Max(4, height);
        startCoord.x = Mathf.Clamp(startCoord.x, 0, width - 1);
        startCoord.y = Mathf.Clamp(startCoord.y, 0, height - 1);

        // 候選位置：排除起點與其周圍 8 格（保證開局安全，且玩家一開始就有資訊可推理）
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (Mathf.Abs(x - startCoord.x) <= 1 && Mathf.Abs(z - startCoord.y) <= 1) continue;
                candidates.Add(new Vector2Int(x, z));
            }
        }

        // 數量保護：預留 2 格給鑰匙與出口
        int maxObjects = candidates.Count - 2;
        int ghosts = Mathf.Clamp(ghostCount, 0, maxObjects);
        int walls = Mathf.Clamp(wallCount, 0, maxObjects - ghosts);

        // 反覆隨機直到「起點 → 鑰匙、起點 → 出口」都存在不經過幽靈與牆的路徑
        TileType[,] map = null;
        for (int attempt = 0; attempt < Mathf.Max(1, maxAttempts); attempt++)
        {
            System.Random rng = useFixedSeed ? new System.Random(seed + attempt) : new System.Random();
            Shuffle(candidates, rng);

            map = new TileType[width, height]; // 預設值就是 TileType.Safe
            int index = 0;

            Vector2Int p = candidates[index++];
            map[p.x, p.y] = TileType.Key;

            p = candidates[index++];
            map[p.x, p.y] = TileType.Exit;

            for (int i = 0; i < walls; i++)
            {
                p = candidates[index++];
                map[p.x, p.y] = TileType.Wall;
            }

            for (int i = 0; i < ghosts; i++)
            {
                p = candidates[index++];
                map[p.x, p.y] = TileType.Ghost;
            }

            if (IsLayoutSolvable(map)) break;

            if (attempt == maxAttempts - 1)
            {
                Debug.LogWarning("[GridManager] 找不到保證可通關的配置，使用最後一次隨機結果。請減少幽靈或牆壁數量。");
            }
        }

        BuildTiles(map);
        BuildBorderWalls();
    }

    /// <summary>依照配置圖建立所有 Tile 物件。</summary>
    private void BuildTiles(TileType[,] map)
    {
        tiles = new Tile[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GameObject go = new GameObject("Tile_" + x + "_" + z);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = GridToWorld(x, z);

                Tile tile = go.AddComponent<Tile>();
                tile.Init(x, z, map[x, z], tileSize, glowKeyAndExit);
                tiles[x, z] = tile;
                spawned.Add(go);

                if (tile.type == TileType.Ghost) ghostTiles.Add(tile);
            }
        }
    }

    /// <summary>在網格外圍建立一圈邊牆（純視覺，移動邊界由 IsInBounds 控制）。</summary>
    private void BuildBorderWalls()
    {
        Material m = MaterialFactory.Create(new Color(0.12f, 0.12f, 0.18f), Color.black);

        for (int i = -1; i <= width; i++)
        {
            CreateBorderCube(i, -1, m);
            CreateBorderCube(i, height, m);
        }
        for (int j = 0; j < height; j++)
        {
            CreateBorderCube(-1, j, m);
            CreateBorderCube(width, j, m);
        }
    }

    private void CreateBorderCube(int x, int z, Material m)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = "Border_" + x + "_" + z;
        Collider col = c.GetComponent<Collider>();
        if (col != null) Destroy(col);

        c.transform.SetParent(transform, false);
        c.transform.localPosition = new Vector3(x * tileSize, 0.6f, z * tileSize);
        c.transform.localScale = new Vector3(tileSize, 1.2f, tileSize);
        c.GetComponent<Renderer>().sharedMaterial = m;
        spawned.Add(c);
    }

    /// <summary>清除目前地圖。</summary>
    public void ClearGrid()
    {
        foreach (GameObject go in spawned)
        {
            if (go == null) continue;
            go.SetActive(false); // 先隱藏，避免 Destroy 延遲到幀尾時畫面殘留
            Destroy(go);
        }
        spawned.Clear();
        ghostTiles.Clear();
        tiles = null;
    }

    // ================= 可解性檢查 =================

    /// <summary>用 BFS 檢查：從起點出發（不經過牆壁與幽靈）是否能到達鑰匙與出口。</summary>
    private bool IsLayoutSolvable(TileType[,] map)
    {
        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startCoord);
        visited[startCoord.x, startCoord.y] = true;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        int found = 0;

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            TileType t = map[c.x, c.y];
            if (t == TileType.Key || t == TileType.Exit) found++;

            foreach (Vector2Int d in dirs)
            {
                Vector2Int n = c + d;
                if (!IsInBounds(n.x, n.y)) continue;
                if (visited[n.x, n.y]) continue;

                TileType nt = map[n.x, n.y];
                if (nt == TileType.Wall || nt == TileType.Ghost) continue;

                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }

        return found >= 2;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        // Fisher-Yates 洗牌
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    // ================= 座標與查詢 =================

    public bool IsInBounds(int x, int z)
    {
        return x >= 0 && x < width && z >= 0 && z < height;
    }

    public Tile GetTile(int x, int z)
    {
        if (tiles == null || !IsInBounds(x, z)) return null;
        return tiles[x, z];
    }

    /// <summary>該格是否可行走（在範圍內且不是牆壁）。</summary>
    public bool IsWalkable(int x, int z)
    {
        Tile t = GetTile(x, z);
        return t != null && t.type != TileType.Wall;
    }

    public Vector3 GridToWorld(int x, int z)
    {
        return new Vector3(x * tileSize, 0f, z * tileSize);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x / tileSize),
                              Mathf.RoundToInt(worldPos.z / tileSize));
    }

    /// <summary>【核心推理邏輯】計算 (x, z) 周圍 8 格內的幽靈總數（不含自己）。</summary>
    public int CountAdjacentGhosts(int x, int z)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                Tile t = GetTile(x + dx, z + dz);
                if (t != null && t.type == TileType.Ghost) count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 【距離雷達】回傳 worldPos 到「最近一隻幽靈」的歐幾里得距離（Vector3.Distance）。
    /// 若地圖上沒有幽靈則回傳 -1。
    /// </summary>
    public float GetNearestGhostDistance(Vector3 worldPos)
    {
        if (ghostTiles.Count == 0) return -1f;

        float min = float.MaxValue;
        foreach (Tile g in ghostTiles)
        {
            float d = Vector3.Distance(worldPos, g.transform.position);
            if (d < min) min = d;
        }
        return min;
    }

    /// <summary>讓所有幽靈現身（Game Over 時呼叫）。</summary>
    public void RevealAllGhosts()
    {
        foreach (Tile g in ghostTiles) g.RevealGhost();
    }
}

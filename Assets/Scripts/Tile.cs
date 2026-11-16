using UnityEngine;

/// <summary>格子的狀態類型。</summary>
public enum TileType
{
    Safe,   // 安全
    Ghost,  // 有幽靈（地雷）
    Key,    // 有鑰匙
    Exit,   // 出口
    Wall    // 牆壁
}

/// <summary>
/// 單一格子：保存座標 (x, z) 與狀態，並負責自己的視覺表現。
/// 所有外觀都用 Unity 內建幾何體（Cube / Sphere / Cylinder）組成，由 GridManager 動態建立。
/// </summary>
public class Tile : MonoBehaviour
{
    [Header("格子資料（由 GridManager 寫入）")]
    public int x;
    public int z;
    public TileType type = TileType.Safe;

    /// <summary>是否已插上警告旗幟。</summary>
    public bool HasFlag { get; private set; }

    /// <summary>玩家是否已踩過這格。</summary>
    public bool Visited { get; private set; }

    // ---- 視覺物件 ----
    private GameObject keyObj;
    private GameObject exitObj;
    private GameObject ghostObj;
    private GameObject flagObj;
    private Material floorMat;
    private Material ghostMat;
    private Light ghostLight;
    private TextMesh numberText;

    private float tileSize = 1f;
    private bool glowSpecials = true;
    private bool ghostRevealed = false;

    // ---- 顏色設定 ----
    private static readonly Color FloorColor = new Color(0.22f, 0.22f, 0.26f);
    private static readonly Color VisitedFloorColor = new Color(0.12f, 0.30f, 0.36f);
    private static readonly Color WallColor = new Color(0.18f, 0.18f, 0.24f);

    // 數字顏色：0,1,2,3,4+ 依序由安心到危險
    private static readonly Color[] NumberColors =
    {
        new Color(0.5f, 0.9f, 0.6f),  // 0
        new Color(0.4f, 0.8f, 1f),    // 1
        new Color(1f, 0.95f, 0.3f),   // 2
        new Color(1f, 0.6f, 0.2f),    // 3
        new Color(1f, 0.25f, 0.25f)   // 4+
    };

    // 內建字型快取（Unity 版本不同字型名稱不同，故做 try/catch）
    private static Font numberFont;
    private static bool fontSearched = false;

    /// <summary>由 GridManager 呼叫：初始化格子並建立對應的視覺物件。</summary>
    public void Init(int gx, int gz, TileType tileType, float size, bool glow)
    {
        x = gx;
        z = gz;
        type = tileType;
        tileSize = size;
        glowSpecials = glow;

        // 地板：扁平的 Cube，留一點縫隙形成格線感
        floorMat = MaterialFactory.Create(FloorColor, Color.black);
        MakePrimitive(transform, PrimitiveType.Cube, "Floor",
            new Vector3(0f, -0.05f, 0f),
            new Vector3(size * 0.94f, 0.1f, size * 0.94f), floorMat);

        switch (type)
        {
            case TileType.Wall:  BuildWall();  break;
            case TileType.Ghost: BuildGhost(); break;
            case TileType.Key:   BuildKey();   break;
            case TileType.Exit:  BuildExit();  break;
        }

        BuildFlag();
    }

    // ================= 外部操作 =================

    /// <summary>切換旗幟，回傳切換後是否有旗幟。</summary>
    public bool ToggleFlag()
    {
        HasFlag = !HasFlag;
        if (flagObj != null) flagObj.SetActive(HasFlag);
        return HasFlag;
    }

    /// <summary>玩家踩過此格：地板變色，並在地上留下周圍幽靈數字（幫助玩家記憶）。</summary>
    public void MarkVisited(int adjacentGhosts)
    {
        Visited = true;
        floorMat.color = VisitedFloorColor;
        ShowNumber(adjacentGhosts);
    }

    /// <summary>撿起鑰匙。</summary>
    public void CollectKey()
    {
        if (keyObj != null) keyObj.SetActive(false);
        type = TileType.Safe;
    }

    /// <summary>幽靈現身並開始閃紅光。</summary>
    public void RevealGhost()
    {
        if (type != TileType.Ghost || ghostObj == null) return;
        ghostRevealed = true;
        ghostObj.SetActive(true);
    }

    // ================= 每幀更新 =================

    private void Update()
    {
        // 鑰匙緩慢自轉
        if (keyObj != null && keyObj.activeSelf)
        {
            keyObj.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
        }

        // 幽靈閃紅光
        if (ghostRevealed && ghostMat != null)
        {
            float t = Mathf.PingPong(Time.time * 4f, 1f);
            Color c = Color.Lerp(new Color(0.35f, 0f, 0f), new Color(1f, 0.05f, 0.05f), t);
            ghostMat.color = c;
            MaterialFactory.SetEmission(ghostMat, c * 3f);
            if (ghostLight != null) ghostLight.intensity = Mathf.Lerp(0.5f, 4f, t);
        }
    }

    // ================= 建立視覺物件 =================

    private void BuildWall()
    {
        Material m = MaterialFactory.Create(WallColor, Color.black);
        MakePrimitive(transform, PrimitiveType.Cube, "Wall",
            new Vector3(0f, 0.6f, 0f),
            new Vector3(tileSize, 1.2f, tileSize), m);
    }

    private void BuildGhost()
    {
        ghostMat = MaterialFactory.Create(new Color(0.35f, 0f, 0f), Color.black);
        ghostObj = MakePrimitive(transform, PrimitiveType.Sphere, "Ghost",
            new Vector3(0f, 0.5f, 0f),
            Vector3.one * (0.6f * tileSize), ghostMat);

        // 紅色點光源，現身時一起閃爍
        GameObject lightObj = new GameObject("GhostLight");
        lightObj.transform.SetParent(ghostObj.transform, false);
        ghostLight = lightObj.AddComponent<Light>();
        ghostLight.type = LightType.Point;
        ghostLight.color = Color.red;
        ghostLight.range = 3f * tileSize;
        ghostLight.intensity = 2f;

        ghostObj.SetActive(false); // 預設隱形
    }

    private void BuildKey()
    {
        float s = tileSize;
        Color gold = new Color(1f, 0.85f, 0.1f);
        Material m = MaterialFactory.Create(gold, glowSpecials ? gold * 2f : Color.black);

        keyObj = new GameObject("Key");
        keyObj.transform.SetParent(transform, false);
        keyObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        // 用球 + 方塊拼成簡易鑰匙
        MakePrimitive(keyObj.transform, PrimitiveType.Sphere, "Head",
            new Vector3(0f, 0f, -0.15f * s), Vector3.one * (0.25f * s), m);
        MakePrimitive(keyObj.transform, PrimitiveType.Cube, "Shaft",
            new Vector3(0f, 0f, 0.08f * s), new Vector3(0.07f * s, 0.07f * s, 0.35f * s), m);
        MakePrimitive(keyObj.transform, PrimitiveType.Cube, "Tooth",
            new Vector3(0.08f * s, 0f, 0.2f * s), new Vector3(0.2f * s, 0.07f * s, 0.07f * s), m);
    }

    private void BuildExit()
    {
        float s = tileSize;
        Color green = new Color(0.2f, 1f, 0.4f);
        Material m = MaterialFactory.Create(green * 0.6f, glowSpecials ? green * 1.2f : Color.black);

        exitObj = new GameObject("ExitDoor");
        exitObj.transform.SetParent(transform, false);

        // 門板
        MakePrimitive(exitObj.transform, PrimitiveType.Cube, "Door",
            new Vector3(0f, 0.7f, 0f), new Vector3(0.8f * s, 1.4f, 0.15f * s), m);
        // 底座圓盤
        MakePrimitive(exitObj.transform, PrimitiveType.Cylinder, "Pad",
            new Vector3(0f, 0.02f, 0f), new Vector3(0.8f * s, 0.02f, 0.8f * s), m);
    }

    private void BuildFlag()
    {
        float s = tileSize;
        Color orange = new Color(1f, 0.55f, 0.1f);
        Material m = MaterialFactory.Create(orange, orange * 2f);

        flagObj = new GameObject("Flag");
        flagObj.transform.SetParent(transform, false);

        // 旗桿（Cylinder 預設高 2，Y 縮放 0.4 => 高 0.8）
        MakePrimitive(flagObj.transform, PrimitiveType.Cylinder, "Pole",
            new Vector3(0f, 0.4f, 0f), new Vector3(0.06f * s, 0.4f, 0.06f * s), m);
        // 旗面
        MakePrimitive(flagObj.transform, PrimitiveType.Cube, "Cloth",
            new Vector3(0.17f * s, 0.68f, 0f), new Vector3(0.3f * s, 0.2f, 0.03f * s), m);

        // 發光效果：小型橘色點光源
        GameObject lightObj = new GameObject("FlagLight");
        lightObj.transform.SetParent(flagObj.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1f, 0f);
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = orange;
        l.range = 2.5f * tileSize;
        l.intensity = 1.5f;

        flagObj.SetActive(false);
    }

    private void ShowNumber(int count)
    {
        if (type == TileType.Wall) return;

        if (numberText == null)
        {
            EnsureFont();
            if (numberFont == null) return; // 找不到字型就略過，不影響遊戲

            GameObject go = new GameObject("Number");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 平貼地面，朝上

            numberText = go.AddComponent<TextMesh>();
            numberText.font = numberFont;
            numberText.anchor = TextAnchor.MiddleCenter;
            numberText.alignment = TextAlignment.Center;
            numberText.fontSize = 64;
            numberText.characterSize = 0.08f * tileSize;
            go.GetComponent<MeshRenderer>().sharedMaterial = numberFont.material;
        }

        numberText.text = count.ToString();
        numberText.color = NumberColors[Mathf.Clamp(count, 0, NumberColors.Length - 1)];
    }

    private static void EnsureFont()
    {
        if (fontSearched) return;
        fontSearched = true;

        // Unity 2022.2+ / Unity 6 使用 LegacyRuntime.ttf；舊版使用 Arial.ttf
        try { numberFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (numberFont == null)
        {
            try { numberFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
    }

    /// <summary>建立內建幾何體並移除 Collider（本遊戲用數學判定，不需要物理碰撞）。</summary>
    private static GameObject MakePrimitive(Transform parent, PrimitiveType primitive, string objName,
                                            Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(primitive);
        go.name = objName;

        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}

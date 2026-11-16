using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>遊戲狀態。</summary>
public enum GameState
{
    Playing,
    GameOver,
    Victory
}

/// <summary>
/// 遊戲管理器：控制整體流程（開始 / 重來）、處理踩到幽靈、拿鑰匙、抵達出口的判定，
/// 並更新狀態 UI 與勝負面板。也負責套用「漆黑環境」設定。
/// 重來採「原地重新生成」，不需要把場景加入 Build Settings。
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("核心引用")]
    public GridManager grid;
    public PlayerController player;
    public DetectorSystem detector;

    [Header("UI - 狀態顯示")]
    public Text statusText;          // 鑰匙狀態 + 旗幟數量
    public Text messageText;         // 暫時性提示訊息

    [Header("UI - 勝負面板")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public Button gameOverRestartButton;
    public Button victoryRestartButton;
    public Image redFlashOverlay;    // 全螢幕紅色 Image（Game Over 閃紅光，可留空）

    [Header("氛圍")]
    public Camera mainCamera;
    public bool applyDarkSettings = true; // 自動把環境光、天空盒、攝影機背景設為全黑
    public float panelDelay = 1.2f;       // 幽靈現身後多久才顯示重來面板

    public GameState State { get; private set; }

    private bool hasKey = false;
    private int flagsPlaced = 0;
    private float messageTimer = 0f;

    // ================= 生命週期 =================

    private void Awake()
    {
        if (grid == null || player == null || detector == null)
        {
            Debug.LogError("[GameManager] 請在 Inspector 指定 Grid / Player / Detector！");
        }

        if (applyDarkSettings) ApplyDarkSettings();

        // 以程式綁定按鈕事件，免去在 Inspector 設定 OnClick
        if (gameOverRestartButton != null) gameOverRestartButton.onClick.AddListener(StartGame);
        if (victoryRestartButton != null) victoryRestartButton.onClick.AddListener(StartGame);
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.OnTileEntered += HandlePlayerEnteredTile;
            player.OnFlagToggled += HandleFlagToggled;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnTileEntered -= HandlePlayerEnteredTile;
            player.OnFlagToggled -= HandleFlagToggled;
        }
    }

    private void Start()
    {
        StartGame();
    }

    private void Update()
    {
        // 快捷鍵：R 重來（方便測試）
        if (Input.GetKeyDown(KeyCode.R)) StartGame();

        // 訊息倒數消失
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && messageText != null) messageText.text = "";
        }
    }

    // ================= 流程控制 =================

    /// <summary>開始（或重新開始）一局。</summary>
    public void StartGame()
    {
        StopAllCoroutines();

        State = GameState.Playing;
        hasKey = false;
        flagsPlaced = 0;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (redFlashOverlay != null)
        {
            Color c = redFlashOverlay.color;
            c.a = 0f;
            redFlashOverlay.color = c;
        }

        grid.GenerateGrid();

        detector.IsActive = true;
        player.InputEnabled = true;
        player.ResetToStart(); // 會觸發 OnTileEntered，讓偵測器顯示初始數字

        UpdateStatusUI();
        ShowMessage("找到鑰匙，再前往出口！", 3f);
    }

    private void GameOver()
    {
        State = GameState.GameOver;
        player.InputEnabled = false;
        detector.IsActive = false;

        grid.RevealAllGhosts(); // 所有幽靈現身並閃紅光
        ShowMessage("你踩到幽靈了！", 5f);

        StartCoroutine(RedFlashRoutine());
        StartCoroutine(ShowPanelAfterDelay(gameOverPanel, GameState.GameOver));
    }

    private void Victory()
    {
        State = GameState.Victory;
        player.InputEnabled = false;
        detector.IsActive = false;

        ShowMessage("成功逃脫！", 5f);
        StartCoroutine(ShowPanelAfterDelay(victoryPanel, GameState.Victory));
    }

    // ================= 事件處理 =================

    /// <summary>玩家抵達某格時的判定。</summary>
    private void HandlePlayerEnteredTile(Tile tile)
    {
        if (State != GameState.Playing || tile == null) return;

        switch (tile.type)
        {
            case TileType.Ghost:
                GameOver();
                break;

            case TileType.Key:
                if (!hasKey)
                {
                    hasKey = true;
                    tile.CollectKey();
                    ShowMessage("取得鑰匙！前往出口門。", 3f);
                    UpdateStatusUI();
                }
                break;

            case TileType.Exit:
                if (hasKey) Victory();
                else ShowMessage("門鎖住了，需要鑰匙。", 2f);
                break;
        }
    }

    private void HandleFlagToggled(Tile tile, bool placed)
    {
        flagsPlaced += placed ? 1 : -1;
        UpdateStatusUI();
    }

    // ================= UI =================

    private void UpdateStatusUI()
    {
        if (statusText == null) return;

        string keyStr = hasKey ? "已取得" : "未取得";
        statusText.text = "鑰匙：" + keyStr + "\n旗幟：" + flagsPlaced + " / " + grid.TotalGhosts;
    }

    private void ShowMessage(string msg, float seconds)
    {
        if (messageText == null) return;
        messageText.text = msg;
        messageTimer = seconds;
    }

    private IEnumerator ShowPanelAfterDelay(GameObject panel, GameState expected)
    {
        yield return new WaitForSeconds(panelDelay);
        if (State == expected && panel != null) panel.SetActive(true);
    }

    /// <summary>全螢幕紅光閃爍。</summary>
    private IEnumerator RedFlashRoutine()
    {
        if (redFlashOverlay == null) yield break;

        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            Color c = redFlashOverlay.color;
            c.a = Mathf.PingPong(t * 4f, 0.45f);
            redFlashOverlay.color = c;
            yield return null;
        }

        Color end = redFlashOverlay.color;
        end.a = 0.2f; // 停留一層淡淡的紅
        redFlashOverlay.color = end;
    }

    // ================= 黑暗氛圍 =================

    /// <summary>把環境光、反射、天空盒與攝影機背景全部設為黑色，場景只剩玩家燈與發光物件。</summary>
    private void ApplyDarkSettings()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.skybox = null;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
        }
    }
}

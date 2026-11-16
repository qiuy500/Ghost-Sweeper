using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 靈異探測器：
/// 1) 數字提示：玩家所在格子周圍 8 格內的幽靈總數。
/// 2) 距離雷達：玩家與最近一隻幽靈的距離（Vector3.Distance），並依距離調整「滴滴聲」間隔。
/// 若沒有指定 beepClip，會自動用程式生成一個 880Hz 短嗶聲（無需任何音效資產）。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DetectorSystem : MonoBehaviour
{
    [Header("引用")]
    public GridManager grid;
    public PlayerController player;

    [Header("UI（Legacy Text）")]
    public Text adjacentCountText;   // 顯示周圍 8 格幽靈數
    public Text distanceText;        // 顯示最近幽靈距離

    [Header("滴滴聲設定")]
    public AudioClip beepClip;                 // 可留空，自動生成
    [Range(0f, 1f)] public float volume = 0.6f;
    public float nearDistance = 1f;            // 小於等於此距離 → 使用最短間隔
    public float farDistance = 6f;             // 大於等於此距離 → 使用最長間隔
    public float minInterval = 0.12f;          // 最近時的滴滴間隔（秒）
    public float maxInterval = 1.5f;           // 最遠時的滴滴間隔（秒）
    public float minPitch = 0.9f;              // 遠處音調
    public float maxPitch = 1.6f;              // 近處音調

    /// <summary>是否運作中（Game Over / Victory 時由 GameManager 關閉）。</summary>
    public bool IsActive { get; set; }

    /// <summary>目前所在格子周圍 8 格的幽靈數。</summary>
    public int AdjacentGhostCount { get; private set; }

    /// <summary>與最近幽靈的距離（-1 表示地圖上沒有幽靈）。</summary>
    public float NearestGhostDistance { get; private set; }

    private AudioSource audioSource;
    private float beepTimer = 0f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D 音效

        if (beepClip == null) beepClip = CreateBeepClip();
        NearestGhostDistance = -1f;
    }

    private void OnEnable()
    {
        if (player != null) player.OnTileEntered += HandleTileEntered;
    }

    private void OnDisable()
    {
        if (player != null) player.OnTileEntered -= HandleTileEntered;
    }

    /// <summary>玩家抵達新格子時：重新計算周圍幽靈數，並把數字標記在地板上。</summary>
    private void HandleTileEntered(Tile tile)
    {
        if (grid == null || player == null) return;

        AdjacentGhostCount = grid.CountAdjacentGhosts(player.GridPos.x, player.GridPos.y);

        if (tile != null && tile.type != TileType.Ghost)
        {
            tile.MarkVisited(AdjacentGhostCount);
        }

        if (adjacentCountText != null)
        {
            adjacentCountText.text = "周圍幽靈數：" + AdjacentGhostCount;
        }
    }

    private void Update()
    {
        if (!IsActive || grid == null || player == null) return;

        // 每幀計算與最近幽靈的距離（玩家移動中也會連續變化）
        NearestGhostDistance = grid.GetNearestGhostDistance(player.transform.position);

        if (distanceText != null)
        {
            distanceText.text = NearestGhostDistance < 0f
                ? "最近幽靈距離：--"
                : "最近幽靈距離：" + NearestGhostDistance.ToString("F2");
        }

        if (NearestGhostDistance < 0f) return; // 沒有幽靈就不發聲

        // 距離 → 0~1（0 = 很近，1 = 很遠），再換算成滴聲間隔與音調
        float t = Mathf.InverseLerp(nearDistance, farDistance, NearestGhostDistance);
        float interval = Mathf.Lerp(minInterval, maxInterval, t);

        beepTimer -= Time.deltaTime;
        if (beepTimer <= 0f)
        {
            audioSource.pitch = Mathf.Lerp(maxPitch, minPitch, t);
            audioSource.PlayOneShot(beepClip, volume);
            beepTimer = interval;
        }
    }

    /// <summary>程式生成一段 0.08 秒的正弦波嗶聲。</summary>
    private AudioClip CreateBeepClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.08f;
        const float frequency = 880f;

        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            float envelope = 1f - (float)i / samples; // 線性淡出，避免結尾爆音
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.8f;
        }

        AudioClip clip = AudioClip.Create("GeneratedBeep", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

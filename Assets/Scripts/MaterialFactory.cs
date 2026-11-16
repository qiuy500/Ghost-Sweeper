using UnityEngine;

/// <summary>
/// 材質球工廠：用程式動態建立「基本材質球」，不需要任何外部資產。
/// 會依序嘗試 URP Lit → Built-in Standard，自動相容兩種渲染管線（不支援 HDRP）。
/// </summary>
public static class MaterialFactory
{
    private static Shader cachedShader;

    private static Shader GetShader()
    {
        if (cachedShader != null) return cachedShader;

        cachedShader = Shader.Find("Universal Render Pipeline/Lit"); // URP
        if (cachedShader == null) cachedShader = Shader.Find("Standard"); // Built-in
        if (cachedShader == null) cachedShader = Shader.Find("Sprites/Default"); // 最後備援
        return cachedShader;
    }

    /// <summary>建立材質：baseColor 為底色，emissionColor 為自發光（黑色 = 不發光）。</summary>
    public static Material Create(Color baseColor, Color emissionColor)
    {
        Material mat = new Material(GetShader());
        mat.color = baseColor;
        SetEmission(mat, emissionColor);
        return mat;
    }

    /// <summary>設定自發光顏色（可在執行時呼叫，用於閃爍效果）。</summary>
    public static void SetEmission(Material mat, Color emissionColor)
    {
        if (mat == null) return;
        if (!mat.HasProperty("_EmissionColor")) return;

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }
}

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Picture quality and frame pacing for the 3D demo, tuned to the machine it runs on.
///  - Two tiers: integrated / small GPUs get 2x MSAA, one 1024 shadow cascade; dedicated GPUs get 4x MSAA, two 2048 cascades.
///    (The old fixed setting was 8x MSAA, four 4096 cascades and SMAA on top, which a laptop GPU cannot carry.)
///  - VSync at about 60 frames a second on any monitor (every 2nd refresh at 120/144 Hz), about 30 while a card covers
///    the room (pause, the city wheel, help): the GPU never runs flat out for a card table.
///  - In the background (window not focused / minimised) it drops to 15 frames a second.
///  - If the game still runs below ~40 fps it lowers the render resolution step by step (to 70% at most).
/// Everything is put back when the demo closes. URP settings are reached by reflection, so this compiles without URP.
/// </summary>
public static class Gfx
{
    static bool _on;
    static int _aa, _cascades, _pixelLights, _vsync, _targetFps;
    static AnisotropicFiltering _aniso;
    static ShadowQuality _shadows;
    static ShadowResolution _shadowRes;
    static float _shadowDistance;
    static object _asset;
    static readonly System.Collections.Generic.Dictionary<string, object> _assetOld = new System.Collections.Generic.Dictionary<string, object>();

    /// <summary>True on integrated graphics or a GPU with little memory.</summary>
    public static bool LowEnd { get; private set; }

    // dynamic resolution
    static float _scale = 1f;
    static float _winTime, _settle = 3f;
    static int _winFrames;
    const float MinScale = 0.7f, SlowFps = 40f, Window = 2f;

    static bool DetectLowEnd()
    {
        string n = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
        bool integrated = (n.Contains("intel") && !n.Contains("arc")) || n.Contains("radeon(tm) graphics") ||
                          n.Contains("radeon graphics") || n.Contains("vega") || n.Contains("adreno") || n.Contains("mali");
        return integrated || SystemInfo.graphicsMemorySize < 3000 || SystemInfo.processorCount < 4;
    }

    public static void Apply(Camera cam)
    {
        if (!_on) LowEnd = DetectLowEnd();
        if (cam != null)
        {
            cam.allowMSAA = true;
            cam.allowHDR = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 220f;
            SetupUrpCamera(cam);
        }
        if (_on) return;
        _on = true;

        _aa = QualitySettings.antiAliasing;
        _aniso = QualitySettings.anisotropicFiltering;
        _shadows = QualitySettings.shadows;
        _shadowRes = QualitySettings.shadowResolution;
        _shadowDistance = QualitySettings.shadowDistance;
        _cascades = QualitySettings.shadowCascades;
        _pixelLights = QualitySettings.pixelLightCount;
        _vsync = QualitySettings.vSyncCount;
        _targetFps = Application.targetFrameRate;

        int msaa = LowEnd ? 2 : 4;
        int shadowMap = LowEnd ? 1024 : 2048;
        int cascades = LowEnd ? 1 : 2;
        float shadowDist = LowEnd ? 12f : 16f;       // the table is all that casts shadows; the backdrop casts none

        QualitySettings.antiAliasing = msaa;
        QualitySettings.anisotropicFiltering = LowEnd ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
        QualitySettings.shadows = LowEnd ? ShadowQuality.HardOnly : ShadowQuality.All;
        QualitySettings.shadowResolution = LowEnd ? ShadowResolution.Medium : ShadowResolution.High;
        QualitySettings.shadowDistance = shadowDist;
        QualitySettings.shadowCascades = cascades;
        QualitySettings.pixelLightCount = Mathf.Max(_pixelLights, 3);

        // frame pacing: about 60 frames a second whatever the monitor (a 144 Hz screen gets every 2nd refresh)
        float hz = 60f;
        try { hz = (float)Screen.currentResolution.refreshRateRatio.value; } catch (Exception) { }
        if (hz < 30f || float.IsNaN(hz)) hz = 60f;
        _hz = hz;
        _interval = Mathf.Clamp(Mathf.RoundToInt(hz / 60f), 1, 4);
        _calm = false;
        _background = false;
        ApplyPacing();

        _scale = 1f;
        _settle = 3f;
        _winTime = 0f;
        _winFrames = 0;

        try
        {
            _asset = GraphicsSettings.currentRenderPipeline;
            if (_asset != null)
            {
                foreach (string p in new[] { "msaaSampleCount", "mainLightShadowmapResolution", "shadowDistance", "shadowCascadeCount", "supportsHDR", "renderScale", "supportsSoftShadows" })
                    Remember(_asset, p);
                SetProp(_asset, "msaaSampleCount", msaa);
                SetProp(_asset, "mainLightShadowmapResolution", shadowMap);
                SetProp(_asset, "shadowDistance", shadowDist);
                SetProp(_asset, "shadowCascadeCount", cascades);
                SetProp(_asset, "supportsHDR", false);              // no post-processing here, so HDR buffers are only extra bandwidth
                SetProp(_asset, "renderScale", 1f);
                if (LowEnd) SetProp(_asset, "supportsSoftShadows", false);
            }
        }
        catch (Exception) { }
    }

    /// <summary>Called every frame with the unscaled frame time: lowers the resolution if the GPU cannot keep up.</summary>
    public static void Tick(float dt)
    {
        if (!_on || _asset == null) return;
        if (_settle > 0f) { _settle -= dt; return; }        // skip the hitch of building a map
        if (dt > 0.25f) return;                             // a single load spike is not a slow GPU
        _winTime += dt;
        _winFrames++;
        if (_winTime < Window) return;
        float fps = _winFrames / _winTime;
        _winTime = 0f;
        _winFrames = 0;
        // slow = clearly under the pace we asked for (60, or 30 while calm), not just under the monitor's rate
        if (fps < ExpectedFps * (SlowFps / 60f) && _scale > MinScale + 0.001f)
        {
            _scale = Mathf.Max(MinScale, _scale - 0.1f);
            try { SetProp(_asset, "renderScale", _scale); } catch (Exception) { }
            _settle = 1f;
        }
    }

    /// <summary>Called after a map is built, so its building time is not counted as slowness.</summary>
    public static void Settle() { _settle = Mathf.Max(_settle, 1.5f); }

    /// <summary>Window lost focus or was minimised: draw only a few frames a second. Back in front: follow the monitor again.</summary>
    public static void SetBackground(bool background)
    {
        if (!_on || background == _background) return;
        _background = background;
        if (!background) _settle = 1.5f;
        ApplyPacing();
    }

    /// <summary>
    /// Calm = nothing on screen moves much (the pause card, the city wheel or the help card cover the room):
    /// half the frame rate (~30). Cheap to call every frame: it only changes anything when the state changes.
    /// </summary>
    public static void SetCalm(bool calm)
    {
        if (!_on || calm == _calm) return;
        _calm = calm;
        _settle = Mathf.Max(_settle, 1f);
        _winTime = 0f;
        _winFrames = 0;
        ApplyPacing();
    }

    static float _hz = 60f;
    static int _interval = 1;
    static bool _calm, _background;

    static float ExpectedFps { get { return _hz / Mathf.Max(1, QualitySettings.vSyncCount); } }

    static void ApplyPacing()
    {
        if (_background)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 15;
            return;
        }
        QualitySettings.vSyncCount = Mathf.Clamp(_calm ? _interval * 2 : _interval, 1, 4);
        Application.targetFrameRate = -1;
    }

    public static void Restore()
    {
        if (!_on) return;
        _on = false;
        QualitySettings.antiAliasing = _aa;
        QualitySettings.anisotropicFiltering = _aniso;
        QualitySettings.shadows = _shadows;
        QualitySettings.shadowResolution = _shadowRes;
        QualitySettings.shadowDistance = _shadowDistance;
        QualitySettings.shadowCascades = _cascades;
        QualitySettings.pixelLightCount = _pixelLights;
        QualitySettings.vSyncCount = _vsync;
        Application.targetFrameRate = _targetFps;
        try
        {
            if (_asset != null)
                foreach (var kv in _assetOld) SetProp(_asset, kv.Key, kv.Value);
        }
        catch (Exception) { }
        _assetOld.Clear();
        _asset = null;
    }

    static void Remember(object target, string name)
    {
        PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanRead && p.CanWrite) _assetOld[name] = p.GetValue(target, null);
    }

    static void SetProp(object target, string name, object value)
    {
        PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p == null || !p.CanWrite) return;
        if (value is int && p.PropertyType.IsEnum) value = Enum.ToObject(p.PropertyType, (int)value);
        p.SetValue(target, value, null);
    }

    /// <summary>In URP the camera has an extra component: no post-process anti-aliasing (MSAA already smooths the edges) and no post-processing.</summary>
    static void SetupUrpCamera(Camera cam)
    {
        try
        {
            Type t = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (t == null) return;
            Component data = cam.gameObject.GetComponent(t);
            if (data == null) return;
            PropertyInfo aa = t.GetProperty("antialiasing");
            if (aa != null && aa.CanWrite) aa.SetValue(data, Enum.Parse(aa.PropertyType, "None"), null);
            PropertyInfo pp = t.GetProperty("renderPostProcessing");
            if (pp != null && pp.CanWrite) pp.SetValue(data, false, null);
        }
        catch (Exception) { }
    }
}

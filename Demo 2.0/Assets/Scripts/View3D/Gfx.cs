using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Picture quality for the 3D demo: anti-aliasing, sharper shadows and sharper textures at a slant. It changes global
/// quality settings only while the demo is open and puts every value back afterwards. The Universal Render Pipeline
/// settings are reached by reflection, so this compiles in projects without URP.
/// </summary>
public static class Gfx
{
    static bool _on;
    static int _aa, _cascades, _pixelLights;
    static AnisotropicFiltering _aniso;
    static ShadowQuality _shadows;
    static ShadowResolution _shadowRes;
    static float _shadowDistance;
    static object _asset;
    static readonly System.Collections.Generic.Dictionary<string, object> _assetOld = new System.Collections.Generic.Dictionary<string, object>();

    public static void Apply(Camera cam)
    {
        if (cam != null)
        {
            cam.allowMSAA = true;
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

        QualitySettings.antiAliasing = 8;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 40f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.pixelLightCount = Mathf.Max(_pixelLights, 6);

        try
        {
            _asset = GraphicsSettings.currentRenderPipeline;
            if (_asset != null)
            {
                Remember(_asset, "msaaSampleCount");
                Remember(_asset, "mainLightShadowmapResolution");
                Remember(_asset, "shadowDistance");
                SetProp(_asset, "msaaSampleCount", 8);
                SetProp(_asset, "mainLightShadowmapResolution", 4096);
                SetProp(_asset, "shadowDistance", 40f);
            }
        }
        catch (Exception) { }
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

    /// <summary>In URP the camera has an extra component with the anti-aliasing mode (SMAA, high quality).</summary>
    static void SetupUrpCamera(Camera cam)
    {
        try
        {
            Type t = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (t == null) return;
            Component data = cam.gameObject.GetComponent(t);
            if (data == null) return;
            PropertyInfo aa = t.GetProperty("antialiasing");
            if (aa != null && aa.CanWrite) aa.SetValue(data, Enum.Parse(aa.PropertyType, "SubpixelMorphologicalAntiAliasing"), null);
            PropertyInfo q = t.GetProperty("antialiasingQuality");
            if (q != null && q.CanWrite) q.SetValue(data, Enum.Parse(q.PropertyType, "High"), null);
        }
        catch (Exception) { }
    }
}

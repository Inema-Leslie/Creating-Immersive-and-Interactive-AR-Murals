using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MuralAR
{
    [Serializable]
    public struct LightingPreset
    {
        public Material skybox;
        public AmbientMode ambientMode;
        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
        public Color ambientLight;
        public float ambientIntensity;
        public bool fog;
        public Color fogColor;
        public FogMode fogMode;
        public float fogDensity;
        public float fogStart;
        public float fogEnd;
        public Color sunColor;
        public float sunIntensity;
        public Vector3 sunRotation;
        public LightShadows sunShadows;

        public static LightingPreset Capture(Light sun) => new()
        {
            skybox = RenderSettings.skybox,
            ambientMode = RenderSettings.ambientMode,
            ambientSky = RenderSettings.ambientSkyColor,
            ambientEquator = RenderSettings.ambientEquatorColor,
            ambientGround = RenderSettings.ambientGroundColor,
            ambientLight = RenderSettings.ambientLight,
            ambientIntensity = RenderSettings.ambientIntensity,
            fog = RenderSettings.fog,
            fogColor = RenderSettings.fogColor,
            fogMode = RenderSettings.fogMode,
            fogDensity = RenderSettings.fogDensity,
            fogStart = RenderSettings.fogStartDistance,
            fogEnd = RenderSettings.fogEndDistance,
            sunColor = sun != null ? sun.color : Color.white,
            sunIntensity = sun != null ? sun.intensity : 1f,
            sunRotation = sun != null ? sun.transform.eulerAngles : new Vector3(50f, -30f, 0f),
            sunShadows = sun != null ? sun.shadows : LightShadows.Soft,
        };

        public void Apply(Light sun)
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            if (sun == null) return;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.transform.rotation = Quaternion.Euler(sunRotation);
            sun.shadows = sunShadows;
            DynamicGI.UpdateEnvironment();
        }
    }
}

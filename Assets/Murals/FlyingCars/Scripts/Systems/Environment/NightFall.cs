using UnityEngine;

namespace MuralAR
{
    public sealed class NightFall : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => sun = refs.Sun;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly string[] SkyFloats = { "_Exposure", "_AtmosphereThickness", "_SunSize", "_SunSizeConvergence" };
        static readonly string[] SkyColors = { "_SkyTint", "_GroundColor" };

        [SerializeField] MuralConfig config;
        [SerializeField] StoryPlayer story;
        [SerializeField] ExperienceController experience;
        [SerializeField] Light sun;
        [SerializeField, Tooltip("The night look (sky material, ambient, fog, moonlight), made by the setup script.")]
        LightingPreset nightLighting;
        [SerializeField, Tooltip("Star particles on a dome over the savanna (additive, faded in with _BaseColor).")]
        ParticleSystem stars;
        [SerializeField, Tooltip("Firefly clusters around the trees and around her.")]
        ParticleSystem[] fireflies = System.Array.Empty<ParticleSystem>();
        [SerializeField, Tooltip("Unlit scenery (the baked tree impostors) that the sun doesn't darken: tinted towards night instead.")]
        Renderer[] unlitScenery = System.Array.Empty<Renderer>();
        [SerializeField] Color unlitNightTint = new(0.22f, 0.25f, 0.38f, 1f);

        public float Amount { get; private set; }

        LightingPreset _day;
        Material _sky;
        MaterialPropertyBlock _block;
        Renderer _starsRenderer;
        bool _firefliesOut;
        bool _running;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (stars != null) _starsRenderer = stars.GetComponent<Renderer>();
            SetStars(0f);
        }

        void OnEnable()
        {
            story.OnStoryStarted += HandleStoryStarted;
            experience.OnStateChanged += HandleState;
        }

        void OnDisable()
        {
            story.OnStoryStarted -= HandleStoryStarted;
            experience.OnStateChanged -= HandleState;
        }

        void HandleStoryStarted()
        {
            _day = LightingPreset.Capture(sun);
            if (_sky != null) Destroy(_sky);
            _sky = _day.skybox != null ? new Material(_day.skybox) : null;
            if (_sky != null) RenderSettings.skybox = _sky;
            Amount = 0f;
            _firefliesOut = false;
            _running = true;
            if (stars != null) stars.Play();
            SetStars(0f);
        }

        void HandleState(ExperienceState state)
        {
            if (state != ExperienceState.Scanning) return;
            _running = false;
            Amount = 0f;
            SetStars(0f);
            foreach (var f in fireflies) f.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            TintScenery(Color.white);
            if (_sky != null) Destroy(_sky);
            _sky = null;
        }

        void Update()
        {
            if (!_running) return;
            float k = Mathf.SmoothStep(0f, 1f, (story.Elapsed - config.nightFallsAtSeconds) / Mathf.Max(0.01f, config.nightTransitionSeconds));
            if (Mathf.Approximately(k, Amount) && k is 0f or 1f) return;
            Amount = k;
            Blend(k);
            if (!_firefliesOut && k > 0.35f)
            {
                _firefliesOut = true;
                foreach (var f in fireflies) f.Play(true);
            }
        }

        void Blend(float k)
        {
            var n = nightLighting;
            RenderSettings.ambientLight = Color.Lerp(_day.ambientLight, n.ambientLight, k);
            RenderSettings.ambientSkyColor = Color.Lerp(_day.ambientSky, n.ambientSky, k);
            RenderSettings.fogColor = Color.Lerp(_day.fogColor, n.fogColor, k);
            RenderSettings.fogDensity = Mathf.Lerp(_day.fogDensity, n.fogDensity, k);
            if (sun != null)
            {
                sun.color = Color.Lerp(_day.sunColor, n.sunColor, k);
                sun.intensity = Mathf.Lerp(_day.sunIntensity, n.sunIntensity, k);
                sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(_day.sunRotation), Quaternion.Euler(n.sunRotation), k);
            }
            if (_sky != null && n.skybox != null && _day.skybox != null)
            {
                foreach (var p in SkyFloats)
                    if (_sky.HasProperty(p)) _sky.SetFloat(p, Mathf.Lerp(_day.skybox.GetFloat(p), n.skybox.GetFloat(p), k));
                foreach (var p in SkyColors)
                    if (_sky.HasProperty(p)) _sky.SetColor(p, Color.Lerp(_day.skybox.GetColor(p), n.skybox.GetColor(p), k));
            }
            SetStars(Mathf.Clamp01((k - 0.3f) / 0.7f));
            TintScenery(Color.Lerp(Color.white, unlitNightTint, k));
        }

        void TintScenery(Color tint)
        {
            foreach (var r in unlitScenery)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                r.SetPropertyBlock(_block);
            }
        }

        void SetStars(float alpha)
        {
            if (_starsRenderer == null) return;
            _starsRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
            _starsRenderer.SetPropertyBlock(_block);
            _starsRenderer.enabled = alpha > 0.001f;
        }

        void OnDestroy()
        {
            if (_sky != null) Destroy(_sky);
        }
    }
}

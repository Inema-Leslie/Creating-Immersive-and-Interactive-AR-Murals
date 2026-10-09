using UnityEngine;

namespace MuralAR
{
    public sealed class CarLights : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] CarController car;
        [SerializeField, Tooltip("Additive glow renderers (cones, underglow, pads).")] Renderer[] glows = System.Array.Empty<Renderer>();
        [SerializeField, Tooltip("Body renderer with an emissive material slot (the ARCADE car has one).")] Renderer emissive;
        [SerializeField] int emissiveMaterialIndex = -1;
        [SerializeField] TrailRenderer trail;
        [SerializeField, Tooltip("The tappable info icon; shown only while the car hovers idle.")] GameObject infoIcon;
        [SerializeField] float flickerRate = 18f;
        [SerializeField] float blipSeconds = 1.2f;
        [SerializeField] float emissionStrength = 3f;

        MaterialPropertyBlock _block;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (infoIcon != null) infoIcon.SetActive(false);
        }
        float _flickerUntil;

        void OnEnable()
        {
            car.OnStateChanged += HandleState;
            car.OnBlip += HandleBlip;
        }

        void OnDisable()
        {
            car.OnStateChanged -= HandleState;
            car.OnBlip -= HandleBlip;
        }

        void HandleBlip() => _flickerUntil = Time.time + blipSeconds;

        void HandleState(CarState state)
        {
            if (trail != null) trail.emitting = state is CarState.Flying or CarState.Returning;
            if (infoIcon != null) infoIcon.SetActive(state == CarState.Idle);
        }

        void Update()
        {
            float level;
            if (car.State == CarState.Dormant) level = 0f;
            else if (car.State == CarState.Waking || Time.time < _flickerUntil)
                level = Mathf.PerlinNoise(Time.time * flickerRate, car.Index) > 0.45f ? 0.9f : 0.05f;
            else level = 0.4f + 0.6f * car.Throttle;

            var color = car.Settings.lightColor * level;
            foreach (var r in glows)
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(_block);
            }

            if (emissive != null && emissiveMaterialIndex >= 0)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (2f + car.Throttle * 10f));
                emissive.GetPropertyBlock(_block, emissiveMaterialIndex);
                _block.SetColor(EmissionId, car.Settings.lightColor * (level * pulse * emissionStrength));
                emissive.SetPropertyBlock(_block, emissiveMaterialIndex);
            }
        }
    }
}

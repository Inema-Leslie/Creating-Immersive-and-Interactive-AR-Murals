using UnityEngine;

namespace MuralAR
{
    public sealed class CarAudio : MonoBehaviour
    {
        const float RevFadeSeconds = 0.25f;
        const float BlipSeconds = 0.6f;
        const float BurstSeconds = 1.1f;
        const float EngineFadeInSeconds = 1.2f;
        const float EngineTakeOverShare = 0.6f;

        [SerializeField] CarController car;
        [SerializeField] AudioSource engine;
        [SerializeField] AudioSource oneShots;
        [SerializeField, Tooltip("Plays the throttle revs; stopped when the hold is released.")] AudioSource revSource;
        [SerializeField, Tooltip("Loop for hover and flight.")] AudioClip engineLoop;
        [SerializeField] AudioClip startUp;
        [SerializeField] AudioClip rev;
        [SerializeField] AudioClip flyBy;
        [SerializeField, Tooltip("Played just before the car comes to a stop.")] AudioClip brake;
        [SerializeField, Tooltip("Played as it parks in front of the viewer for inspection.")] AudioClip handBrake;
        [SerializeField, Tooltip("Optional greeting once it hovers for the first time (Car B honks).")] AudioClip greeting;
        [SerializeField] AnimationCurve volumeByThrottle = AnimationCurve.Linear(0f, 0.25f, 1f, 0.9f);
        [SerializeField, Range(0f, 1f)] float oneShotVolume = 0.8f;

        public float Volume { get; set; } = 1f;

        CarState _previous = CarState.Dormant;
        float _engineStartsAt = float.MaxValue;
        float _revStopAt = float.MaxValue;
        float _revFadeFrom = float.MaxValue;
        int _brakedPath = -1;
        bool _greeted;

        void OnEnable()
        {
            car.OnStateChanged += HandleState;
            car.OnBlip += HandleBlip;
            car.OnBurst += HandleBurst;
        }

        void OnDisable()
        {
            car.OnStateChanged -= HandleState;
            car.OnBlip -= HandleBlip;
            car.OnBurst -= HandleBurst;
        }

        void HandleBlip() => PlayRev(BlipSeconds);

        void HandleBurst() => PlayRev(BurstSeconds);

        void HandleState(CarState state)
        {
            switch (state)
            {
                case CarState.Waking:
                    Play(startUp);
                    if (!engine.isPlaying && engineLoop != null)
                    {
                        float delay = startUp != null ? startUp.length * EngineTakeOverShare : 0f;
                        engine.clip = engineLoop;
                        engine.loop = true;
                        engine.volume = 0f;
                        engine.PlayDelayed(delay);
                        _engineStartsAt = Time.time + delay;
                    }
                    break;
                case CarState.Idle when !_greeted && _previous == CarState.Waking:
                    _greeted = true;
                    Play(greeting);
                    break;
                case CarState.Revving:
                    PlayRev(float.MaxValue);
                    break;
                case CarState.Flying when car.Maneuver == CarManeuver.FlyBy:
                    Play(flyBy);
                    break;
            }
            if (_previous == CarState.Revving && state != CarState.Revving) StopRev();
            _previous = state;
        }

        void Update()
        {
            float now = Time.time;

            if (car.StoppingSoon && car.PathId != _brakedPath)
            {
                _brakedPath = car.PathId;
                Play(car.State == CarState.Inspect ? handBrake : brake);
            }

            if (revSource != null && revSource.isPlaying)
            {
                if (now >= _revStopAt && _revFadeFrom == float.MaxValue) _revFadeFrom = now;
                float fade = _revFadeFrom == float.MaxValue ? 1f : 1f - (now - _revFadeFrom) / RevFadeSeconds;
                if (fade <= 0f)
                {
                    revSource.Stop();
                    _revFadeFrom = float.MaxValue;
                }
                else revSource.volume = Mathf.Clamp01(fade) * oneShotVolume * Volume;
            }

            if (!engine.isPlaying && now < _engineStartsAt) return;
            var s = car.Settings;
            float fadeIn = Mathf.Clamp01((now - _engineStartsAt) / EngineFadeInSeconds);
            engine.pitch = Mathf.Lerp(s.idlePitch, s.revPitch, car.Throttle);
            engine.volume = car.State == CarState.Dormant ? 0f : volumeByThrottle.Evaluate(car.Throttle) * fadeIn * Volume;
            oneShots.volume = Volume;
        }

        void PlayRev(float seconds)
        {
            if (revSource == null || rev == null) return;
            revSource.clip = rev;
            revSource.loop = false;
            revSource.time = 0f;
            revSource.volume = oneShotVolume * Volume;
            revSource.Play();
            _revFadeFrom = float.MaxValue;
            _revStopAt = seconds == float.MaxValue ? float.MaxValue : Time.time + seconds;
        }

        void StopRev()
        {
            if (revSource != null && revSource.isPlaying && _revFadeFrom == float.MaxValue) _revFadeFrom = Time.time;
        }

        void Play(AudioClip clip)
        {
            if (clip != null) oneShots.PlayOneShot(clip, oneShotVolume);
        }
    }
}

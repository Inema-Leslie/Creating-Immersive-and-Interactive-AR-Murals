using System;
using UnityEngine;

namespace MuralAR
{
    public sealed class StoryPlayer : MonoBehaviour
    {
        const double StartDelay = 0.1;

        [SerializeField] MuralConfig config;
        [SerializeField] ExperienceController experience;
        [SerializeField] AudioSource voice;
        [SerializeField] AudioSource music;

        public event Action OnStoryStarted;
        public event Action OnStoryFinished;

        public bool IsPlaying { get; private set; }

        public float Elapsed { get; private set; }

        public float VoiceSeconds => voice != null && voice.clip != null ? voice.clip.length : 0f;

        public float Duration => VoiceSeconds + config.storyTailSeconds;

        void OnEnable() => experience.OnStateChanged += HandleState;
        void OnDisable() => experience.OnStateChanged -= HandleState;

        void HandleState(ExperienceState state)
        {
            if (state == ExperienceState.Savanna) Begin();
            else if (state == ExperienceState.Scanning) Stop();
        }

        public void Begin()
        {
            if (IsPlaying) return;
            IsPlaying = true;
            Elapsed = 0f;
            double at = AudioSettings.dspTime + StartDelay;
            if (voice != null)
            {
                voice.volume = config.voiceVolume;
                voice.PlayScheduled(at);
            }
            if (music != null)
            {
                music.volume = config.storyMusicVolume;
                music.PlayScheduled(at);
            }
            OnStoryStarted?.Invoke();
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            if (voice != null) voice.Stop();
            if (music != null) music.Stop();
        }

        void Update()
        {
            if (!IsPlaying) return;
            Elapsed += Time.deltaTime;
            if (Elapsed < Duration) return;
            IsPlaying = false;
            OnStoryFinished?.Invoke();
            experience.GoTo(ExperienceState.Complete);
        }
    }
}

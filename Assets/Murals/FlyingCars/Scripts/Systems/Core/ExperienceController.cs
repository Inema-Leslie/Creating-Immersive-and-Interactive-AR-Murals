using System;
using UnityEngine;

namespace MuralAR
{
    public enum ExperienceState
    {
        Scanning,
        Awakening,
        CarsActive,
        WomanEmerging,
        WomanReady,
        Savanna,
        Complete,
    }

    public sealed class ExperienceController : MonoBehaviour
    {
        [SerializeField] MuralTracker tracker;
        [SerializeField, Tooltip("Start looking for the mural immediately. Turned off once the start screen exists (M7).")]
        bool beginScanningOnStart = true;

        public event Action<ExperienceState> OnStateChanged;

        public ExperienceState State { get; private set; } = ExperienceState.Scanning;

        void OnEnable() => tracker.OnMuralFound += HandleMuralFound;

        void OnDisable() => tracker.OnMuralFound -= HandleMuralFound;

        void Start()
        {
            if (beginScanningOnStart) Restart();
        }

        public void GoTo(ExperienceState next)
        {
            if (next == State) return;
            State = next;
            OnStateChanged?.Invoke(next);
        }

        public void CallWoman()
        {
            if (State == ExperienceState.CarsActive) GoTo(ExperienceState.WomanEmerging);
        }

        public void Restart()
        {
            tracker.BeginScanning();
            if (State == ExperienceState.Scanning) return;
            State = ExperienceState.Scanning;
            OnStateChanged?.Invoke(State);
        }

        void HandleMuralFound(Pose pose)
        {
            if (State == ExperienceState.Scanning) GoTo(ExperienceState.Awakening);
        }
    }
}

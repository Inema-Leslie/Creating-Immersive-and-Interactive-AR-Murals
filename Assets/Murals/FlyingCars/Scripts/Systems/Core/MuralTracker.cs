using System;
using UnityEngine;

namespace MuralAR
{
    public enum TrackingLoss
    {
        None,
        Holding,
        Hinting,
        Paused,
    }

    public sealed class MuralTracker : MonoBehaviour
    {
        public static readonly Quaternion ImageToMural = Quaternion.Euler(90f, 0f, 0f);

        [SerializeField] MuralConfig config;
        [SerializeField, Tooltip("Child of the mural root, rotated by ImageToMural. All content goes under it.")]
        Transform anchor;

        public event Action<Pose> OnMuralFound;
        public event Action OnMuralLost;
        public event Action OnMuralRelocalized;
        public event Action<TrackingLoss> OnLossChanged;

        public bool IsScanning { get; private set; }
        public bool IsFound { get; private set; }
        public bool IsTracking { get; private set; }
        public TrackingLoss Loss { get; private set; }
        public Vector2 MuralSizeMeters { get; private set; }
        public Transform Anchor => anchor;
        public MuralConfig Config => config;

        public float SecondsSinceSeen => IsFound && !IsTracking ? Time.time - _lostAt : 0f;

        float _lostAt;

        void Awake()
        {
            if (MuralSizeMeters == Vector2.zero) MuralSizeMeters = new Vector2(config.imageWidthMeters, config.imageWidthMeters * config.imageHeightToWidth);
        }

        public void SetSize(Vector2 metres)
        {
            if (metres.x > 0f && metres.y > 0f) MuralSizeMeters = metres;
        }

        public void BeginScanning()
        {
            IsScanning = true;
            IsFound = false;
            IsTracking = false;
            SetLoss(TrackingLoss.None);
        }

        public void ReportFound()
        {
            if (!IsScanning) return;
            if (!IsFound)
            {
                IsFound = true;
                IsTracking = true;
                SetLoss(TrackingLoss.None);
                OnMuralFound?.Invoke(new Pose(anchor.position, anchor.rotation));
                return;
            }
            if (IsTracking) return;
            IsTracking = true;
            SetLoss(TrackingLoss.None);
            OnMuralRelocalized?.Invoke();
        }

        public void ReportLost()
        {
            if (!IsFound || !IsTracking) return;
            IsTracking = false;
            _lostAt = Time.time;
            SetLoss(TrackingLoss.Holding);
            OnMuralLost?.Invoke();
        }

        void Update()
        {
            if (!IsFound || IsTracking) return;
            float since = Time.time - _lostAt;
            SetLoss(since >= config.lossPauseSeconds ? TrackingLoss.Paused
                : since >= config.lossHoldSeconds ? TrackingLoss.Hinting
                : TrackingLoss.Holding);
        }

        void SetLoss(TrackingLoss loss)
        {
            if (loss == Loss) return;
            Loss = loss;
            OnLossChanged?.Invoke(loss);
        }
    }
}

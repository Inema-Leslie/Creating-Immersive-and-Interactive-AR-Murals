using System;
using System.Collections;
using UnityEngine;

namespace MuralAR
{
    public enum CarState
    {
        Dormant,
        Waking,
        Idle,
        Revving,
        Flying,
        Inspect,
        Returning,
    }

    public enum CarManeuver
    {
        None,
        Launch,
        FlyBy,
        Inspect,
        Return,
        Orbit,
        Portal,
    }

    public sealed class CarController : MonoBehaviour
    {
        const float IdleThrottle = 0.15f, WakingThrottle = 0.3f, FlyingThrottle = 0.6f, InspectThrottle = 0.1f, ReturnThrottle = 0.4f;
        const float WakeFlickerSeconds = 1.5f;
        const float ThrottleEase = 3f;
        const float SteerSharpness = 3f;
        const float SteerMaxSpeed = 6f;
        const float PortalScale = 0.04f;

        [SerializeField] MuralConfig config;
        [SerializeField, Tooltip("0 = Car A, 1 = Car B (settings in MuralConfig).")]
        int carIndex;
        [SerializeField] CarPathFollower path;
        [SerializeField, Tooltip("The visual model, offset for bob and shake.")]
        Transform model;
        [SerializeField, Tooltip("Model length and width at scale 1 (set by the setup script).")]
        Vector2 modelLengthWidth = new(1f, 0.5f);

        public event Action<CarState> OnStateChanged;

        public event Action OnBlip;

        public event Action OnBurst;

        public CarState State { get; private set; } = CarState.Dormant;
        public CarManeuver Maneuver { get; private set; }
        public float Throttle { get; private set; }
        public CarSettings Settings => config.Car(carIndex);
        public int Index => carIndex;
        public Vector3 HoverSpot { get; private set; }

        public bool IsSolid => model.gameObject.activeSelf && transform.localScale.x > _flyingScale * 0.3f;

        public bool CanBeNudged => (State is CarState.Flying or CarState.Returning or CarState.Waking) && !_scripted;

        public int PathId => path.PathId;
        public bool StoppingSoon => path.IsMoving && !path.IsLooping && path.RemainingDistance < path.Speed * config.carBrakeLeadSeconds;

        MuralTracker _tracker;
        Camera _camera;
        Vector3 _paintedSpot;
        float _paintedScale;
        float _flyingScale;
        float _revTime;
        float _bobPhase;
        bool _scripted;
        Vector3 _steerTarget;
        Vector3 _steerForward;
        float _steerBank;
        bool _steerKeepClear;
        BoxCollider _body;

        public void Init(MuralTracker tracker, Camera viewCamera)
        {
            _tracker = tracker;
            _camera = viewCamera;
            var s = Settings;
            var size = tracker.MuralSizeMeters;
            _paintedSpot = new Vector3((s.paintedCenter.x - 0.5f) * size.x, (0.5f - s.paintedCenter.y) * size.y, -0.05f);

            float ground = -size.y * 0.5f;
            float top = ground + config.carFlightFloor + config.carFlightVolume.y;
            HoverSpot = new Vector3(_paintedSpot.x + s.hoverOffset.x,
                Mathf.Clamp(_paintedSpot.y, ground + config.carFlightFloor, top) + s.hoverOffset.y,
                -config.carHoverDistanceFromWall);

            _paintedScale = s.paintedSize.x * size.x / modelLengthWidth.y;
            _flyingScale = s.flyingLengthMeters / modelLengthWidth.x;
            _bobPhase = carIndex * 1.7f;
            _body = GetComponent<BoxCollider>();

            transform.localPosition = _paintedSpot;
            transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            transform.localScale = Vector3.one * _paintedScale;
            path.Constrain = KeepClearOfCamera;
            path.OnPathComplete += HandlePathComplete;
            model.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (path != null) path.OnPathComplete -= HandlePathComplete;
        }

        public void Wake()
        {
            if (State == CarState.Dormant) StartCoroutine(WakeSequence());
        }

        public void Blip()
        {
            if (State == CarState.Idle) OnBlip?.Invoke();
        }

        public void StartRev()
        {
            if (State != CarState.Idle) return;
            _revTime = 0f;
            SetState(CarState.Revving);
        }

        public void StopRev()
        {
            if (State == CarState.Revving) SetState(CarState.Idle);
        }

        public void Launch(Vector3 localDirection)
        {
            if (State is not (CarState.Idle or CarState.Revving)) return;
            var dir = localDirection.sqrMagnitude > 1e-4f ? localDirection.normalized : Vector3.back;
            var far = ClampToVolume(HoverSpot + dir * config.carLaunchDistance);
            var side = Vector3.Cross(Vector3.up, dir).normalized * Settings.loopRadius;
            var swing = ClampToVolume(far + side);
            Maneuver = CarManeuver.Launch;
            SetState(CarState.Flying);
            OnBurst?.Invoke();
            path.FlyPath(new[] { far, swing, HoverSpot }, Settings.cruiseSpeed * 1.4f, Settings.bankDegrees);
        }

        public void FlyBy()
        {
            if (State is not (CarState.Idle or CarState.Revving)) return;
            var cam = CameraLocal();
            var toCam = cam - HoverSpot;
            toCam.y = 0f;
            var right = Vector3.Cross(Vector3.up, toCam.normalized);
            float pass = config.carMinCameraDistance + 0.4f;
            var beside = cam + right * pass + Vector3.down * 0.2f;
            var beyond = cam + right * pass * 1.6f - toCam.normalized * 1.2f;
            Maneuver = CarManeuver.FlyBy;
            SetState(CarState.Flying);
            path.FlyPath(new[] { beside, beyond, ClampToVolume(HoverSpot + right * 1.5f), HoverSpot }, Settings.cruiseSpeed * 2.4f, Settings.bankDegrees * 1.5f);
        }

        public void Inspect()
        {
            if (State is not (CarState.Idle or CarState.Revving)) return;
            var cam = CameraLocal();
            var toCar = HoverSpot - cam;
            var spot = cam + toCar.normalized * Mathf.Max(config.carInspectDistance, config.carMinCameraDistance + 0.3f);
            Maneuver = CarManeuver.Inspect;
            SetState(CarState.Inspect);
            path.FlyPath(new[] { spot }, Settings.cruiseSpeed, Settings.bankDegrees);
        }

        public void EndInspect()
        {
            if (State == CarState.Inspect) ReturnHome();
        }

        public void ReturnHome()
        {
            if (State is CarState.Dormant or CarState.Waking) return;
            StopScripted();
            Maneuver = CarManeuver.Return;
            SetState(CarState.Returning);
            path.FlyPath(new[] { HoverSpot }, Settings.cruiseSpeed, Settings.bankDegrees);
        }

        public void Steer(Vector3 worldTarget, Vector3 worldForward, float bankDegrees, bool keepClearOfCamera)
        {
            if (State is CarState.Dormant or CarState.Waking || _scripted) return;
            if (Maneuver != CarManeuver.Orbit || State != CarState.Flying)
            {
                path.Stop();
                Maneuver = CarManeuver.Orbit;
                SetState(CarState.Flying);
            }
            _steerTarget = worldTarget;
            _steerForward = worldForward;
            _steerBank = bankDegrees;
            _steerKeepClear = keepClearOfCamera;
        }

        public void FlyToAndHover(Vector3 localPoint)
        {
            if (State is CarState.Dormant or CarState.Waking) return;
            Maneuver = CarManeuver.Portal;
            SetState(CarState.Flying);
            path.FlyPath(new[] { localPoint }, Settings.cruiseSpeed, Settings.bankDegrees);
        }

        public bool IsOnPath => path.IsMoving;

        public IEnumerator DiveIntoPortal(Vector3 localFront, Vector3 localCentre, float seconds)
        {
            _scripted = true;
            path.Stop();
            Maneuver = CarManeuver.Portal;
            SetState(CarState.Flying);
            OnBurst?.Invoke();
            var start = transform.localPosition;
            var startRotation = transform.localRotation;
            var into = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            float startScale = transform.localScale.x;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                var p = Vector3.Lerp(Vector3.Lerp(start, localFront, k), Vector3.Lerp(localFront, localCentre, k), k);
                transform.localPosition = p;
                transform.localRotation = Quaternion.Slerp(startRotation, into, Mathf.SmoothStep(0f, 1f, k * 1.6f));
                transform.localScale = Vector3.one * Mathf.Lerp(startScale, _flyingScale * PortalScale, k * k);
                yield return null;
            }
            model.gameObject.SetActive(false);
        }

        public IEnumerator ComeOutOfPortal(Vector3 localCentre, Vector3 localTarget, float seconds)
        {
            _scripted = true;
            transform.localPosition = localCentre;
            transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            transform.localScale = Vector3.one * _flyingScale * PortalScale;
            model.gameObject.SetActive(true);
            var outward = localCentre + new Vector3(0f, 0.4f, -1.6f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                var p = Vector3.Lerp(Vector3.Lerp(localCentre, outward, k), Vector3.Lerp(outward, localTarget, k), k);
                var previous = transform.localPosition;
                transform.localPosition = p;
                var dir = p - previous;
                dir.y *= 0.3f;
                if (dir.sqrMagnitude > 1e-6f)
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 5f);
                transform.localScale = Vector3.one * Mathf.Lerp(_flyingScale * PortalScale, _flyingScale, Mathf.Clamp01(t / (seconds * 0.4f)));
                yield return null;
            }
            transform.localPosition = localTarget;
            transform.localScale = Vector3.one * _flyingScale;
            _scripted = false;
            _steerTarget = transform.position;
            _steerForward = transform.forward;
        }

        void StopScripted()
        {
            _scripted = false;
            if (!model.gameObject.activeSelf && State != CarState.Dormant) model.gameObject.SetActive(true);
        }

        public void BodyBox(out Vector3 centre, out Quaternion rotation, out Vector3 halfExtents)
        {
            centre = transform.TransformPoint(_body.center);
            rotation = transform.rotation;
            halfExtents = Vector3.Scale(_body.size * (0.5f / 1.1f), transform.lossyScale);
        }

        IEnumerator WakeSequence()
        {
            SetState(CarState.Waking);
            model.gameObject.SetActive(true);
            yield return new WaitForSeconds(WakeFlickerSeconds);

            var startPos = transform.localPosition;
            var startRot = transform.localRotation;
            var endRot = Quaternion.LookRotation(Vector3.back + Vector3.right * 0.3f * (carIndex == 0 ? -1f : 1f), Vector3.up);
            for (float t = 0f; t < config.carLiftOffSeconds; t += Time.deltaTime)
            {
                float k = t / config.carLiftOffSeconds;
                float outK = Phase(k, 0f, 0.4f);
                float sideK = Phase(k, 0.3f, 0.7f);
                float downK = Phase(k, 0.6f, 1f);
                transform.localPosition = new Vector3(Mathf.Lerp(startPos.x, HoverSpot.x, sideK), Mathf.Lerp(startPos.y, HoverSpot.y, downK), Mathf.Lerp(startPos.z, HoverSpot.z, outK));
                transform.localRotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, k));
                transform.localScale = Vector3.one * Mathf.Lerp(_paintedScale, _flyingScale, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            transform.localPosition = HoverSpot;
            transform.localScale = Vector3.one * _flyingScale;
            SetState(CarState.Idle);
        }

        static float Phase(float k, float from, float to) => Mathf.SmoothStep(0f, 1f, (k - from) / (to - from));

        void HandlePathComplete()
        {
            if (State is not (CarState.Flying or CarState.Returning)) return;
            if (Maneuver == CarManeuver.Portal) return;
            Maneuver = CarManeuver.None;
            SetState(CarState.Idle);
        }

        void Update()
        {
            if (_tracker == null) return;
            float dt = Time.deltaTime;

            float target = State switch
            {
                CarState.Dormant => 0f,
                CarState.Waking => WakingThrottle,
                CarState.Revving => Mathf.Clamp01((_revTime += dt) / config.holdToFullRevSeconds),
                CarState.Flying => FlyingThrottle,
                CarState.Inspect => InspectThrottle,
                CarState.Returning => ReturnThrottle,
                _ => IdleThrottle,
            };
            Throttle = State == CarState.Revving ? target : Mathf.MoveTowards(Throttle, target, dt * ThrottleEase);

            float bob = State is CarState.Idle or CarState.Revving or CarState.Inspect
                ? Mathf.Sin(Time.time * 1.6f + _bobPhase) * config.carHoverBob / Mathf.Max(0.01f, transform.localScale.y)
                : 0f;
            var shake = State == CarState.Revving ? UnityEngine.Random.insideUnitSphere * (0.01f * Throttle) : Vector3.zero;
            model.localPosition = new Vector3(0f, bob, 0f) + shake;

            if (Maneuver == CarManeuver.Orbit && State == CarState.Flying && !_scripted)
                SteerStep(dt);
            else if (Maneuver == CarManeuver.Portal && State == CarState.Flying && !_scripted && !path.IsMoving)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(Vector3.back, Vector3.up), dt * 2f);
            else if (State == CarState.Inspect && !path.IsMoving)
                transform.localRotation *= Quaternion.Euler(0f, 20f * dt, 0f);
            else if (State == CarState.Idle)
                FaceViewerSlowly(dt);
        }

        void SteerStep(float dt)
        {
            var target = _steerTarget;
            if (_steerKeepClear)
                target = _tracker.Anchor.TransformPoint(KeepClearOfCamera(_tracker.Anchor.InverseTransformPoint(target)));
            var position = transform.position;
            var next = Vector3.Lerp(position, target, 1f - Mathf.Exp(-SteerSharpness * dt));
            transform.position = Vector3.MoveTowards(position, next, SteerMaxSpeed * dt);

            var toTarget = target - position;
            var facing = toTarget.sqrMagnitude > 1f ? toTarget : _steerForward;
            facing.y *= 0.3f;
            if (facing.sqrMagnitude < 1e-6f) return;
            var rotation = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(0f, 0f, _steerBank);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, dt * 3f);
        }

        void FaceViewerSlowly(float dt)
        {
            var toCam = CameraLocal() - transform.localPosition;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.LookRotation(Quaternion.Euler(0f, carIndex == 0 ? 35f : -35f, 0f) * toCam, Vector3.up);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, dt * 1.5f);
        }

        Vector3 CameraLocal() => _tracker.Anchor.InverseTransformPoint(_camera.transform.position);

        public Vector3 VolumeCentre()
        {
            float ground = -_tracker.MuralSizeMeters.y * 0.5f;
            var v = config.carFlightVolume;
            return new Vector3(0f, ground + config.carFlightFloor + v.y * 0.5f, -0.5f - v.z * 0.5f);
        }

        Vector3 ClampToVolume(Vector3 p)
        {
            var c = VolumeCentre();
            var h = config.carFlightVolume * 0.5f;
            return new Vector3(Mathf.Clamp(p.x, c.x - h.x, c.x + h.x), Mathf.Clamp(p.y, c.y - h.y, c.y + h.y), Mathf.Clamp(p.z, c.z - h.z, c.z + h.z));
        }

        Vector3 KeepClearOfCamera(Vector3 p)
        {
            var cam = CameraLocal();
            var away = p - cam;
            float min = config.carMinCameraDistance;
            if (away.sqrMagnitude >= min * min) return p;
            if (away.sqrMagnitude < 1e-6f) away = Vector3.up;
            return cam + away.normalized * min;
        }

        void SetState(CarState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }
    }
}

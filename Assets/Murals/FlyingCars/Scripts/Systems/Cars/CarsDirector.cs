using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MuralAR
{
    public sealed class CarsDirector : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => viewCamera = refs.Camera;

        enum Formation
        {
            Free,
            Orbit,
            Procession,
            Swirl,
        }

        [SerializeField] MuralConfig config;
        [SerializeField] MuralTracker tracker;
        [SerializeField] ExperienceController experience;
        [SerializeField] WomanController woman;
        [SerializeField] Camera viewCamera;
        [SerializeField, Tooltip("Car A then Car B.")] CarController[] carPrefabs = Array.Empty<CarController>();

        public event Action<Vector3> OnFollowedHerThroughPortal;

        readonly List<CarController> _cars = new();
        readonly List<CarAudio> _audio = new();
        Formation _formation;
        float _angle;
        float _swirlTime;
        Vector3 _swirlCentre;
        readonly Vector3[] _escort = new Vector3[2];
        readonly bool[] _escorting = new bool[2];

        public IReadOnlyList<CarController> Cars => _cars;

        public Vector3 SwirlCentre => _swirlCentre;

        public float Clearance { get; private set; } = float.PositiveInfinity;

        void OnEnable()
        {
            tracker.OnMuralFound += HandleFound;
            tracker.OnLossChanged += HandleLoss;
            experience.OnStateChanged += HandleState;
            woman.OnEmerged += HandleEmerged;
        }

        void OnDisable()
        {
            tracker.OnMuralFound -= HandleFound;
            tracker.OnLossChanged -= HandleLoss;
            experience.OnStateChanged -= HandleState;
            woman.OnEmerged -= HandleEmerged;
        }

        void HandleFound(Pose pose)
        {
            if (_cars.Count > 0) return;
            foreach (var prefab in carPrefabs)
            {
                var car = Instantiate(prefab, tracker.Anchor);
                car.Init(tracker, viewCamera);
                _cars.Add(car);
                _audio.Add(car.GetComponent<CarAudio>());
            }
            if (experience.State == ExperienceState.Awakening) StartCoroutine(WakeCars());
        }

        IEnumerator WakeCars()
        {
            yield return new WaitForSeconds(config.carsWakeAfterSeconds);
            float waited = 0f;
            foreach (var car in _cars)
            {
                float delay = car.Settings.wakeDelaySeconds - waited;
                if (delay > 0f) yield return new WaitForSeconds(delay);
                waited += Mathf.Max(0f, delay);
                car.Wake();
            }
            while (!AllAwake()) yield return null;
            if (experience.State == ExperienceState.Awakening) experience.GoTo(ExperienceState.CarsActive);
        }

        bool AllAwake()
        {
            foreach (var car in _cars)
                if (car.State is CarState.Dormant or CarState.Waking) return false;
            return true;
        }

        void HandleState(ExperienceState state)
        {
            switch (state)
            {
                case ExperienceState.WomanEmerging:
                    foreach (var car in _cars)
                        if (car.State == CarState.Dormant) car.Wake();
                    StartOrbit();
                    break;
                case ExperienceState.Savanna:
                    StartSwirl();
                    foreach (var a in _audio)
                        if (a != null) a.Volume = config.carVolumeDuringStory;
                    break;
                case ExperienceState.Complete:
                    foreach (var a in _audio)
                        if (a != null) a.Volume = 1f;
                    break;
            }
        }

        void HandleLoss(TrackingLoss loss)
        {
            if (loss != TrackingLoss.Paused || experience.State is not (ExperienceState.Awakening or ExperienceState.CarsActive)) return;
            foreach (var car in _cars)
                if (car.State is CarState.Flying or CarState.Revving or CarState.Inspect) car.ReturnHome();
        }

        void StartOrbit()
        {
            if (_cars.Count == 0) return;
            _formation = Formation.Orbit;
            var a = tracker.Anchor.InverseTransformPoint(_cars[0].transform.position);
            float bx = _cars.Count > 1 ? tracker.Anchor.InverseTransformPoint(_cars[1].transform.position).x : float.MaxValue;
            _angle = a.x <= bx ? Mathf.PI : 0f;
        }

        void UpdateOrbit(float dt)
        {
            var radii = config.carEmergeOrbitRadii;
            _angle += config.carOrbitSpeed / Mathf.Max(0.1f, (radii.x + radii.y) * 0.5f) * dt;
            var centre = _cars[0].VolumeCentre();
            for (int i = 0; i < _cars.Count; i++)
            {
                float angle = _angle + i * Mathf.PI;
                var local = centre + new Vector3(Mathf.Cos(angle) * radii.x, i == 0 ? 0.35f : -0.35f, Mathf.Sin(angle) * radii.y);
                var tangent = new Vector3(-Mathf.Sin(angle) * radii.x, 0f, Mathf.Cos(angle) * radii.y);
                var anchor = tracker.Anchor;
                _cars[i].Steer(anchor.TransformPoint(local), anchor.TransformDirection(tangent), _cars[i].Settings.bankDegrees * 0.6f, true);
            }
        }

        void HandleEmerged(Vector3 groundPoint) => StartCoroutine(Procession(groundPoint));

        IEnumerator Procession(Vector3 groundPoint)
        {
            while (!AllAwake()) yield return null;
            while (_formation == Formation.Orbit && Mathf.Abs(Mathf.Cos(_angle)) < 0.97f) yield return null;
            if (_cars.Count > 0)
            {
                _formation = Formation.Procession;
                var anchor = tracker.Anchor;
                var portal = woman.PortalCentreLocal;
                var her = anchor.InverseTransformPoint(groundPoint);
                var front = portal + new Vector3(0f, 0f, -1.4f);

                var order = new List<CarController>(_cars);
                order.Sort((p, q) => anchor.InverseTransformPoint(p.transform.position).x.CompareTo(anchor.InverseTransformPoint(q.transform.position).x));
                for (int i = 0; i < order.Count; i++)
                {
                    float side = order.Count == 1 ? 0f : (i == 0 ? -1f : 1f);
                    order[i].FlyToAndHover(portal + new Vector3(side * config.carPortalQueueSide, 0.3f, -1.8f));
                }
                foreach (var car in order)
                    while (car.IsOnPath) yield return null;

                for (int i = 0; i < _cars.Count; i++)
                {
                    var car = _cars[i];
                    int slot = order.IndexOf(car);
                    float side = order.Count == 1 ? 0f : (slot == 0 ? -1f : 1f);
                    var escortLocal = new Vector3(her.x + side * config.carEscortSide, her.y + config.carEscortHeight - 0.4f * i, her.z);
                    yield return car.DiveIntoPortal(front, portal, config.carPortalDiveSeconds);
                    yield return new WaitForSeconds(config.carPortalHiddenSeconds);
                    yield return car.ComeOutOfPortal(portal, escortLocal, config.carPortalExitSeconds);
                    _escort[i] = anchor.TransformPoint(escortLocal);
                    _escorting[i] = true;
                }
            }
            woman.ClosePortal();
            OnFollowedHerThroughPortal?.Invoke(groundPoint);
        }

        void UpdateEscort()
        {
            for (int i = 0; i < _cars.Count; i++)
            {
                if (!_escorting[i]) continue;
                var facing = woman.Woman.forward;
                facing.y = 0f;
                _cars[i].Steer(_escort[i], facing.sqrMagnitude > 1e-4f ? facing : _cars[i].transform.forward, 0f, false);
            }
        }

        void StartSwirl()
        {
            if (_cars.Count == 0 || woman.Woman == null) return;
            _formation = Formation.Swirl;
            _swirlTime = 0f;
            _swirlCentre = woman.Woman.position + Vector3.up * config.carSwirlHeight;
            var fromCentre = _cars[0].transform.position - _swirlCentre;
            _angle = Mathf.Atan2(fromCentre.z, fromCentre.x);
        }

        void UpdateSwirl(float dt)
        {
            _swirlTime += dt;
            var r = config.carSwirlRadius;
            float radius = Mathf.Lerp(r.x, r.y, 0.5f + 0.5f * Mathf.Sin(_swirlTime * 0.21f));
            _angle += config.carSwirlSpeed / radius * dt;

            float drift = config.carSwirlDrift;
            var target = woman.Woman.position + new Vector3(Mathf.Sin(_swirlTime * 0.05f) * drift, config.carSwirlHeight, Mathf.Cos(_swirlTime * 0.07f) * drift);
            _swirlCentre = Vector3.Lerp(_swirlCentre, target, 1f - Mathf.Exp(-0.8f * dt));

            float lift = Mathf.Sin(_angle * 0.5f) * config.carSwirlLift;
            for (int i = 0; i < _cars.Count; i++)
            {
                float sign = i == 0 ? 1f : -1f;
                var dir = new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * sign;
                var tangent = new Vector3(-Mathf.Sin(_angle), 0f, Mathf.Cos(_angle)) * sign;
                _cars[i].Steer(_swirlCentre + dir * radius + Vector3.up * (lift * sign), tangent, _cars[i].Settings.bankDegrees * 0.7f, false);
            }
        }

        void Update()
        {
            if (_cars.Count == 0) return;
            float dt = Time.deltaTime;
            switch (_formation)
            {
                case Formation.Orbit: UpdateOrbit(dt); break;
                case Formation.Procession: UpdateEscort(); break;
                case Formation.Swirl: UpdateSwirl(dt); break;
            }
        }

        void LateUpdate()
        {
            Clearance = float.PositiveInfinity;
            if (_cars.Count < 2 || !_cars[0].IsSolid || !_cars[1].IsSolid) return;
            var a = _cars[0];
            var b = _cars[1];
            a.BodyBox(out var ca, out var ra, out var ha);
            b.BodyBox(out var cb, out var rb, out var hb);

            float gapAB = SurfaceGap(ca, ra, ha, cb, rb, hb, out var pushAB);
            float gapBA = SurfaceGap(cb, rb, hb, ca, ra, ha, out var pushBA);
            var push = gapAB <= gapBA ? pushAB : -pushBA;
            Clearance = Mathf.Min(gapAB, gapBA);
            float overlap = config.carMinClearance - Clearance;
            if (overlap <= 0f) return;

            bool moveA = a.CanBeNudged, moveB = b.CanBeNudged;
            if (!moveA && !moveB) moveA = moveB = true;
            float shareA = moveA && moveB ? 0.5f : moveA ? 1f : 0f;
            a.transform.position += push * (overlap * shareA);
            b.transform.position -= push * (overlap * (1f - shareA));
        }

        static float SurfaceGap(Vector3 pc, Quaternion pr, Vector3 ph, Vector3 qc, Quaternion qr, Vector3 qh, out Vector3 away)
        {
            float best = float.PositiveInfinity;
            away = (pc - qc).sqrMagnitude > 1e-8f ? (pc - qc).normalized : Vector3.up;
            var toQ = Quaternion.Inverse(qr);
            for (int x = -2; x <= 2; x++)
            for (int y = -2; y <= 2; y++)
            for (int z = -2; z <= 2; z++)
            {
                if (Mathf.Abs(x) != 2 && Mathf.Abs(y) != 2 && Mathf.Abs(z) != 2) continue;
                var p = pc + pr * new Vector3(ph.x * x * 0.5f, ph.y * y * 0.5f, ph.z * z * 0.5f);
                var local = toQ * (p - qc);
                var clamped = new Vector3(Mathf.Clamp(local.x, -qh.x, qh.x), Mathf.Clamp(local.y, -qh.y, qh.y), Mathf.Clamp(local.z, -qh.z, qh.z));
                var outside = local - clamped;
                float d = outside.sqrMagnitude > 1e-10f
                    ? outside.magnitude
                    : -Mathf.Min(qh.x - Mathf.Abs(local.x), Mathf.Min(qh.y - Mathf.Abs(local.y), qh.z - Mathf.Abs(local.z)));
                if (d >= best) continue;
                best = d;
                if (d > 0f) away = qr * (outside / d);
            }
            return best;
        }
    }
}

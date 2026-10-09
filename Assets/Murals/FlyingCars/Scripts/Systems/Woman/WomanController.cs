using System;
using System.Collections;
using UnityEngine;

namespace MuralAR
{
    public sealed class WomanController : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => viewCamera = refs.Camera;

        public const string WalkingParam = "Walking";
        public const string WaveParam = "Wave";
        public const string TalkParam = "Talk";
        public const string PointParam = "Point";

        static readonly int WalkingId = Animator.StringToHash(WalkingParam);
        static readonly int WaveId = Animator.StringToHash(WaveParam);

        [SerializeField] MuralConfig config;
        [SerializeField] MuralTracker tracker;
        [SerializeField] ExperienceController experience;
        [SerializeField] Camera viewCamera;
        [SerializeField, Tooltip("The AMANIRENAS prefab (her materials use the Dissolve_Metallic shader).")]
        GameObject womanPrefab;
        [SerializeField, Tooltip("The magic portal she comes through (Hovl Magic Effects \"Portal yellow\").")]
        GameObject portalPrefab;
        [SerializeField, Tooltip("Sparkles layered on the portal (Hovl \"Sparks flashing yellow\"). Optional.")]
        GameObject portalSparksPrefab;
        [SerializeField, Tooltip("Physics layer of her tap areas (Default in the team project).")]
        int womanLayer;

        public event Action<Vector3> OnEmerged;

        public event Action<GameObject, string> OnTapAreaCreated;

        public event Action OnTouched;

        public bool HasEmerged { get; private set; }
        public Transform Woman => _woman != null ? _woman.transform : null;
        public GameObject PaintedTapTarget => _tapTarget;

        public WomanIdleMotion Motion => _motion;

        public Vector3 PortalCentreLocal { get; private set; }

        GameObject _tapTarget;
        GameObject _woman;
        Collider _womanCollider;
        Animator _animator;
        DissolveController _dissolve;
        WomanIdleMotion _motion;
        float _modelHeight;
        GameObject _portal;

        void OnEnable()
        {
            tracker.OnMuralFound += HandleMuralFound;
        }

        void OnDisable()
        {
            tracker.OnMuralFound -= HandleMuralFound;
        }

        void HandleMuralFound(Pose pose)
        {
            if (_tapTarget != null) return;
            _tapTarget = new GameObject("Painted Woman (tap)") { layer = womanLayer };
            _tapTarget.transform.SetParent(tracker.Anchor, false);
            var r = config.paintedWomanRegion;
            var size = tracker.MuralSizeMeters;
            _tapTarget.transform.localPosition = MuralPoint(r.center, 0f);
            var box = _tapTarget.AddComponent<BoxCollider>();
            box.size = new Vector3(r.width * size.x, r.height * size.y, 0.2f);
            OnTapAreaCreated?.Invoke(_tapTarget, "PaintedWoman");
        }

        public void HandleTap(GameObject target)
        {
            if (target == _tapTarget && CanEmerge()) Emerge();
            else if (HasEmerged && _womanCollider != null && target == _womanCollider.gameObject)
            {
                if (_animator != null) _animator.SetTrigger(WaveId);
                OnTouched?.Invoke();
            }
        }

        bool CanEmerge() => !HasEmerged && _woman == null &&
            (experience.State == ExperienceState.Awakening || experience.State == ExperienceState.CarsActive);

        public void Emerge()
        {
            if (_woman != null) return;
            experience.GoTo(ExperienceState.WomanEmerging);
            StartCoroutine(EmergeSequence());
        }

        IEnumerator EmergeSequence()
        {
            var anchor = tracker.Anchor;
            var size = tracker.MuralSizeMeters;
            var region = config.paintedWomanRegion;

            _woman = Instantiate(womanPrefab, anchor);
            _woman.name = "AMANIRENAS";
            _animator = _woman.GetComponentInChildren<Animator>();
            if (!_woman.TryGetComponent(out _dissolve)) _dissolve = _woman.AddComponent<DissolveController>();
            _modelHeight = MeasureHeight(_woman);
            _motion = _woman.AddComponent<WomanIdleMotion>();
            _motion.Init(config, FeetHeight(_woman), _modelHeight);
            _dissolve.SetAmount(1f);

            float paintedHeight = region.height * size.y;
            float startScale = paintedHeight / _modelHeight;
            float endScale = config.womanHeightMeters / _modelHeight;
            var groundY = -size.y * 0.5f;
            var start = new Vector3(MuralPoint(region.center, 0f).x, groundY, -0.05f);

            var cameraLocal = anchor.InverseTransformPoint(viewCamera.transform.position);
            float depth = Mathf.Min(config.womanStandDistanceFromWall, Mathf.Max(0.5f, -cameraLocal.z * 0.5f));
            var end = new Vector3(start.x, groundY, -depth);

            PortalCentreLocal = MuralPoint(region.center, config.portalOffsetFromWall);
            _portal = OpenPortal(anchor, PortalCentreLocal, paintedHeight);
            yield return new WaitForSeconds(config.portalLeadSeconds);

            _woman.transform.localPosition = start;
            _woman.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            _woman.transform.localScale = Vector3.one * startScale;
            _dissolve.SetAmount(1f);

            float dissolveSeconds = config.emergenceSeconds * config.emergenceDissolveShare;
            yield return _dissolve.Animate(1f, 0f, dissolveSeconds);

            if (_animator != null) _animator.SetBool(WalkingId, true);
            _motion.Walking = true;
            float walkSeconds = config.emergenceSeconds - dissolveSeconds;
            var startRotation = _woman.transform.localRotation;
            for (float t = 0f; t < walkSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / walkSeconds);
                _woman.transform.localPosition = Vector3.Lerp(start, end, k);
                _woman.transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, k);
                _woman.transform.localRotation = Quaternion.Slerp(startRotation, FacingCamera(anchor), k);
                yield return null;
            }
            if (_animator != null) _animator.SetBool(WalkingId, false);
            _motion.Walking = false;

            _woman.transform.SetParent(null, true);
            var worldUp = _woman.transform.eulerAngles;
            _woman.transform.rotation = Quaternion.Euler(0f, worldUp.y, 0f);
            _womanCollider = AddBodyCollider(_woman, womanLayer);
            OnTapAreaCreated?.Invoke(_womanCollider.gameObject, "Woman");

            HasEmerged = true;
            experience.GoTo(ExperienceState.WomanReady);
            OnEmerged?.Invoke(_woman.transform.position);
        }

        GameObject OpenPortal(Transform anchor, Vector3 localCentre, float figureHeight)
        {
            if (portalPrefab == null) return null;
            var portal = Instantiate(portalPrefab, anchor);
            portal.name = "Portal";
            portal.transform.localPosition = localCentre;
            portal.transform.localRotation = Quaternion.identity;
            portal.transform.localScale = Vector3.one * (figureHeight * config.portalSizeToFigure / config.portalNativeHeight);

            if (portalSparksPrefab != null)
            {
                var sparks = Instantiate(portalSparksPrefab, portal.transform);
                sparks.name = "Portal Sparks";
                sparks.transform.localPosition = Vector3.zero;
            }

            foreach (var ps in portal.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                foreach (var m in ps.GetComponent<ParticleSystemRenderer>().materials)
                {
                    m.SetFloat("_SoftParticlesEnabled", 0f);
                    m.DisableKeyword("_SOFTPARTICLES_ON");
                }
                ps.Play(false);
            }
            return portal;
        }

        public void ClosePortal()
        {
            if (_portal == null) return;
            StartCoroutine(ClosePortalRoutine(_portal));
            _portal = null;
        }

        IEnumerator ClosePortalRoutine(GameObject portal)
        {
            foreach (var ps in portal.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var start = portal.transform.localScale;
            for (float t = 0f; t < config.portalCloseSeconds; t += Time.deltaTime)
            {
                portal.transform.localScale = start * (1f - Mathf.SmoothStep(0f, 1f, t / config.portalCloseSeconds));
                yield return null;
            }
            foreach (var r in portal.GetComponentsInChildren<ParticleSystemRenderer>())
                foreach (var m in r.materials) Destroy(m);
            Destroy(portal);
        }

        void OnDestroy()
        {
            if (_woman != null) Destroy(_woman);
        }

        public void ResetWoman()
        {
            StopAllCoroutines();
            if (_woman != null) Destroy(_woman);
            if (_portal != null) Destroy(_portal);
            _portal = null;
            _woman = null;
            HasEmerged = false;
        }

        Quaternion FacingCamera(Transform anchor)
        {
            var toCamera = anchor.InverseTransformDirection(viewCamera.transform.position - _woman.transform.position);
            toCamera.y = 0f;
            return toCamera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCamera, Vector3.up) : Quaternion.LookRotation(Vector3.back, Vector3.up);
        }

        Vector3 MuralPoint(Vector2 uvFromTop, float depth)
        {
            var size = tracker.MuralSizeMeters;
            return new Vector3((uvFromTop.x - 0.5f) * size.x, (0.5f - uvFromTop.y) * size.y, -depth);
        }

        static float MeasureHeight(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1f;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return Mathf.Max(0.01f, bounds.size.y / model.transform.lossyScale.y);
        }

        static float FeetHeight(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return model.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y;
        }

        static Collider AddBodyCollider(GameObject model, int layer)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var go = new GameObject("Body (tap)") { layer = layer };
            go.transform.SetParent(model.transform, false);
            var capsule = go.AddComponent<CapsuleCollider>();
            float scale = model.transform.lossyScale.y;
            capsule.height = bounds.size.y / scale;
            capsule.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.3f / scale;
            capsule.center = model.transform.InverseTransformPoint(bounds.center);
            return capsule;
        }
    }
}

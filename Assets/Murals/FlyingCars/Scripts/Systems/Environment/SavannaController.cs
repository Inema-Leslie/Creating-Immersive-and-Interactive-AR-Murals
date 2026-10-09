using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace MuralAR
{
    public sealed class SavannaController : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => (viewCamera, cameraBackground, sun) = (refs.Camera, refs.CameraBackground, refs.Sun);

        [SerializeField] MuralConfig config;
        [SerializeField] MuralTracker tracker;
        [SerializeField] ExperienceController experience;
        [SerializeField, Tooltip("The savanna begins once both cars have followed her through the portal.")]
        CarsDirector cars;
        [SerializeField] Camera viewCamera;
        [SerializeField, Tooltip("Switched off while in the savanna so the sky replaces the camera feed.")]
        ARCameraBackground cameraBackground;
        [SerializeField] Light sun;
        [SerializeField, Tooltip("Root of the savanna world (terrain, trees, leaves, stars, fireflies). Inactive until it begins.")]
        GameObject savannaRoot;
        [SerializeField, Tooltip("Captured from the ARCADE Day scene by the setup script.")]
        LightingPreset dayLighting;
        [SerializeField, Tooltip("Full-screen image used for the gold flash.")]
        Image flash;
        [SerializeField] Color flashColor = new(1f, 0.85f, 0.5f, 1f);

        public bool IsActive { get; private set; }

        public Transform Root => savannaRoot.transform;

        public int TreeCount => _trees.Length;
        public Transform Tree(int index) => _trees[Mathf.Clamp(index, 0, _trees.Length - 1)];

        Terrain[] _terrains;
        Transform[] _trees;
        Transform[] _seated;
        float[] _seatOffsets;
        LightingPreset _arLighting;
        CameraClearFlags _arClearFlags;
        Color _arBackground;
        Transform _home;
        bool _tookOver;
        bool _arBackgroundWasOn;

        void OnEnable()
        {
            cars.OnFollowedHerThroughPortal += HandleArrived;
            experience.OnStateChanged += HandleState;
        }
        void OnDisable()
        {
            cars.OnFollowedHerThroughPortal -= HandleArrived;
            experience.OnStateChanged -= HandleState;
        }

        void HandleState(ExperienceState state)
        {
            if (state == ExperienceState.Complete && IsActive) StartCoroutine(EndSequence());
        }

        IEnumerator EndSequence()
        {
            yield return new WaitForSeconds(config.cinematicReturnSeconds);
            float half = config.savannaFlashSeconds * 0.5f;
            yield return Fade(0f, 1f, half);
            Restore();
            yield return Fade(1f, 0f, half);
        }

        void Awake()
        {
            _terrains = savannaRoot.GetComponentsInChildren<Terrain>(true);
            foreach (var terrain in _terrains) terrain.terrainData = Instantiate(terrain.terrainData);
            _trees = Children("Trees");
            var fireflies = Children("Fireflies");
            _seated = new Transform[_trees.Length + fireflies.Length];
            _trees.CopyTo(_seated, 0);
            fireflies.CopyTo(_seated, _trees.Length);
            _seatOffsets = new float[_seated.Length];
            for (int i = 0; i < _seated.Length; i++) _seatOffsets[i] = _seated[i].position.y - GroundAt(_seated[i].position);
            savannaRoot.SetActive(false);
            SetFlash(0f);
        }

        Transform[] Children(string group)
        {
            var parent = savannaRoot.transform.Find(group);
            var children = new Transform[parent != null ? parent.childCount : 0];
            for (int i = 0; i < children.Length; i++) children[i] = parent.GetChild(i);
            return children;
        }

        void HandleArrived(Vector3 groundPoint)
        {
            if (config.savannaStartsWhenSheLands) Begin(groundPoint);
        }

        public void Begin(Vector3 groundPoint)
        {
            if (IsActive) return;
            IsActive = true;
            StartCoroutine(BeginSequence(groundPoint));
        }

        IEnumerator BeginSequence(Vector3 groundPoint)
        {
            float half = config.savannaFlashSeconds * 0.5f;
            yield return Fade(0f, 1f, half);

            _home = savannaRoot.transform.parent;
            savannaRoot.transform.SetParent(null, true);
            var forward = Vector3.ProjectOnPlane(-tracker.Anchor.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            savannaRoot.transform.SetPositionAndRotation(groundPoint, Quaternion.LookRotation(forward, Vector3.up));
            PlaceOnGround(groundPoint);
            foreach (var terrain in _terrains) ClearGrass(terrain, groundPoint, viewCamera.transform.position);
            savannaRoot.SetActive(true);

            _arLighting = LightingPreset.Capture(sun);
            _tookOver = true;
            _arClearFlags = viewCamera.clearFlags;
            _arBackground = viewCamera.backgroundColor;
            _arBackgroundWasOn = cameraBackground != null && cameraBackground.enabled;
            if (cameraBackground != null) cameraBackground.enabled = false;
            viewCamera.clearFlags = CameraClearFlags.Skybox;
            dayLighting.Apply(sun);
            experience.GoTo(ExperienceState.Savanna);

            for (float t = 0f; t < half * 2f; t += Time.deltaTime)
            {
                float k = t / (half * 2f);
                SetFlash(1f - k);
                yield return null;
            }
            SetFlash(0f);
        }

        void PlaceOnGround(Vector3 groundPoint)
        {
            foreach (var terrain in _terrains)
            {
                var size = terrain.terrainData.size;
                float centre = terrain.terrainData.GetInterpolatedHeight(0.5f, 0.5f);
                terrain.transform.SetPositionAndRotation(groundPoint + new Vector3(-size.x * 0.5f, -centre, -size.z * 0.5f), Quaternion.identity);
            }
            for (int i = 0; i < _seated.Length; i++)
            {
                var p = _seated[i].position;
                p.y = GroundAt(p) + _seatOffsets[i];
                _seated[i].position = p;
            }
        }

        void ClearGrass(Terrain terrain, Vector3 her, Vector3 viewer)
        {
            var data = terrain.terrainData;
            int res = data.detailResolution;
            var origin = terrain.transform.position;
            var a = new Vector2(her.x, her.z);
            var b = new Vector2(viewer.x, viewer.z);
            float radius = config.savannaClearRadiusAroundHer, halfWidth = config.savannaClearPathHalfWidth;
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                var map = data.GetDetailLayer(0, 0, res, res, layer);
                for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    var cell = new Vector2(origin.x + (x + 0.5f) * data.size.x / res, origin.z + (z + 0.5f) * data.size.z / res);
                    if (Vector2.Distance(cell, a) < radius || DistanceToSegment(cell, a, b) < halfWidth) map[z, x] = 0;
                }
                data.SetDetailLayer(0, 0, layer, map);
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        public float GroundAt(Vector3 point)
        {
            if (_terrains.Length == 0) return savannaRoot.transform.position.y;
            var t = _terrains[0];
            return t.SampleHeight(point) + t.transform.position.y;
        }

        public void End()
        {
            if (!IsActive) return;
            StopAllCoroutines();
            Restore();
            SetFlash(0f);
        }

        void Restore(bool putAway = true)
        {
            if (!IsActive) return;
            IsActive = false;
            savannaRoot.SetActive(false);
            if (putAway && _home != null) savannaRoot.transform.SetParent(_home, false);
            if (!_tookOver) return;
            _tookOver = false;
            _arLighting.Apply(sun);
            if (viewCamera == null) return;
            viewCamera.clearFlags = _arClearFlags;
            viewCamera.backgroundColor = _arBackground;
            if (cameraBackground != null) cameraBackground.enabled = _arBackgroundWasOn;
        }

        void OnDestroy()
        {
            Restore(putAway: false);
            if (savannaRoot != null && savannaRoot.transform.parent == null) Destroy(savannaRoot);
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetFlash(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetFlash(to);
        }

        void SetFlash(float alpha)
        {
            if (flash == null) return;
            var c = flashColor;
            c.a *= alpha;
            flash.color = c;
            flash.enabled = alpha > 0.001f;
        }
    }
}

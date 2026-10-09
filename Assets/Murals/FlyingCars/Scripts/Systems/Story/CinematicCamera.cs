using UnityEngine;

namespace MuralAR
{
    public sealed class CinematicCamera : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => arCamera = refs.Camera;

        const float SubjectFollowSharpness = 2f;
        const float MinHeightAboveGround = 0.25f;

        [SerializeField] MuralConfig config;
        [SerializeField] StoryPlayer story;
        [SerializeField] WomanController woman;
        [SerializeField] SavannaController savanna;
        [SerializeField] CarsDirector cars;
        [SerializeField, Tooltip("The phone's AR camera (switched off while this one films).")]
        Camera arCamera;
        [SerializeField, Tooltip("The story camera (starts inactive).")]
        Camera cinematic;

        public bool IsFilming { get; private set; }

        public int ShotIndex { get; private set; } = -1;

        AudioListener _arListener;
        AudioListener _cinematicListener;
        int _shot = -1;
        float _shotStartedAt;
        Pose _blendFrom;
        float _blendFromFov;
        Vector3 _herSmoothed;
        bool _returning;
        float _returnStartedAt;

        void Awake()
        {
            _arListener = arCamera.GetComponent<AudioListener>();
            _cinematicListener = cinematic.GetComponent<AudioListener>();
            cinematic.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            story.OnStoryStarted += HandleStarted;
            story.OnStoryFinished += HandleFinished;
        }

        void OnDisable()
        {
            story.OnStoryStarted -= HandleStarted;
            story.OnStoryFinished -= HandleFinished;
        }

        void HandleStarted()
        {
            if (!config.cinematicCamera || config.shots.Length == 0 || woman.Woman == null) return;
            cinematic.transform.SetPositionAndRotation(arCamera.transform.position, arCamera.transform.rotation);
            cinematic.fieldOfView = arCamera.fieldOfView;
            cinematic.nearClipPlane = arCamera.nearClipPlane;
            cinematic.farClipPlane = arCamera.farClipPlane;
            cinematic.gameObject.SetActive(true);
            arCamera.enabled = false;
            if (_arListener != null) _arListener.enabled = false;
            if (_cinematicListener != null) _cinematicListener.enabled = true;
            _herSmoothed = woman.Woman.position;
            _shot = -1;
            _returning = false;
            IsFilming = true;
        }

        void HandleFinished()
        {
            if (!IsFilming) return;
            _returning = true;
            _returnStartedAt = Time.time;
            _blendFrom = new Pose(cinematic.transform.position, cinematic.transform.rotation);
            _blendFromFov = cinematic.fieldOfView;
        }

        void OnDestroy()
        {
            if (IsFilming) HandBack();
        }

        void HandBack()
        {
            IsFilming = false;
            ShotIndex = -1;
            if (arCamera != null) arCamera.enabled = true;
            if (_arListener != null) _arListener.enabled = true;
            if (_cinematicListener != null) _cinematicListener.enabled = false;
            if (cinematic != null) cinematic.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!IsFilming) return;
            float dt = Time.deltaTime;
            _herSmoothed = Vector3.Lerp(_herSmoothed, woman.Woman.position, 1f - Mathf.Exp(-SubjectFollowSharpness * dt));

            if (_returning)
            {
                float r = Mathf.SmoothStep(0f, 1f, (Time.time - _returnStartedAt) / Mathf.Max(0.01f, config.cinematicReturnSeconds));
                Apply(arCamera.transform.position, arCamera.transform.rotation, arCamera.fieldOfView, r);
                if (r >= 1f) HandBack();
                return;
            }

            var shots = config.shots;
            int index = 0;
            for (int i = 1; i < shots.Length; i++)
                if (story.Elapsed >= shots[i].startSeconds) index = i;
            if (index != _shot)
            {
                _blendFrom = new Pose(cinematic.transform.position, cinematic.transform.rotation);
                _blendFromFov = cinematic.fieldOfView;
                _shot = index;
                ShotIndex = index;
                _shotStartedAt = story.Elapsed;
            }

            var shot = shots[_shot];
            float end = _shot + 1 < shots.Length ? shots[_shot + 1].startSeconds : story.Duration;
            float k = Mathf.SmoothStep(0f, 1f, (story.Elapsed - shot.startSeconds) / Mathf.Max(0.01f, end - shot.startSeconds));
            Evaluate(shot, k, out var position, out var look, out float fov);
            var rotation = Quaternion.LookRotation(look - position, Vector3.up);
            float blend = shot.blendSeconds > 0f ? Mathf.SmoothStep(0f, 1f, (story.Elapsed - _shotStartedAt) / shot.blendSeconds) : 1f;
            Apply(position, rotation, fov, blend);
        }

        void Apply(Vector3 position, Quaternion rotation, float fov, float t)
        {
            var p = Vector3.Lerp(_blendFrom.position, position, t);
            p.y = Mathf.Max(p.y, savanna.GroundAt(p) + MinHeightAboveGround);
            cinematic.transform.SetPositionAndRotation(p, Quaternion.Slerp(_blendFrom.rotation, rotation, t));
            cinematic.fieldOfView = Mathf.Lerp(_blendFromFov, fov, t);
        }

        void Evaluate(in CinematicShot shot, float k, out Vector3 position, out Vector3 look, out float fov)
        {
            var root = savanna.Root;
            var subject = shot.subject switch
            {
                ShotSubject.Tree when savanna.TreeCount > 0 => savanna.Tree(shot.treeIndex).position,
                ShotSubject.Cars when cars.Cars.Count > 0 => cars.SwirlCentre,
                _ => _herSmoothed,
            };
            if (shot.subject == ShotSubject.Tree) subject.y = savanna.GroundAt(subject);

            float yaw = Mathf.Lerp(shot.from.x, shot.to.x, k);
            float distance = Mathf.Lerp(shot.from.y, shot.to.y, k);
            float height = Mathf.Lerp(shot.from.z, shot.to.z, k);
            var around = Quaternion.AngleAxis(yaw, Vector3.up) * root.forward;
            position = subject + around * distance + Vector3.up * height;

            var lookOffset = Vector3.Lerp(shot.lookFrom, shot.lookTo, k);
            look = subject + root.right * lookOffset.x + Vector3.up * lookOffset.y + root.forward * lookOffset.z;
            if ((look - position).sqrMagnitude < 1e-4f) look = position + root.forward * -1f;
            fov = Mathf.Lerp(shot.fovFrom, shot.fovTo, k);
        }
    }
}

using UnityEngine;

namespace MuralAR
{
    public sealed class WomanWander : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => audience = new[] { audience.Length > 0 ? audience[0] : null, refs.Camera };

        [SerializeField] MuralConfig config;
        [SerializeField] StoryPlayer story;
        [SerializeField] WomanController woman;
        [SerializeField] SavannaController savanna;
        [SerializeField, Tooltip("She turns to face whichever of these cameras is rendering when she pauses.")]
        Camera[] audience = System.Array.Empty<Camera>();

        enum Step { Waiting, Turning, Walking, Pausing, Home, Done }

        Step _step;
        int _next;
        float _pauseUntil;
        bool _active;
        Vector3 _landing;

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
            _active = woman.Woman != null && config.womanWanderPoints.Length > 0;
            if (woman.Woman != null) _landing = woman.Woman.position;
            _step = Step.Waiting;
            _next = 0;
        }

        void HandleFinished()
        {
            if (!_active || woman.Woman == null) return;
            _step = Step.Home;
            woman.Motion.Walking = true;
        }

        void Update()
        {
            if (!_active || woman.Woman == null) return;
            var her = woman.Woman;
            float dt = Time.deltaTime;

            switch (_step)
            {
                case Step.Waiting:
                    FaceAudience(her, dt);
                    if (story.Elapsed >= config.womanWanderStartSeconds) _step = Step.Turning;
                    break;

                case Step.Turning:
                {
                    var to = Flat(Target() - her.position);
                    if (TurnTowards(her, to, dt) < 5f)
                    {
                        _step = Step.Walking;
                        woman.Motion.Walking = true;
                    }
                    break;
                }

                case Step.Walking:
                {
                    var target = Target();
                    var to = Flat(target - her.position);
                    float step = config.womanWalkSpeed * dt;
                    if (to.magnitude <= step)
                    {
                        Place(her, target);
                        woman.Motion.Walking = false;
                        _pauseUntil = story.Elapsed + Random.Range(config.womanPauseSeconds.x, config.womanPauseSeconds.y);
                        _next = (_next + 1) % config.womanWanderPoints.Length;
                        _step = Step.Pausing;
                    }
                    else
                    {
                        TurnTowards(her, to, dt);
                        Place(her, her.position + to.normalized * step);
                    }
                    break;
                }

                case Step.Home:
                {
                    var to = Flat(_landing - her.position);
                    float step = config.womanWalkSpeed * 1.5f * dt;
                    if (to.magnitude <= step)
                    {
                        her.position = _landing;
                        woman.Motion.Walking = false;
                        _step = Step.Done;
                    }
                    else
                    {
                        TurnTowards(her, to, dt);
                        var p = her.position + to.normalized * step;
                        p.y = Mathf.MoveTowards(her.position.y, _landing.y, step);
                        her.position = p;
                    }
                    break;
                }

                case Step.Done:
                    FaceAudience(her, dt);
                    break;

                case Step.Pausing:
                    FaceAudience(her, dt);
                    if (story.Elapsed >= _pauseUntil) _step = Step.Turning;
                    break;
            }
        }

        Vector3 Target()
        {
            var p = config.womanWanderPoints[_next];
            var root = savanna.Root;
            return root.position + root.right * p.x + root.forward * p.y;
        }

        void Place(Transform her, Vector3 position)
        {
            position.y = savanna.GroundAt(position);
            her.position = position;
        }

        void FaceAudience(Transform her, float dt)
        {
            foreach (var cam in audience)
            {
                if (cam == null || !cam.isActiveAndEnabled) continue;
                TurnTowards(her, Flat(cam.transform.position - her.position), dt);
                return;
            }
        }

        float TurnTowards(Transform her, Vector3 direction, float dt)
        {
            if (direction.sqrMagnitude < 1e-4f) return 0f;
            var target = Quaternion.LookRotation(direction, Vector3.up);
            her.rotation = Quaternion.RotateTowards(her.rotation, target, config.womanTurnDegreesPerSecond * dt);
            return Quaternion.Angle(her.rotation, target);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

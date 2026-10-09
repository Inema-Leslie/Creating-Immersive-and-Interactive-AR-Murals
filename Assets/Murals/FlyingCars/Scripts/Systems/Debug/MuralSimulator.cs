using UnityEngine;
using UnityEngine.InputSystem;

namespace MuralAR
{
    public sealed class MuralSimulator : MonoBehaviour
    {
        [SerializeField] MuralConfig config;
        [SerializeField, Tooltip("The FlyingCarsMural prefab (the same one FlyingCarsData points to).")]
        GameObject muralPrefab;
        [SerializeField] Camera viewCamera;
        [SerializeField, Tooltip("Unlit material showing the full wall photo.")]
        Material wallMaterial;
        [SerializeField, Tooltip("Where the tracking image sits inside the wall photo, normalised, origin top-left.")]
        Rect trackedRegionInPhoto = new(0f, 0f, 1f, 1f);
        [SerializeField, Tooltip("AR components (and the MuralSpawner) to switch off while simulating.")]
        Behaviour[] disableWhileSimulating = System.Array.Empty<Behaviour>();
        [SerializeField, Tooltip("Seconds out of view before the image counts as lost (MuralSpawner.lostDelay).")]
        float lostDelay = 0.5f;
        [SerializeField] float eyeHeight = 1.6f;
        [SerializeField] float lookSensitivity = 0.15f;
        [SerializeField] float walkSpeed = 2.5f;
        [SerializeField] float scrollStep = 0.6f;
        [SerializeField] Color skyColor = new(0.56f, 0.74f, 0.93f);

        public bool ForceLost { get; set; }

        public bool IsActive { get; private set; }

        public GameObject Mural { get; private set; }
        public IMuralHost Host { get; private set; }

        public Pose ImagePose { get; private set; }
        public Vector2 MuralSize { get; private set; }

        GameObject _wall;
        ExperienceController _experience;
        bool _tracked;
        float _lastSeen;
        float _yaw;
        float _pitch;

        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            foreach (var b in disableWhileSimulating)
            {
                if (b != null) b.enabled = false;
            }

            MuralSize = new Vector2(config.imageWidthMeters, config.imageWidthMeters * config.imageHeightToWidth);
            var center = new Vector3(0f, MuralSize.y * 0.5f, 0f);

            ImagePose = new Pose(center, Quaternion.Euler(-90f, 0f, 0f));
            BuildWall(center);

            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = skyColor;
            viewCamera.transform.SetParent(null, true);
            viewCamera.transform.position = new Vector3(0f, eyeHeight, -MuralSize.y * 1.15f);
            var look = Quaternion.LookRotation(center - viewCamera.transform.position).eulerAngles;
            _yaw = look.y;
            _pitch = look.x > 180f ? look.x - 360f : look.x;
            ApplyLook();

            var image = new GameObject("Simulated Tracked Image (FlyingCars)").transform;
            image.SetPositionAndRotation(ImagePose.position, ImagePose.rotation);
            Mural = Instantiate(muralPrefab, image);
            Mural.transform.localPosition = Vector3.zero;
            Mural.transform.localRotation = Quaternion.identity;
            Host = Mural.GetComponent<IMuralHost>();
            _experience = Mural.GetComponentInChildren<ExperienceController>(true);
            if (_experience != null) _experience.OnStateChanged += HideWallInSavanna;
        }

        void HideWallInSavanna(ExperienceState state) => _wall.SetActive(state != ExperienceState.Savanna);

        void OnDestroy()
        {
            if (_experience != null) _experience.OnStateChanged -= HideWallInSavanna;
        }

        void Update()
        {
            if (!IsActive) return;
            HandleInput();
            if (Host == null) return;

            bool visible = !ForceLost && InView(ImagePose.position);
            if (visible)
            {
                _lastSeen = Time.time;
                if (!_tracked)
                {
                    _tracked = true;
                    Host.SimulateFound();
                }
            }
            else if (_tracked && Time.time - _lastSeen > lostDelay)
            {
                _tracked = false;
                Host.SimulateLost();
            }
        }

        bool InView(Vector3 point)
        {
            var vp = viewCamera.WorldToViewportPoint(point);
            return vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        void HandleInput()
        {
            var cam = viewCamera.transform;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue();
                    _yaw += delta.x * lookSensitivity;
                    _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -80f, 80f);
                    ApplyLook();
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) cam.position += cam.forward * (Mathf.Sign(scroll) * scrollStep);
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.lKey.wasPressedThisFrame) ForceLost = !ForceLost;

            var move = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            if (move == Vector2.zero) return;

            var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            cam.position += (forward * move.y + right * move.x) * (walkSpeed * Time.deltaTime);
        }

        void ApplyLook() => viewCamera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        void BuildWall(Vector3 muralCenter)
        {
            var r = trackedRegionInPhoto;
            float photoWidth = MuralSize.x / r.width;
            float photoHeight = MuralSize.y / r.height;
            float dx = (0.5f - (r.x + r.width * 0.5f)) * photoWidth;
            float dy = ((r.y + r.height * 0.5f) - 0.5f) * photoHeight;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _wall = wall;
            wall.name = "Simulated Wall Photo";
            Destroy(wall.GetComponent<Collider>());
            wall.transform.SetParent(transform, false);
            wall.transform.position = muralCenter + new Vector3(dx, dy, 0.01f);
            wall.transform.localScale = new Vector3(photoWidth, photoHeight, 1f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;
        }
    }
}

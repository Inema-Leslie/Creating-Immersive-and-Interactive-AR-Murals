using UnityEngine;

namespace MuralAR
{
    public sealed class CarInteraction : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => viewCamera = refs.Camera;

        public const string InfoIconName = "Info Icon";

        [SerializeField] TouchInput touch;
        [SerializeField] CarInfoPanel infoPanel;
        [SerializeField] Camera viewCamera;
        [SerializeField] MuralTracker tracker;

        void OnEnable()
        {
            touch.OnTap += HandleTap;
            touch.OnHoldStart += HandleHoldStart;
            touch.OnHoldEnd += HandleHoldEnd;
            touch.OnSwipe += HandleSwipe;
            touch.OnDoubleTap += HandleDoubleTap;
        }

        void OnDisable()
        {
            touch.OnTap -= HandleTap;
            touch.OnHoldStart -= HandleHoldStart;
            touch.OnHoldEnd -= HandleHoldEnd;
            touch.OnSwipe -= HandleSwipe;
            touch.OnDoubleTap -= HandleDoubleTap;
        }

        static CarController CarOf(GameObject target) => target != null ? target.GetComponentInParent<CarController>() : null;

        void HandleTap(GameObject target)
        {
            var car = CarOf(target);
            if (car == null) return;

            if (target.name == InfoIconName)
            {
                car.Inspect();
                infoPanel.Show(car.Settings, car.EndInspect);
                return;
            }
            if (car.State == CarState.Dormant) car.Wake();
            else car.Blip();
        }

        void HandleHoldStart(GameObject target)
        {
            var car = CarOf(target);
            if (car != null) car.StartRev();
        }

        void HandleHoldEnd(GameObject target)
        {
            var car = CarOf(target);
            if (car != null) car.StopRev();
        }

        void HandleDoubleTap(GameObject target)
        {
            var car = CarOf(target);
            if (car != null) car.FlyBy();
        }

        void HandleSwipe(GameObject target, Vector2 screenDelta)
        {
            var car = CarOf(target);
            if (car == null) return;
            var cam = viewCamera.transform;
            var world = cam.right * screenDelta.x + cam.up * screenDelta.y + cam.forward * (screenDelta.magnitude * 0.3f);
            car.Launch(tracker.Anchor.InverseTransformDirection(world.normalized));
        }
    }
}

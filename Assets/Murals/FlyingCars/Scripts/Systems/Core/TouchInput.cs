using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MuralAR
{
    public sealed class TouchInput : MonoBehaviour, ISceneBound
    {
        public void Bind(SceneRefs refs) => viewCamera = refs.Camera;

        [SerializeField] MuralConfig config;
        [SerializeField] Camera viewCamera;
        [SerializeField] LayerMask interactionLayers;
        [SerializeField] float maxDistance = 100f;

        public event Action<GameObject> OnTap;
        public event Action<GameObject> OnHoldStart;
        public event Action<GameObject> OnHoldEnd;
        public event Action<GameObject, Vector2> OnSwipe;
        public event Action<GameObject> OnDoubleTap;

        GameObject _target;
        Vector2 _pressPosition;
        float _pressTime;
        bool _pressing;
        bool _holding;
        bool _moved;
        GameObject _lastTapTarget;
        float _lastTapTime = -10f;

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            var position = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                if (IsOverUI()) return;
                _pressing = true;
                _holding = false;
                _moved = false;
                _pressPosition = position;
                _pressTime = Time.unscaledTime;
                _target = Raycast(position);
                return;
            }
            if (!_pressing) return;

            float threshold = config.swipeThresholdPixels;
            if (!_moved && (position - _pressPosition).sqrMagnitude > threshold * threshold) _moved = true;

            if (pointer.press.isPressed)
            {
                if (!_holding && !_moved && _target != null && Time.unscaledTime - _pressTime >= config.holdStartSeconds)
                {
                    _holding = true;
                    OnHoldStart?.Invoke(_target);
                }
                return;
            }

            _pressing = false;
            if (_holding)
            {
                OnHoldEnd?.Invoke(_target);
                return;
            }
            if (_moved)
            {
                if (_target != null) OnSwipe?.Invoke(_target, position - _pressPosition);
                return;
            }
            if (_target != null) Tap(_target);
        }

        void Tap(GameObject target)
        {
            OnTap?.Invoke(target);
            if (target == _lastTapTarget && Time.unscaledTime - _lastTapTime <= config.doubleTapWindowSeconds)
            {
                OnDoubleTap?.Invoke(target);
                _lastTapTarget = null;
                return;
            }
            _lastTapTarget = target;
            _lastTapTime = Time.unscaledTime;
        }

        GameObject Raycast(Vector2 screenPosition)
        {
            Physics.SyncTransforms();
            var ray = viewCamera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out var hit, maxDistance, interactionLayers, QueryTriggerInteraction.Collide)
                ? hit.collider.gameObject
                : null;
        }

        static bool IsOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public void SimulateTap(GameObject target) => Tap(target);
        public void SimulateHold(GameObject target, bool start)
        {
            if (start) OnHoldStart?.Invoke(target);
            else OnHoldEnd?.Invoke(target);
        }
        public void SimulateSwipe(GameObject target, Vector2 delta) => OnSwipe?.Invoke(target, delta);
    }
}

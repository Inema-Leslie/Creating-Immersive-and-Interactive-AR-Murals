using UnityEngine;

namespace MuralAR
{
    public sealed class WomanIdleMotion : MonoBehaviour
    {
        MuralConfig _config;
        Transform _body;
        float _feetY;
        float _height;
        float _phase;

        public bool Walking { get; set; }

        public void Init(MuralConfig config, float feetY, float height)
        {
            _config = config;
            _feetY = feetY;
            _height = height;
            _phase = Random.value * 10f;

            var parts = new Transform[transform.childCount];
            for (int i = 0; i < parts.Length; i++) parts[i] = transform.GetChild(i);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            _body.localPosition = new Vector3(0f, feetY, 0f);
            foreach (var part in parts) part.SetParent(_body, true);
        }

        void Update()
        {
            if (_body == null) return;
            float t = Time.time + _phase;
            float breath = Mathf.Sin(t * 2f * Mathf.PI / _config.womanBreathSeconds);
            float sway = Mathf.Sin(t * 2f * Mathf.PI / _config.womanSwaySeconds);

            float bob = 0f, roll = 0f;
            if (Walking)
            {
                float step = Mathf.Sin(t * _config.womanStepsPerSecond * Mathf.PI);
                bob = Mathf.Abs(step) * _config.womanStepBob * _height;
                roll = step * _config.womanStepRollDegrees;
            }

            _body.localPosition = new Vector3(0f, _feetY + bob, 0f);
            _body.localScale = new Vector3(1f, 1f + breath * _config.womanBreathAmount, 1f);
            _body.localRotation = Quaternion.Euler(0f, sway * _config.womanSwayDegrees * 0.6f, sway * _config.womanSwayDegrees + roll);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuralAR
{
    public sealed class CarPathFollower : MonoBehaviour
    {
        public event Action OnPathComplete;

        public bool IsMoving { get; private set; }

        public bool IsLooping => _loop;

        public float Speed => _speed;

        public int PathId { get; private set; }

        public float RemainingDistance
        {
            get
            {
                if (!IsMoving || _loop) return 0f;
                float d = Vector3.Distance(transform.localPosition, Point(_segment + 1));
                for (int i = _segment + 1; i < _points.Count - 1; i++) d += Vector3.Distance(_points[i], _points[i + 1]);
                return d;
            }
        }

        public Func<Vector3, Vector3> Constrain { get; set; }

        readonly List<Vector3> _points = new();
        int _segment;
        float _u;
        float _speed;
        float _bankDegrees;
        bool _loop;
        float _roll;

        public void FlyPath(IReadOnlyList<Vector3> points, float speed, float bankDegrees, bool loop = false)
        {
            _points.Clear();
            _points.Add(transform.localPosition);
            foreach (var p in points) _points.Add(p);
            if (loop) _points.Add(points[0]);
            _segment = 0;
            _u = 0f;
            _speed = Mathf.Max(0.05f, speed);
            _bankDegrees = bankDegrees;
            _loop = loop;
            IsMoving = _points.Count > 1;
            PathId++;
        }

        public void Stop() => IsMoving = false;

        void Update()
        {
            if (!IsMoving) return;

            float segmentLength = Mathf.Max(0.01f, Vector3.Distance(Point(_segment), Point(_segment + 1)));
            _u += _speed * Time.deltaTime / segmentLength;
            while (_u >= 1f)
            {
                _u -= 1f;
                _segment++;
                if (_segment >= _points.Count - 1)
                {
                    if (_loop)
                    {
                        _segment = 1;
                    }
                    else
                    {
                        transform.localPosition = Safe(_points[_points.Count - 1]);
                        IsMoving = false;
                        OnPathComplete?.Invoke();
                        return;
                    }
                }
            }

            var position = Safe(Evaluate(_segment, _u));
            var ahead = Safe(Evaluate(_segment, Mathf.Min(_u + 0.05f, 1f)));
            var direction = ahead - position;
            if (direction.sqrMagnitude > 1e-6f)
            {
                var forward = direction.normalized;
                var previous = transform.localRotation * Vector3.forward;
                float turn = Vector3.SignedAngle(previous, forward, Vector3.up);
                _roll = Mathf.Lerp(_roll, Mathf.Clamp(-turn * 6f, -_bankDegrees, _bankDegrees), Time.deltaTime * 3f);
                var target = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, _roll);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * 4f);
            }
            transform.localPosition = position;
        }

        Vector3 Safe(Vector3 p) => Constrain != null ? Constrain(p) : p;

        Vector3 Point(int index)
        {
            if (_loop)
            {
                int n = _points.Count - 1;
                if (index > n) index = 1 + (index - 1) % (n - 1);
                return _points[Mathf.Clamp(index, 0, n)];
            }
            return _points[Mathf.Clamp(index, 0, _points.Count - 1)];
        }

        Vector3 Evaluate(int segment, float u)
        {
            var p0 = Point(segment - 1);
            var p1 = Point(segment);
            var p2 = Point(segment + 1);
            var p3 = Point(segment + 2);
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }
    }
}

using System.Collections;
using UnityEngine;

namespace MuralAR
{
    public sealed class DissolveController : MonoBehaviour
    {
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        Renderer[] _renderers;
        bool[] _dissolves;
        MaterialPropertyBlock _block;

        public float Amount { get; private set; }

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _dissolves = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var material = _renderers[i].sharedMaterial;
                _dissolves[i] = material != null && material.HasProperty(DissolveId);
            }
            _block = new MaterialPropertyBlock();
        }

        public void SetAmount(float amount)
        {
            if (_renderers == null) Awake();
            Amount = Mathf.Clamp01(amount);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (!_dissolves[i])
                {
                    r.enabled = Amount < 0.5f;
                    continue;
                }
                r.GetPropertyBlock(_block);
                _block.SetFloat(DissolveId, Amount);
                r.SetPropertyBlock(_block);
            }
        }

        public IEnumerator Animate(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetAmount(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetAmount(to);
        }
    }
}

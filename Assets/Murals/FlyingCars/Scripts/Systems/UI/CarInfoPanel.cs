using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuralAR
{
    public sealed class CarInfoPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] Button closeButton;

        Action _onClose;

        public bool IsOpen => panel.activeSelf;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            panel.SetActive(false);
        }

        public void Show(CarSettings car, Action onClose)
        {
            _onClose?.Invoke();
            _onClose = onClose;
            title.text = car.displayName;
            body.text = $"<b>Design</b>\n{car.design}\n\n<b>How it hovers</b>\n{car.howItHovers}\n\n<b>Its role</b>\n{car.role}";
            panel.SetActive(true);
        }

        public void Close()
        {
            panel.SetActive(false);
            var callback = _onClose;
            _onClose = null;
            callback?.Invoke();
        }
    }
}

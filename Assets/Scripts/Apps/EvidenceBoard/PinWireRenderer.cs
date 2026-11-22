using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.EvidenceBoard
{
    /// <summary>
    /// 증거판 위 두 단서 카드의 상단 핀(RectTransform) 사이를 잇는 UI 연결선(실) 렌더러.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class PinWireRenderer : MonoBehaviour
    {
        [SerializeField] private float wireThickness = 3.5f;

        private RectTransform _lineRect;
        private RectTransform _pinA;
        private RectTransform _pinB;

        private void Awake()
        {
            _lineRect = GetComponent<RectTransform>();
            var img = GetComponent<Image>();
            img.raycastTarget = false;
        }

        public void Bind(RectTransform pinA, RectTransform pinB)
        {
            _pinA = pinA;
            _pinB = pinB;
            UpdatePositions();
        }

        public void UpdatePositions()
        {
            if (_pinA == null || _pinB == null || _lineRect == null || _lineRect.parent == null) return;

            Transform parent = _lineRect.parent;
            Vector2 posA = parent.InverseTransformPoint(_pinA.position);
            Vector2 posB = parent.InverseTransformPoint(_pinB.position);

            Vector2 dir = posB - posA;
            float dist = dir.magnitude;

            _lineRect.anchoredPosition = posA + dir * 0.5f;
            _lineRect.sizeDelta = new Vector2(dist, wireThickness);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}

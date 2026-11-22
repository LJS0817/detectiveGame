using UnityEngine;
using UnityEngine.EventSystems;

namespace ColdCaseOS.OS
{
    /// <summary>
    /// OSWindow의 타이틀바에 부착되어 드래그 이동 입력만 부모 OSWindow로 전달합니다.
    /// 창 본체 내부의 텍스트 드래그/줌인 드래그와 입력이 충돌하지 않도록 분리합니다.
    /// </summary>
    public class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private OSWindow targetWindow;
        private Canvas _rootCanvas;

        private void Awake()
        {
            if (targetWindow == null)
                targetWindow = GetComponentInParent<OSWindow>();
            _rootCanvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            targetWindow?.OnPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (targetWindow == null) return;
            float scale = (_rootCanvas != null && _rootCanvas.scaleFactor > 0f) ? _rootCanvas.scaleFactor : 1f;
            targetWindow.MoveByDelta(eventData.delta / scale);
        }
    }
}

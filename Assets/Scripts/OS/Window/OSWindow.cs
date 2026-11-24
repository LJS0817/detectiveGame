using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColdCaseOS.OS
{
    public enum WindowDisplayState
    {
        Closed,
        Normal,
        Minimized,
        SnappedLeft,
        SnappedRight,
        Maximized
    }

    [Flags]
    public enum WindowResizeHandle
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Top = 1 << 2,
        Bottom = 1 << 3
    }

    /// <summary>
    /// 모든 K-POL OS 앱 창의 기반 컴포넌트.
    /// 드래그 이동, 모서리/테두리 드래그 리사이즈(LayoutElement 최소 크기 연동), 화면 경계 클램핑,
    /// 좌우 50% 분할 스냅, 최소화/복원, DOTween 전환 애니메이션 및 생명주기 콜백을 제공합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class OSWindow : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Window Identity")]
        [SerializeField] private string windowId;
        [SerializeField] private Image titleBarBackground;
        [SerializeField] private Color focusedTitleColor = new Color(0.173f, 0.173f, 0.173f, 1f);   // #2C2C2C
        [SerializeField] private Color unfocusedTitleColor = new Color(0.353f, 0.337f, 0.314f, 1f); // #5A5650
        [SerializeField] private float animDuration = 0.16f;

        [Header("Window Resize")]
        [SerializeField] private bool resizable = true;
        [SerializeField] private bool allowEdgeResize = false; // false: 4개 모서리 전용, true: 4개 모서리 + 4개 변
        [SerializeField] private float resizeHandleThickness = 18f;
        [SerializeField] private Vector2 fallbackMinSize = new Vector2(360f, 260f);

        public string WindowId => windowId;
        public WindowDisplayState State { get; private set; } = WindowDisplayState.Closed;
        protected bool IsResizing => _activeResizeHandle != WindowResizeHandle.None;

        public event Action<OSWindow> OnFocused;
        public event Action<OSWindow> OnMinimized;
        public event Action<OSWindow> OnClosed;

        protected RectTransform _rect;
        protected CanvasGroup _canvasGroup;
        protected WindowManager _manager;
        private LayoutElement _layoutElement;
        private Canvas _rootCanvas;
        private RectTransform _desktopBounds;

        private WindowResizeHandle _activeResizeHandle = WindowResizeHandle.None;
        private Vector2 _savedAnchoredPos;
        private Vector2 _savedSizeDelta;
        private bool _hasSavedNormalRect;

        protected virtual void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _layoutElement = GetComponent<LayoutElement>();
            _rootCanvas = GetComponentInParent<Canvas>();
        }

        protected virtual void OnWindowFocused() { }
        protected virtual void OnWindowMinimized() { }
        protected virtual void OnWindowClosed() { }

        public void BindManager(WindowManager manager, RectTransform desktopBounds)
        {
            _manager = manager;
            _desktopBounds = desktopBounds;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _manager?.BringToFront(this);
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            TryBeginResize(eventData);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (_activeResizeHandle != WindowResizeHandle.None)
            {
                ContinueResize(eventData);
            }
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            EndResize();
        }

        public bool TryBeginResize(PointerEventData eventData)
        {
            _activeResizeHandle = WindowResizeHandle.None;
            if (!resizable || _rect == null) return false;

            var handle = DetectResizeHandle(eventData);
            if (handle == WindowResizeHandle.None) return false;

            _manager?.BringToFront(this);
            if (State == WindowDisplayState.SnappedLeft || State == WindowDisplayState.SnappedRight || State == WindowDisplayState.Maximized)
            {
                State = WindowDisplayState.Normal;
            }

            _rect.DOKill();
            _activeResizeHandle = handle;
            return true;
        }

        public void ContinueResize(PointerEventData eventData)
        {
            if (_activeResizeHandle == WindowResizeHandle.None || _rect == null) return;

            if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>();
            float scale = (_rootCanvas != null && _rootCanvas.scaleFactor > 0f) ? _rootCanvas.scaleFactor : 1f;
            Vector2 delta = eventData.delta / scale;

            float minW = (_layoutElement != null && _layoutElement.minWidth > 0f) ? _layoutElement.minWidth : fallbackMinSize.x;
            float minH = (_layoutElement != null && _layoutElement.minHeight > 0f) ? _layoutElement.minHeight : fallbackMinSize.y;
            float maxW = (_desktopBounds != null && _desktopBounds.rect.width > minW) ? _desktopBounds.rect.width : 1920f;
            float maxH = (_desktopBounds != null && _desktopBounds.rect.height > minH) ? _desktopBounds.rect.height : 1016f;

            Vector2 size = _rect.sizeDelta;
            Vector2 pos = _rect.anchoredPosition;
            Vector2 pivot = _rect.pivot;

            if ((_activeResizeHandle & WindowResizeHandle.Right) != 0)
            {
                float nextW = Mathf.Clamp(size.x + delta.x, minW, maxW);
                float actualDw = nextW - size.x;
                size.x = nextW;
                pos.x += actualDw * pivot.x;
            }
            else if ((_activeResizeHandle & WindowResizeHandle.Left) != 0)
            {
                float nextW = Mathf.Clamp(size.x - delta.x, minW, maxW);
                float actualDw = nextW - size.x;
                size.x = nextW;
                pos.x -= actualDw * (1f - pivot.x);
            }

            if ((_activeResizeHandle & WindowResizeHandle.Top) != 0)
            {
                float nextH = Mathf.Clamp(size.y + delta.y, minH, maxH);
                float actualDh = nextH - size.y;
                size.y = nextH;
                pos.y += actualDh * pivot.y;
            }
            else if ((_activeResizeHandle & WindowResizeHandle.Bottom) != 0)
            {
                float nextH = Mathf.Clamp(size.y - delta.y, minH, maxH);
                float actualDh = nextH - size.y;
                size.y = nextH;
                pos.y -= actualDh * (1f - pivot.y);
            }

            _rect.sizeDelta = size;
            _rect.anchoredPosition = pos;
        }

        public void EndResize()
        {
            if (_activeResizeHandle == WindowResizeHandle.None) return;
            _activeResizeHandle = WindowResizeHandle.None;
            SaveNormalRectIfNeeded();
        }

        private WindowResizeHandle DetectResizeHandle(PointerEventData eventData)
        {
            Camera cam = (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _rootCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, cam, out Vector2 local))
                return WindowResizeHandle.None;

            Rect r = _rect.rect;
            bool nearLeft = local.x <= r.xMin + resizeHandleThickness;
            bool nearRight = local.x >= r.xMax - resizeHandleThickness;
            bool nearBottom = local.y <= r.yMin + resizeHandleThickness;
            bool nearTop = local.y >= r.yMax - resizeHandleThickness;

            WindowResizeHandle handle = WindowResizeHandle.None;
            if (nearLeft) handle |= WindowResizeHandle.Left;
            else if (nearRight) handle |= WindowResizeHandle.Right;

            if (nearTop) handle |= WindowResizeHandle.Top;
            else if (nearBottom) handle |= WindowResizeHandle.Bottom;

            if (!allowEdgeResize)
            {
                bool hasHorizontal = (handle & (WindowResizeHandle.Left | WindowResizeHandle.Right)) != 0;
                bool hasVertical = (handle & (WindowResizeHandle.Top | WindowResizeHandle.Bottom)) != 0;
                if (!hasHorizontal || !hasVertical) return WindowResizeHandle.None;
            }

            return handle;
        }

        public void SetFocusVisual(bool isFocused)
        {
            if (titleBarBackground != null)
                titleBarBackground.color = isFocused ? focusedTitleColor : unfocusedTitleColor;

            if (isFocused)
            {
                OnWindowFocused();
                OnFocused?.Invoke(this);
            }
        }

        public void Open()
        {
            if (State == WindowDisplayState.Normal || State == WindowDisplayState.SnappedLeft ||
                State == WindowDisplayState.SnappedRight || State == WindowDisplayState.Maximized)
            {
                _manager?.BringToFront(this);
                return;
            }

            gameObject.SetActive(true);
            State = WindowDisplayState.Normal;
            if (_hasSavedNormalRect)
            {
                _rect.anchoredPosition = _savedAnchoredPos;
                _rect.sizeDelta = _savedSizeDelta;
            }

            _canvasGroup.DOKill();
            _rect.DOKill();
            _canvasGroup.alpha = 0f;
            _rect.localScale = Vector3.one * 0.96f;

            _canvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuad);
            _rect.DOScale(1f, animDuration).SetEase(Ease.OutBack);

            _manager?.BringToFront(this);
        }

        public void Minimize()
        {
            if (State == WindowDisplayState.Closed || State == WindowDisplayState.Minimized) return;
            SaveNormalRectIfNeeded();
            State = WindowDisplayState.Minimized;

            _canvasGroup.DOKill();
            _canvasGroup.DOFade(0f, animDuration * 0.8f).OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnWindowMinimized();
                OnMinimized?.Invoke(this);
            });
        }

        public void Close()
        {
            if (State == WindowDisplayState.Closed) return;
            SaveNormalRectIfNeeded();
            State = WindowDisplayState.Closed;

            _canvasGroup.DOKill();
            _canvasGroup.DOFade(0f, animDuration * 0.8f).OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnWindowClosed();
                OnClosed?.Invoke(this);
            });
        }

        public void CloseImmediate()
        {
            _canvasGroup?.DOKill();
            _rect?.DOKill();
            State = WindowDisplayState.Closed;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 서류 ↔ CCTV 동시 비교를 위한 좌/우 50% 분할 스냅 토글.
        /// </summary>
        public void ToggleSnap(bool snapLeft)
        {
            var targetState = snapLeft ? WindowDisplayState.SnappedLeft : WindowDisplayState.SnappedRight;
            if (State == targetState)
            {
                RestoreNormalRect();
                return;
            }

            if (_desktopBounds == null) return;
            SaveNormalRectIfNeeded();
            State = targetState;

            Rect bounds = _desktopBounds.rect;
            float halfW = bounds.width * 0.5f;
            Vector2 targetSize = new Vector2(halfW, bounds.height);
            Vector2 targetPos = new Vector2(snapLeft ? -halfW * 0.5f : halfW * 0.5f, 0f);

            _rect.DOKill();
            _rect.DOSizeDelta(targetSize, animDuration).SetEase(Ease.OutCubic);
            _rect.DOAnchorPos(targetPos, animDuration).SetEase(Ease.OutCubic);
            _manager?.BringToFront(this);
        }

        public void RestoreNormalRect()
        {
            if (!_hasSavedNormalRect) return;
            State = WindowDisplayState.Normal;
            _rect.DOKill();
            _rect.DOSizeDelta(_savedSizeDelta, animDuration).SetEase(Ease.OutCubic);
            _rect.DOAnchorPos(_savedAnchoredPos, animDuration).SetEase(Ease.OutCubic);
        }

        public void MoveByDelta(Vector2 scaledDelta)
        {
            if (State == WindowDisplayState.SnappedLeft || State == WindowDisplayState.SnappedRight || State == WindowDisplayState.Maximized)
            {
                RestoreNormalRect();
            }

            Vector2 nextPos = _rect.anchoredPosition + scaledDelta;
            _rect.anchoredPosition = ClampToDesktopBounds(nextPos);
        }

        private void SaveNormalRectIfNeeded()
        {
            if (State == WindowDisplayState.Normal || !_hasSavedNormalRect)
            {
                _savedAnchoredPos = _rect.anchoredPosition;
                _savedSizeDelta = _rect.sizeDelta;
                _hasSavedNormalRect = true;
            }
        }

        private Vector2 ClampToDesktopBounds(Vector2 pos)
        {
            if (_desktopBounds == null) return pos;
            Rect bounds = _desktopBounds.rect;
            float marginX = bounds.width * 0.45f;
            float marginY = bounds.height * 0.45f;
            return new Vector2(
                Mathf.Clamp(pos.x, -marginX, marginX),
                Mathf.Clamp(pos.y, -marginY, marginY));
        }
    }
}

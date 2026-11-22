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

    /// <summary>
    /// 모든 K-POL OS 앱 창의 기반 컴포넌트.
    /// 드래그 이동, 화면 경계 클램핑, 좌우 50% 분할 스냅, 최소화/복원, DOTween 전환 애니메이션 및 생명주기 콜백을 제공합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class OSWindow : MonoBehaviour, IPointerDownHandler
    {
        [Header("Window Identity")]
        [SerializeField] private string windowId;
        [SerializeField] private Image titleBarBackground;
        [SerializeField] private Color focusedTitleColor = new Color(0.16f, 0.24f, 0.34f, 1f);
        [SerializeField] private Color unfocusedTitleColor = new Color(0.10f, 0.13f, 0.17f, 1f);
        [SerializeField] private float animDuration = 0.16f;

        public string WindowId => windowId;
        public WindowDisplayState State { get; private set; } = WindowDisplayState.Closed;

        public event Action<OSWindow> OnFocused;
        public event Action<OSWindow> OnMinimized;
        public event Action<OSWindow> OnClosed;

        protected RectTransform _rect;
        protected CanvasGroup _canvasGroup;
        protected WindowManager _manager;
        private RectTransform _desktopBounds;

        private Vector2 _savedAnchoredPos;
        private Vector2 _savedSizeDelta;
        private bool _hasSavedNormalRect;

        protected virtual void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
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

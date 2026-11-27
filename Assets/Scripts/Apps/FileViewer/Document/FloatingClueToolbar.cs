using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.FileViewer
{
    /// <summary>
    /// 문서 텍스트 드래그 완료 시 마우스 커서 근처에 뜨는 3버튼 플로팅 툴바.
    /// [+ 증거판 등록] / [K-POL DB 조회] / [현장반 지시] + [취소].
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class FloatingClueToolbar : MonoBehaviour
    {
        [SerializeField] private Button addToBoardButton;
        [SerializeField] private Button searchDbButton;
        [SerializeField] private Button orderFieldButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Vector2 screenOffset = new Vector2(0f, 40f);

        private RectTransform _rect;
        private CanvasGroup _group;
        private Action _onConfirmAdd;
        private Action _onSearchDb;
        private Action _onOrderField;
        private Action _onCancel;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _group = GetComponent<CanvasGroup>();

            if (addToBoardButton != null)
                addToBoardButton.onClick.AddListener(HandleConfirm);
            if (searchDbButton != null)
                searchDbButton.onClick.AddListener(HandleSearchDb);
            if (orderFieldButton != null)
                orderFieldButton.onClick.AddListener(HandleOrderField);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(HandleCancel);

            HideImmediate();
        }

        public void ShowAtScreenPosition(
            Vector2 screenPos,
            Canvas rootCanvas,
            Action onConfirmAdd,
            Action onCancel = null,
            Action onSearchDb = null,
            Action onOrderField = null)
        {
            _onConfirmAdd = onConfirmAdd;
            _onSearchDb = onSearchDb;
            _onOrderField = onOrderField;
            _onCancel = onCancel;

            if (searchDbButton != null) searchDbButton.gameObject.SetActive(onSearchDb != null);
            if (orderFieldButton != null) orderFieldButton.gameObject.SetActive(onOrderField != null);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (rootCanvas != null && _rect.parent is RectTransform parentRect)
            {
                Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos + screenOffset, cam, out var localPoint))
                {
                    _rect.anchoredPosition = localPoint;
                }
            }

            _group.DOKill();
            _rect.DOKill();
            _group.alpha = 0f;
            _rect.localScale = Vector3.one * 0.9f;
            _group.DOFade(1f, 0.12f).SetEase(Ease.OutQuad);
            _rect.DOScale(1f, 0.12f).SetEase(Ease.OutBack);
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            _group.DOKill();
            _group.DOFade(0f, 0.1f).OnComplete(() => gameObject.SetActive(false));
        }

        public void HideImmediate()
        {
            _group?.DOKill();
            if (_group != null) _group.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void HandleConfirm()
        {
            var cb = _onConfirmAdd;
            Hide();
            cb?.Invoke();
        }

        private void HandleSearchDb()
        {
            var cb = _onSearchDb;
            Hide();
            cb?.Invoke();
        }

        private void HandleOrderField()
        {
            var cb = _onOrderField;
            Hide();
            cb?.Invoke();
        }

        private void HandleCancel()
        {
            var cb = _onCancel;
            Hide();
            cb?.Invoke();
        }
    }
}

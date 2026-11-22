using System.IO;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.EvidenceBoard
{
    /// <summary>
    /// 증거판 위 개별 사용자 단서 카드 UI.
    /// 자유 드래그 배치, 하단 TMP_InputField 사용자 메모, [기각(취소선 후 접기)], [폐기함 이동], 핀 선 연결을 지원합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ClueCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI Elements")]
        [SerializeField] private RectTransform pinAnchor;
        [SerializeField] private Button pinConnectButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private GameObject contentBodyRoot;
        [SerializeField] private TMP_Text excerptText;
        [SerializeField] private RawImage capturePreviewImage;
        [SerializeField] private TMP_InputField memoInputField;
        [SerializeField] private Button dismissToggleButton;
        [SerializeField] private Button moveToTrashButton;

        [Header("Collapse Animation")]
        [SerializeField] private float expandedHeight = 220f;
        [SerializeField] private float collapsedHeight = 44f;

        public UserClueCard Data { get; private set; }
        public RectTransform PinAnchor => pinAnchor != null ? pinAnchor : _rect;

        private RectTransform _rect;
        private Canvas _rootCanvas;
        private EvidenceBoardController _board;
        private Texture2D _previewTex;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _rootCanvas = GetComponentInParent<Canvas>();

            if (pinConnectButton != null)
                pinConnectButton.onClick.AddListener(() => _board?.OnPinClicked(Data?.cardId));
            if (dismissToggleButton != null)
                dismissToggleButton.onClick.AddListener(() => AppRoot.Instance?.RuntimeState.ToggleDismissCard(Data?.cardId));
            if (moveToTrashButton != null)
                moveToTrashButton.onClick.AddListener(() => AppRoot.Instance?.RuntimeState.SetTrashState(Data?.cardId, true));
            if (memoInputField != null)
                memoInputField.onEndEdit.AddListener(HandleMemoChanged);
        }

        private void OnDestroy()
        {
            if (_previewTex != null) Destroy(_previewTex);
        }

        public void Initialize(UserClueCard data, EvidenceBoardController board)
        {
            Data = data;
            _board = board;
            _rect.anchoredPosition = data.boardPosition.ToVector2();

            if (memoInputField != null)
                memoInputField.SetTextWithoutNotify(data.userMemo ?? string.Empty);

            LoadCapturePreviewIfNeeded();
            RefreshVisual(immediate: true);
        }

        public void RefreshVisual(bool immediate = false)
        {
            if (Data == null) return;
            gameObject.SetActive(!Data.isInTrash);

            if (titleText != null)
            {
                titleText.text = Data.isDismissed ? $"<s>{Data.displayTitle}</s>" : Data.displayTitle;
                titleText.alpha = Data.isDismissed ? 0.55f : 1f;
            }

            if (excerptText != null)
            {
                bool hasText = !string.IsNullOrEmpty(Data.extractedText);
                excerptText.gameObject.SetActive(hasText);
                excerptText.text = Data.extractedText;
            }

            if (contentBodyRoot != null)
                contentBodyRoot.SetActive(!Data.isDismissed);

            float targetH = Data.isDismissed ? collapsedHeight : expandedHeight;
            Vector2 targetSize = new Vector2(_rect.sizeDelta.x, targetH);

            _rect.DOKill();
            if (immediate)
                _rect.sizeDelta = targetSize;
            else
                _rect.DOSizeDelta(targetSize, 0.16f).SetEase(Ease.OutCubic).OnUpdate(() => _board?.NotifyCardMoved());
        }

        private void LoadCapturePreviewIfNeeded()
        {
            if (capturePreviewImage == null) return;
            if (string.IsNullOrEmpty(Data.capturedImagePath) || !File.Exists(Data.capturedImagePath))
            {
                capturePreviewImage.gameObject.SetActive(false);
                return;
            }

            byte[] bytes = File.ReadAllBytes(Data.capturedImagePath);
            _previewTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (_previewTex.LoadImage(bytes))
            {
                capturePreviewImage.texture = _previewTex;
                capturePreviewImage.gameObject.SetActive(true);
            }
        }

        private void HandleMemoChanged(string text)
        {
            if (Data == null) return;
            Data.userMemo = text;
            AppRoot.Instance?.Campaign.AutoSaveActiveState(AppRoot.Instance.RuntimeState);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = (_rootCanvas != null && _rootCanvas.scaleFactor > 0f) ? _rootCanvas.scaleFactor : 1f;
            _rect.anchoredPosition += eventData.delta / scale;
            _board?.NotifyCardMoved();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Data == null) return;
            Data.boardPosition = SerializableVector2.FromVector2(_rect.anchoredPosition);
            AppRoot.Instance?.Campaign.AutoSaveActiveState(AppRoot.Instance.RuntimeState);
        }
    }
}

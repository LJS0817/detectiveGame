using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.FileViewer
{
    /// <summary>
    /// OSWindow를 직접 상속하는 포렌식 문서 열람기 앱 창 (확정 C안 커스텀).
    /// 별도 메타데이터 팝업 창 없이 하단 접이식 메타데이터 바를 내장하며,
    /// 각 메타데이터 요소(생성일시·수정일시·작성자·원본파일)를 클릭하면 본문 드래그와 동일한 [보드에 추가 | 취소] 메뉴를 띄웁니다.
    /// </summary>
    public class DocumentViewer : OSWindow
    {
        [Header("Document Sheet UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TextDragSelector dragSelector;
        [SerializeField] private FloatingClueToolbar floatingToolbar;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Collapsible Bottom Metadata Bar")]
        [SerializeField] private Button toggleMetadataBarButton;
        [SerializeField] private GameObject metadataContentPanel;
        [SerializeField] private Button createdAtButton;
        [SerializeField] private Button modifiedAtButton;
        [SerializeField] private Button authorButton;
        [SerializeField] private Button sourceFileButton;

        private DocumentData _currentDoc;
        private Canvas _rootCanvas;
        private bool _isMetadataExpanded;

        protected override void Awake()
        {
            base.Awake();
            _rootCanvas = GetComponentInParent<Canvas>();

            if (toggleMetadataBarButton != null)
                toggleMetadataBarButton.onClick.AddListener(ToggleMetadataBar);

            BindMetaFieldButton(createdAtButton, "생성 일시", m => m.createdAt);
            BindMetaFieldButton(modifiedAtButton, "수정 일시", m => m.modifiedAt);
            BindMetaFieldButton(authorButton, "작성자", m => m.author);
            BindMetaFieldButton(sourceFileButton, "원본 파일", m => m.sourceFile);

            SetMetadataExpanded(false);
        }

        public void OpenDocument(DocumentData doc)
        {
            if (doc == null) return;
            _currentDoc = doc;

            if (titleText != null) titleText.text = doc.title;
            if (bodyText != null) bodyText.text = doc.bodyText;
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;

            dragSelector?.BindDocument(doc);
            RefreshMetadataFooter(doc.metadata);
            SetMetadataExpanded(false);

            var state = AppRoot.Instance?.RuntimeState;
            if (state != null)
            {
                state.MarkItemRead(doc.docId);
                state.DiscoverSuspects(doc.revealsSuspectIds);
            }
            Open();
        }

        private void ToggleMetadataBar()
        {
            if (_currentDoc?.metadata == null) return;
            SetMetadataExpanded(!_isMetadataExpanded);
            GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
        }

        private void SetMetadataExpanded(bool expanded)
        {
            _isMetadataExpanded = expanded;
            if (metadataContentPanel != null)
                metadataContentPanel.SetActive(expanded);
        }

        private void RefreshMetadataFooter(DocumentMetadata meta)
        {
            bool hasMeta = meta != null;
            if (toggleMetadataBarButton != null)
            {
                toggleMetadataBarButton.interactable = hasMeta;
                var lbl = toggleMetadataBarButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = hasMeta ? "파일 메타데이터 속성 ▾" : "메타데이터 없음";
            }

            if (!hasMeta) return;

            SetButtonText(createdAtButton, $"생성: {meta.createdAt}");
            SetButtonText(modifiedAtButton, $"수정: {meta.modifiedAt}");
            SetButtonText(authorButton, $"작성자: {meta.author}");
            SetButtonText(sourceFileButton, $"원본: {meta.sourceFile}");
        }

        private void BindMetaFieldButton(Button btn, string fieldLabel, System.Func<DocumentMetadata, string> selector)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() =>
            {
                if (_currentDoc?.metadata == null || AppRoot.Instance == null) return;
                string fieldValue = selector(_currentDoc.metadata);
                if (string.IsNullOrEmpty(fieldValue)) return;

                if (floatingToolbar != null)
                {
                    floatingToolbar.ShowAtScreenPosition(
                        Input.mousePosition,
                        _rootCanvas,
                        onConfirmAdd: () => AddMetadataClueCard(fieldLabel, fieldValue),
                        onCancel: null);
                }
                else
                {
                    AddMetadataClueCard(fieldLabel, fieldValue);
                }
            });
        }

        private void AddMetadataClueCard(string fieldLabel, string fieldValue)
        {
            if (_currentDoc?.metadata == null || AppRoot.Instance == null) return;

            string evidenceId = string.IsNullOrEmpty(_currentDoc.metadata.metadataEvidenceId)
                ? "NONE"
                : _currentDoc.metadata.metadataEvidenceId;

            var card = UserClueCard.Create(
                sourceId: _currentDoc.docId,
                sourceType: ClueSourceType.MetadataExcerpt,
                displayTitle: $"[메타데이터] {_currentDoc.title} — {fieldLabel}",
                hiddenEvidenceId: evidenceId,
                extractedText: $"{fieldLabel}: {fieldValue}\n(생성: {_currentDoc.metadata.createdAt} / 수정: {_currentDoc.metadata.modifiedAt} / 작성자: {_currentDoc.metadata.author})");

            AppRoot.Instance.RuntimeState.AddClueCard(card);
        }

        private static void SetButtonText(Button btn, string text)
        {
            if (btn == null) return;
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = text;
        }
    }
}

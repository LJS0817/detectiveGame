using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.FileViewer
{
    /// <summary>
    /// OSWindow를 직접 상속하는 포렌식 메타데이터(Created_At, Modified_At, Author) 뷰어 앱 창.
    /// </summary>
    public class MetadataViewerController : OSWindow
    {
        [Header("Metadata Viewer UI")]
        [SerializeField] private TMP_Text fileNameText;
        [SerializeField] private TMP_Text createdAtText;
        [SerializeField] private TMP_Text modifiedAtText;
        [SerializeField] private TMP_Text authorText;
        [SerializeField] private TMP_Text sourceFileText;
        [SerializeField] private Button addMetadataClueButton;

        private DocumentData _currentDoc;

        protected override void Awake()
        {
            base.Awake();
            if (addMetadataClueButton != null)
                addMetadataClueButton.onClick.AddListener(AddMetadataToBoard);
        }

        public void OpenForDocument(DocumentData doc)
        {
            if (doc == null || doc.metadata == null) return;
            _currentDoc = doc;

            if (fileNameText != null) fileNameText.text = doc.title;
            if (createdAtText != null) createdAtText.text = $"최초 생성(Created_At): {doc.metadata.createdAt}";
            if (modifiedAtText != null) modifiedAtText.text = $"최종 수정(Modified_At): {doc.metadata.modifiedAt}";
            if (authorText != null) authorText.text = $"작성자(Author): {doc.metadata.author}";
            if (sourceFileText != null) sourceFileText.text = $"원본 파일명: {doc.metadata.sourceFile}";

            Open();
        }

        private void AddMetadataToBoard()
        {
            if (_currentDoc == null || _currentDoc.metadata == null || AppRoot.Instance == null) return;

            var meta = _currentDoc.metadata;
            string summary = $"파일: {meta.sourceFile}\n생성: {meta.createdAt} / 수정: {meta.modifiedAt}\n작성자: {meta.author}";
            string hiddenId = string.IsNullOrEmpty(meta.metadataEvidenceId) ? "NONE" : meta.metadataEvidenceId;

            var card = UserClueCard.Create(
                sourceId: _currentDoc.docId,
                sourceType: ClueSourceType.MetadataExcerpt,
                displayTitle: $"[메타데이터] {_currentDoc.title}",
                hiddenEvidenceId: hiddenId,
                extractedText: summary);

            AppRoot.Instance.RuntimeState.AddClueCard(card);
        }
    }
}

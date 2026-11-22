using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.FileViewer
{
    /// <summary>
    /// OSWindow를 직접 상속하는 스캔 문서·로그 뷰어 앱 창.
    /// </summary>
    public class DocumentViewer : OSWindow
    {
        [Header("Document Viewer UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TextDragSelector dragSelector;
        [SerializeField] private Button openMetadataButton;
        [SerializeField] private ScrollRect scrollRect;

        private DocumentData _currentDoc;

        protected override void Awake()
        {
            base.Awake();
            if (openMetadataButton != null)
                openMetadataButton.onClick.AddListener(HandleOpenMetadata);
        }

        public void OpenDocument(DocumentData doc)
        {
            if (doc == null) return;
            _currentDoc = doc;

            if (titleText != null) titleText.text = doc.title;
            if (bodyText != null) bodyText.text = doc.bodyText;
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;

            dragSelector?.BindDocument(doc);
            if (openMetadataButton != null)
                openMetadataButton.interactable = doc.metadata != null;

            var state = AppRoot.Instance?.RuntimeState;
            if (state != null)
            {
                state.MarkItemRead(doc.docId);
                if (doc.revealsSuspectIds != null)
                {
                    foreach (var sid in doc.revealsSuspectIds)
                        state.DiscoverSuspect(sid);
                }
            }
            Open();
        }

        private void HandleOpenMetadata()
        {
            if (_currentDoc == null || AppRoot.Instance?.Windows == null) return;
            var metaWindow = AppRoot.Instance.Windows.GetWindow<MetadataViewerController>();
            metaWindow?.OpenForDocument(_currentDoc);
        }
    }
}

using System;
using System.Collections.Generic;
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
    /// - 일반 보고서/로그: 단일 공문서 텍스트 시트 + 자유 드래그 발췌
    /// - 문답형 조서(STATEMENT): 수사관/진술인별 배경색·강조 바·줄바꿈이 구분된 대화 카드 블록 자동 렌더링 (각 발언 문장 드래그 발췌 지원)
    /// - 하단 메타데이터 바 상시 표시 및 각 항목 클릭 시 [보드에 추가 | 취소] 메뉴 제공
    /// </summary>
    public class DocumentViewer : OSWindow
    {
        [Header("Document Sheet UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TextDragSelector dragSelector;
        [SerializeField] private FloatingClueToolbar floatingToolbar;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Bottom Metadata Bar (Always Visible)")]
        [SerializeField] private GameObject metadataContentPanel;
        [SerializeField] private Button createdAtButton;
        [SerializeField] private Button modifiedAtButton;
        [SerializeField] private Button authorButton;
        [SerializeField] private Button sourceFileButton;

        [Header("Q&A Statement Block Colors")]
        [SerializeField] private Color headerBoxColor = new Color(0.173f, 0.173f, 0.173f, 1f);        // #2C2C2C
        [SerializeField] private Color headerTextColor = new Color(0.965f, 0.953f, 0.925f, 1f);       // #F6F3EC
        [SerializeField] private Color investigatorBoxColor = new Color(0.894f, 0.867f, 0.816f, 1f);  // #E4DDD0
        [SerializeField] private Color investigatorAccentColor = new Color(0.173f, 0.173f, 0.173f, 1f); // #2C2C2C
        [SerializeField] private Color witnessBoxColor = new Color(0.980f, 0.973f, 0.953f, 1f);       // #FAF8F3
        [SerializeField] private Color suspectAccentColor = new Color(0.510f, 0.141f, 0.141f, 1f);    // #822424 (피의자)
        [SerializeField] private Color witnessAccentColor = new Color(0.706f, 0.525f, 0.141f, 1f);    // #B48624 (참고인)
        [SerializeField] private Color bodyInkColor = new Color(0.110f, 0.106f, 0.098f, 1f);          // #1C1B19

        private DocumentData _currentDoc;
        private Canvas _rootCanvas;
        private RectTransform _statementContainer;
        private readonly List<GameObject> _spawnedStatementBlocks = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            _rootCanvas = GetComponentInParent<Canvas>();

            BindMetaFieldButton(createdAtButton, "생성 일시", m => m.createdAt);
            BindMetaFieldButton(modifiedAtButton, "수정 일시", m => m.modifiedAt);
            BindMetaFieldButton(authorButton, "작성자", m => m.author);
            BindMetaFieldButton(sourceFileButton, "원본 파일", m => m.sourceFile);
        }

        public void OpenDocument(DocumentData doc)
        {
            if (doc == null) return;
            _currentDoc = doc;

            if (titleText != null) titleText.text = doc.title;

            bool isQaStatement = IsQaStatementDocument(doc);
            if (isQaStatement && bodyText != null)
            {
                bodyText.gameObject.SetActive(false);
                BuildStatementBlocks(doc);
            }
            else
            {
                HideStatementBlocks();
                if (bodyText != null)
                {
                    bodyText.gameObject.SetActive(true);
                    bodyText.lineSpacing = 10f;
                    bodyText.text = doc.bodyText;
                }
                dragSelector?.BindDocument(doc);
            }

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                if (scrollRect.content != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
                scrollRect.verticalNormalizedPosition = 1f;
            }

            RefreshMetadataFooter(doc.metadata);

            var state = AppRoot.Instance?.RuntimeState;
            if (state != null)
            {
                state.MarkItemRead(doc.docId);
                state.DiscoverSuspects(doc.revealsSuspectIds);
            }
            Open();
        }

        private static bool IsQaStatementDocument(DocumentData doc)
        {
            if (doc == null || string.IsNullOrEmpty(doc.bodyText)) return false;
            return string.Equals(doc.category, "STATEMENT", StringComparison.OrdinalIgnoreCase) &&
                   doc.bodyText.Contains("수사관:");
        }

        private void EnsureStatementContainer()
        {
            if (_statementContainer != null || bodyText == null) return;

            Transform parent = bodyText.transform.parent;
            var go = new GameObject("StatementBlocksRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            _statementContainer = go.GetComponent<RectTransform>();
            _statementContainer.anchorMin = new Vector2(0f, 1f);
            _statementContainer.anchorMax = new Vector2(1f, 1f);
            _statementContainer.pivot = new Vector2(0.5f, 1f);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 14);
            vlg.spacing = 10f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = go.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void HideStatementBlocks()
        {
            if (_statementContainer != null)
                _statementContainer.gameObject.SetActive(false);
        }

        private void BuildStatementBlocks(DocumentData doc)
        {
            EnsureStatementContainer();
            if (_statementContainer == null) return;

            foreach (var go in _spawnedStatementBlocks)
                if (go != null) Destroy(go);
            _spawnedStatementBlocks.Clear();

            _statementContainer.gameObject.SetActive(true);

            bool isSuspectDoc = !string.IsNullOrEmpty(doc.title) && doc.title.Contains("피의자");
            string raw = doc.bodyText ?? string.Empty;
            int cursor = 0;

            while (cursor < raw.Length)
            {
                int newlineIdx = raw.IndexOf('\n', cursor);
                int lineEnd = newlineIdx >= 0 ? newlineIdx : raw.Length;
                string line = raw.Substring(cursor, lineEnd - cursor);
                int lineStartOffset = cursor;
                cursor = newlineIdx >= 0 ? newlineIdx + 1 : raw.Length;

                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // 조서 상단 제목 줄 ([피의자 신문 조서 — ...])
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    CreateHeaderBlock(trimmed);
                    continue;
                }

                // "화자: 발언 내용" 파싱
                int colonIdx = line.IndexOf(':');
                if (colonIdx > 0 && colonIdx < 16)
                {
                    string speaker = line.Substring(0, colonIdx).Trim();
                    int contentStartInLine = colonIdx + 1;
                    while (contentStartInLine < line.Length && line[contentStartInLine] == ' ')
                        contentStartInLine++;

                    string speech = line.Substring(contentStartInLine).TrimEnd();
                    int globalOffset = lineStartOffset + contentStartInLine;
                    bool isInvestigator = speaker == "수사관";

                    CreateDialogueBlock(
                        doc,
                        speaker,
                        speech,
                        globalOffset,
                        isInvestigator,
                        isSuspectDoc);
                }
                else
                {
                    CreateDialogueBlock(doc, string.Empty, trimmed, lineStartOffset, false, isSuspectDoc);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_statementContainer);
        }

        private void CreateHeaderBlock(string headerText)
        {
            var box = new GameObject("HeaderBlock", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            box.transform.SetParent(_statementContainer, false);
            box.GetComponent<Image>().color = headerBoxColor;

            var vlg = box.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 10);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var tmp = CreateStyledTmp("HeaderText", box.transform, headerText, GetBaseFontSize() - 1f, FontStyles.Bold, headerTextColor);
            tmp.alignment = TextAlignmentOptions.Left;
            _spawnedStatementBlocks.Add(box);
        }

        private void CreateDialogueBlock(
            DocumentData doc,
            string speaker,
            string speech,
            int globalStartOffset,
            bool isInvestigator,
            bool isSuspectDoc)
        {
            Color bgColor = isInvestigator ? investigatorBoxColor : witnessBoxColor;
            Color accentColor = isInvestigator
                ? investigatorAccentColor
                : (isSuspectDoc ? suspectAccentColor : witnessAccentColor);

            var box = new GameObject("TurnBox", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            box.transform.SetParent(_statementContainer, false);
            box.GetComponent<Image>().color = bgColor;

            var hlg = box.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 14, 0, 0);
            hlg.spacing = 12f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 좌측 화자 구분용 세로 강조 바 (5px)
            var barGo = new GameObject("AccentBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            barGo.transform.SetParent(box.transform, false);
            barGo.GetComponent<Image>().color = accentColor;
            var barLe = barGo.GetComponent<LayoutElement>();
            barLe.minWidth = 5f;
            barLe.preferredWidth = 5f;
            barLe.flexibleWidth = 0f;

            // 우측 텍스트 컨테이너 (화자 줄 + 줄바꿈 + 발언 본문)
            var textCol = new GameObject("TextColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            textCol.transform.SetParent(box.transform, false);
            var colLe = textCol.GetComponent<LayoutElement>();
            colLe.flexibleWidth = 1f;

            var vlg = textCol.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 10, 10);
            vlg.spacing = 5f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(speaker))
            {
                string roleTag = isInvestigator ? "Q  수사관" : $"A  {speaker}";
                CreateStyledTmp("SpeakerLabel", textCol.transform, roleTag, GetBaseFontSize() - 2f, FontStyles.Bold, accentColor);
            }

            var speechTmp = CreateStyledTmp("SpeechText", textCol.transform, speech, GetBaseFontSize(), FontStyles.Normal, bodyInkColor);
            speechTmp.lineSpacing = 8f;

            var selector = speechTmp.gameObject.AddComponent<TextDragSelector>();
            selector.ConfigureToolbar(floatingToolbar);
            selector.BindDocument(doc, speech, globalStartOffset);

            _spawnedStatementBlocks.Add(box);
        }

        private TMP_Text CreateStyledTmp(string objName, Transform parent, string text, float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (bodyText != null && bodyText.font != null)
                tmp.font = bodyText.font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            return tmp;
        }

        private float GetBaseFontSize() => bodyText != null && bodyText.fontSize > 0f ? bodyText.fontSize : 17f;

        private void RefreshMetadataFooter(DocumentMetadata meta)
        {
            bool hasMeta = meta != null;
            if (metadataContentPanel != null)
                metadataContentPanel.SetActive(hasMeta);

            if (!hasMeta) return;

            SetButtonText(createdAtButton, $"생성: {meta.createdAt}");
            SetButtonText(modifiedAtButton, $"수정: {meta.modifiedAt}");
            SetButtonText(authorButton, $"작성자: {meta.author}");
            SetButtonText(sourceFileButton, $"원본: {meta.sourceFile}");
        }

        private void BindMetaFieldButton(Button btn, string fieldLabel, Func<DocumentMetadata, string> selector)
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

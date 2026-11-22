using System;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ColdCaseOS.Apps.FileViewer
{
    /// <summary>
    /// TMP_Text 위에서 마우스로 원하는 텍스트 구간을 자유 드래그 선택(최대 120자 제한)하고,
    /// 플로팅 툴바를 통해 증거판에 단서 카드를 추가하는 컴포넌트.
    /// 정답 여부는 UI에 노출하지 않고 60% 겹침 비율로 내부 hiddenEvidenceId만 매핑합니다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextDragSelector : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public const int MaxSelectionChars = 120;
        public const int MinSelectionChars = 4;
        private const float OverlapThreshold = 0.60f;

        [SerializeField] private FloatingClueToolbar floatingToolbar;
        [SerializeField] private Color32 selectionHighlightColor = new Color32(184, 134, 24, 255); // #B88618 머스타드
        [SerializeField] private Color32 defaultTextColor = new Color32(28, 27, 25, 255);          // #1C1B19 먹색

        private TMP_Text _tmpText;
        private Canvas _rootCanvas;
        private DocumentData _currentDoc;

        private bool _isDragging;
        private int _startCharIdx = -1;
        private int _endCharIdx = -1;

        private void Awake()
        {
            _tmpText = GetComponent<TMP_Text>();
            _rootCanvas = GetComponentInParent<Canvas>();
        }

        public void BindDocument(DocumentData doc)
        {
            _currentDoc = doc;
            ClearSelection();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_currentDoc == null || _tmpText == null) return;
            floatingToolbar?.Hide();

            int idx = FindCharacterIndex(eventData.position);
            if (idx < 0)
            {
                ClearSelection();
                return;
            }

            _isDragging = true;
            _startCharIdx = idx;
            _endCharIdx = idx;
            ApplyHighlight(_startCharIdx, _endCharIdx);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _tmpText == null) return;
            int idx = FindCharacterIndex(eventData.position);
            if (idx < 0) return;

            // 통드래그 꼼수 방지: 최대 120자 제한
            if (Mathf.Abs(idx - _startCharIdx) > MaxSelectionChars)
            {
                idx = idx > _startCharIdx
                    ? _startCharIdx + MaxSelectionChars
                    : _startCharIdx - MaxSelectionChars;
            }

            _endCharIdx = Mathf.Clamp(idx, 0, Mathf.Max(0, _tmpText.textInfo.characterCount - 1));
            ApplyHighlight(_startCharIdx, _endCharIdx);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;

            int minIdx = Mathf.Min(_startCharIdx, _endCharIdx);
            int maxIdx = Mathf.Max(_startCharIdx, _endCharIdx);
            int length = maxIdx - minIdx + 1;

            if (minIdx < 0 || length < MinSelectionChars)
            {
                ClearSelection();
                return;
            }

            if (floatingToolbar != null)
            {
                floatingToolbar.ShowAtScreenPosition(
                    eventData.position,
                    _rootCanvas,
                    onConfirmAdd: () => CommitSelectionToBoard(minIdx, maxIdx),
                    onCancel: ClearSelection);
            }
            else
            {
                CommitSelectionToBoard(minIdx, maxIdx);
            }
        }

        private void CommitSelectionToBoard(int minIdx, int maxIdx)
        {
            if (_currentDoc == null || AppRoot.Instance == null) return;

            string fullText = _currentDoc.bodyText ?? string.Empty;
            int safeStart = Mathf.Clamp(minIdx, 0, fullText.Length - 1);
            int safeLen = Mathf.Clamp(maxIdx - safeStart + 1, 1, fullText.Length - safeStart);
            string excerpt = fullText.Substring(safeStart, safeLen).Trim();

            string matchedEvidenceId = EvaluateHiddenEvidenceId(safeStart, safeStart + safeLen - 1);
            string shortTitle = $"[{_currentDoc.title}] 발췌";

            var card = UserClueCard.Create(
                sourceId: _currentDoc.docId,
                sourceType: ClueSourceType.TextExcerpt,
                displayTitle: shortTitle,
                hiddenEvidenceId: matchedEvidenceId,
                extractedText: excerpt);

            AppRoot.Instance.RuntimeState.AddClueCard(card);
            ClearSelection();
        }

        private string EvaluateHiddenEvidenceId(int selStart, int selEnd)
        {
            if (_currentDoc?.hiddenTags == null) return "NONE";

            string bestId = "NONE";
            float bestRatio = 0f;

            foreach (var tag in _currentDoc.hiddenTags)
            {
                if (tag == null || tag.Length <= 0) continue;

                int overlapStart = Mathf.Max(selStart, tag.startCharIndex);
                int overlapEnd = Mathf.Min(selEnd, tag.endCharIndex);
                if (overlapEnd < overlapStart) continue;

                int overlapLen = overlapEnd - overlapStart + 1;
                float ratio = (float)overlapLen / tag.Length;
                if (ratio >= OverlapThreshold && ratio > bestRatio)
                {
                    bestRatio = ratio;
                    bestId = tag.evidenceId;
                }
            }

            return bestId;
        }

        private int FindCharacterIndex(Vector2 screenPos)
        {
            Camera cam = (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _rootCanvas.worldCamera
                : null;
            return TMP_TextUtilities.FindIntersectingCharacter(_tmpText, screenPos, cam, true);
        }

        private void ApplyHighlight(int a, int b)
        {
            _tmpText.ForceMeshUpdate();
            var textInfo = _tmpText.textInfo;
            int minIdx = Mathf.Min(a, b);
            int maxIdx = Mathf.Max(a, b);

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int matIdx = charInfo.materialReferenceIndex;
                int vIdx = charInfo.vertexIndex;
                Color32[] colors = textInfo.meshInfo[matIdx].colors32;
                Color32 col = (i >= minIdx && i <= maxIdx) ? selectionHighlightColor : defaultTextColor;

                colors[vIdx + 0] = col;
                colors[vIdx + 1] = col;
                colors[vIdx + 2] = col;
                colors[vIdx + 3] = col;
            }

            _tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        public void ClearSelection()
        {
            _isDragging = false;
            _startCharIdx = -1;
            _endCharIdx = -1;
            floatingToolbar?.HideImmediate();
            if (_tmpText != null && !string.IsNullOrEmpty(_tmpText.text))
            {
                ApplyHighlight(-1, -1);
            }
        }
    }
}

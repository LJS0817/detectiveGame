using System;
using System.Collections.Generic;
using System.Linq;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.Report
{
    /// <summary>
    /// OSWindow를 직접 상속하는 검찰 송치 보고서 작성 및 검증 앱 창.
    /// 피의자 드롭다운에는 플레이어가 문서·DB·사진·CCTV·메신저 등으로 실제 발견한 인물(DiscoveredSuspectIds)만 표시됩니다.
    /// </summary>
    public class ReportValidator : OSWindow
    {
        [Serializable]
        public class SlotBinding
        {
            public TMP_Text slotLabelText;
            public TMP_Dropdown clueDropdown;
        }

        [Header("Report Form UI")]
        [SerializeField] private TMP_Text caseHeaderText;
        [SerializeField] private TMP_Dropdown culpritDropdown;
        [SerializeField] private List<SlotBinding> slotBindings = new List<SlotBinding>();
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private GameObject feedbackPanel;

        [Header("Settlement View")]
        [SerializeField] private ResultSettlementView settlementView;

        private readonly List<CulpritOption> _visibleCulprits = new List<CulpritOption>();
        private readonly List<UserClueCard> _selectableCards = new List<UserClueCard>();

        protected override void Awake()
        {
            base.Awake();
            if (submitButton != null)
                submitButton.onClick.AddListener(SubmitReport);
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnSuspectDiscovered += HandleSuspectDiscovered;
            GameEvents.OnClueCardAdded += HandleClueChanged;
            GameEvents.OnClueCardUpdated += HandleClueChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnSuspectDiscovered -= HandleSuspectDiscovered;
            GameEvents.OnClueCardAdded -= HandleClueChanged;
            GameEvents.OnClueCardUpdated -= HandleClueChanged;
        }

        protected override void OnWindowFocused()
        {
            RefreshAll();
        }

        private void HandleEpisodeLoaded(EpisodeData _)
        {
            HideFeedback();
            RefreshAll();
        }

        private void HandleSuspectDiscovered(string _) => RefreshCulpritDropdown();
        private void HandleClueChanged(UserClueCard _) => RefreshSlotDropdowns();

        public void RefreshAll()
        {
            var episode = AppRoot.Instance?.RuntimeState?.Episode;
            if (episode == null) return;

            if (caseHeaderText != null)
                caseHeaderText.text = $"{episode.caseNumber} — {episode.title}";

            RefreshCulpritDropdown();
            RefreshSlotDropdowns();
        }

        private void RefreshCulpritDropdown()
        {
            if (culpritDropdown == null) return;

            var state = AppRoot.Instance?.RuntimeState;
            var rule = state?.Episode?.answerRule;
            if (state == null || rule == null) return;

            string previousSelectedId = GetSelectedCulprit()?.id;

            _visibleCulprits.Clear();
            foreach (var opt in rule.culpritOptions)
            {
                if (state.DiscoveredSuspectIds.Contains(opt.id))
                    _visibleCulprits.Add(opt);
            }

            culpritDropdown.ClearOptions();
            if (_visibleCulprits.Count == 0)
            {
                culpritDropdown.AddOptions(new List<string> { "발견된 용의자 없음 (문서·DB 확인 필요)" });
                culpritDropdown.interactable = false;
                if (submitButton != null) submitButton.interactable = false;
                return;
            }

            culpritDropdown.AddOptions(_visibleCulprits.Select(c => c.label).ToList());
            culpritDropdown.interactable = true;
            if (submitButton != null) submitButton.interactable = true;

            int restoreIdx = _visibleCulprits.FindIndex(c => c.id == previousSelectedId);
            culpritDropdown.SetValueWithoutNotify(restoreIdx >= 0 ? restoreIdx : 0);
        }

        private void RefreshSlotDropdowns()
        {
            var state = AppRoot.Instance?.RuntimeState;
            var slots = state?.Episode?.answerRule?.slots;
            if (state == null || slots == null) return;

            _selectableCards.Clear();
            foreach (var card in state.ClueCards)
            {
                if (!card.isInTrash && !card.isDismissed)
                    _selectableCards.Add(card);
            }

            var optionLabels = new List<string> { "-- 증거 카드 선택 --" };
            foreach (var card in _selectableCards)
                optionLabels.Add(card.displayTitle);

            for (int i = 0; i < slotBindings.Count; i++)
            {
                var binding = slotBindings[i];
                bool hasRule = i < slots.Count;
                if (binding.slotLabelText != null)
                {
                    binding.slotLabelText.gameObject.SetActive(hasRule);
                    if (hasRule) binding.slotLabelText.text = slots[i].label;
                }

                if (binding.clueDropdown != null)
                {
                    binding.clueDropdown.gameObject.SetActive(hasRule);
                    if (!hasRule) continue;

                    int prevIdx = binding.clueDropdown.value;
                    binding.clueDropdown.ClearOptions();
                    binding.clueDropdown.AddOptions(optionLabels);
                    binding.clueDropdown.SetValueWithoutNotify(Mathf.Clamp(prevIdx, 0, optionLabels.Count - 1));
                }
            }
        }

        private CulpritOption GetSelectedCulprit()
        {
            if (culpritDropdown == null || _visibleCulprits.Count == 0) return null;
            int idx = culpritDropdown.value;
            return (idx >= 0 && idx < _visibleCulprits.Count) ? _visibleCulprits[idx] : null;
        }

        private UserClueCard GetSelectedCardForSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slotBindings.Count) return null;
            var dd = slotBindings[slotIndex].clueDropdown;
            if (dd == null || dd.value <= 0) return null;
            int cardIdx = dd.value - 1;
            return (cardIdx >= 0 && cardIdx < _selectableCards.Count) ? _selectableCards[cardIdx] : null;
        }

        private void SubmitReport()
        {
            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            var episode = state?.Episode;
            var rule = episode?.answerRule;
            if (app == null || state == null || episode == null || rule == null) return;

            var selectedCulprit = GetSelectedCulprit();
            if (selectedCulprit == null)
            {
                ShowFeedback("피의자(또는 급습 대상)를 먼저 발견하고 선택해야 합니다.");
                return;
            }

            // 1. 슬롯 미선택 및 과소 캡처(isTooSmallCapture) 우선 검증
            for (int i = 0; i < rule.slots.Count; i++)
            {
                var card = GetSelectedCardForSlot(i);
                if (card == null)
                {
                    ShowFeedback($"[{rule.slots[i].label}] 항목에 제출할 증거 카드가 비어 있습니다.");
                    return;
                }

                if (card.isTooSmallCapture)
                {
                    ShowFeedback($"[{rule.slots[i].label}] 캡처된 영역이 너무 작아 핵심 특징을 식별할 수 없습니다. 사진이나 CCTV를 더 확대하여 다시 캡처해 주세요.");
                    return;
                }
            }

            // 2. 피의자 일치 검증
            if (selectedCulprit.id != rule.correctCulpritId)
            {
                string msg = string.IsNullOrEmpty(selectedCulprit.wrongFeedback)
                    ? "지목한 피의자와 제출된 정황이 일치하지 않습니다. 진술서와 물증을 다시 대조해 주세요."
                    : selectedCulprit.wrongFeedback;
                ShowFeedback(msg);
                GameEvents.RaiseSfxRequested("SFX_REPORT_REJECT");
                return;
            }

            // 3. 각 슬롯별 hiddenEvidenceId 검증
            for (int i = 0; i < rule.slots.Count; i++)
            {
                var slotRule = rule.slots[i];
                var card = GetSelectedCardForSlot(i);
                bool matched = !string.IsNullOrEmpty(card.hiddenEvidenceId) &&
                               slotRule.acceptedEvidenceIds != null &&
                               slotRule.acceptedEvidenceIds.Contains(card.hiddenEvidenceId);

                if (!matched)
                {
                    string msg = string.IsNullOrEmpty(slotRule.wrongFeedback)
                        ? $"[{slotRule.label}] 제출된 증거가 해당 혐의를 직접 입증하지 못합니다."
                        : slotRule.wrongFeedback;
                    ShowFeedback(msg);
                    GameEvents.RaiseSfxRequested("SFX_REPORT_REJECT");
                    return;
                }
            }

            // 4. 송치 승인 및 정산 처리
            HideFeedback();
            GameEvents.RaiseSfxRequested("SFX_REPORT_APPROVED");

            bool clearedWithinDeadline = !state.IsOvertime && state.ElapsedMinutes <= episode.submissionDeadlineMinutes;
            var tier = ResolveArrestReportTier(episode, state.ElapsedMinutes, app.Campaign.SaveData.CountCompleteVolatileClues());

            app.Campaign.RecordEpisodeClear(
                episode.episodeId,
                state.ElapsedMinutes,
                clearedWithinDeadline,
                state.CurrentVolatileClueState,
                tier?.endingType ?? string.Empty);

            GameEvents.RaiseEpisodeCleared(tier, state.ElapsedMinutes, state.CurrentVolatileClueState);
            Close();

            if (settlementView != null)
            {
                settlementView.Show(episode, tier, state.ElapsedMinutes, clearedWithinDeadline, state.CurrentVolatileClueState);
            }
        }

        private static ArrestReportTier ResolveArrestReportTier(EpisodeData episode, int elapsedMinutes, int completeVolatileCount)
        {
            if (episode.arrestReports == null || episode.arrestReports.Count == 0)
                return null;

            bool isFinale = string.Equals(episode.episodeId, "EP07", StringComparison.OrdinalIgnoreCase);
            if (isFinale)
            {
                // EP07: 휘발성 단서 확보 개수 기준 엔딩 분기 (내림차순으로 충족되는 최고 티어 선택)
                var ordered = episode.arrestReports
                    .OrderByDescending(r => r.minCompleteVolatileClues)
                    .ToList();
                foreach (var r in ordered)
                {
                    if (completeVolatileCount >= r.minCompleteVolatileClues)
                        return r;
                }
                return ordered[ordered.Count - 1];
            }

            // EP01~EP06: 소요 시간(elapsedMinutes) 기준 동적 체포 보고서 3티어 선택
            foreach (var r in episode.arrestReports)
            {
                if (elapsedMinutes <= r.maxElapsedMinutes)
                    return r;
            }
            return episode.arrestReports[episode.arrestReports.Count - 1];
        }

        private void ShowFeedback(string message)
        {
            if (feedbackPanel != null) feedbackPanel.SetActive(true);
            if (feedbackText != null) feedbackText.text = message;
        }

        private void HideFeedback()
        {
            if (feedbackPanel != null) feedbackPanel.SetActive(false);
            if (feedbackText != null) feedbackText.text = string.Empty;
        }
    }
}

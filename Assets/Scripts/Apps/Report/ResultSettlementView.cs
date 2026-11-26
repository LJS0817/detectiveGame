using ColdCaseOS.Core;
using ColdCaseOS.Data;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.Report
{
    /// <summary>
    /// 최종 보고서 정답 승인 후 표시되는 수사 종결 팝업.
    /// 다음 에피소드가 있으면 [다음 사건 받기 (EPxx)] 버튼으로 즉시 다음 사건을 시작하고, 최종장(EP07)이면 사건 캐비닛으로 복귀합니다.
    /// </summary>
    public class ResultSettlementView : MonoBehaviour
    {
        [Header("Settlement UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text caseTitleText;
        [SerializeField] private TMP_Text headlineText;
        [SerializeField] private TMP_Text partnerClosingText;
        [SerializeField] private TMP_Text timeSummaryText;
        [SerializeField] private TMP_Text volatileStatusText;
        [SerializeField] private TMP_Text trustStatusText;
        [SerializeField] private Button confirmButton;

        private string _clearedEpisodeId;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
            gameObject.SetActive(false);
        }

        public void Show(
            EpisodeData episode,
            ArrestReportTier tier,
            int elapsedMinutes,
            bool clearedWithinDeadline,
            VolatileClueState volatileState)
        {
            if (episode == null) return;
            _clearedEpisodeId = episode.episodeId;

            string formattedElapsed = TimeManager.FormatMinutesKorean(elapsedMinutes);
            string formattedDeadline = TimeManager.FormatMinutesKorean(episode.submissionDeadlineMinutes);

            if (caseTitleText != null)
                caseTitleText.text = $"{episode.caseNumber} — {episode.title}";

            if (headlineText != null)
            {
                string rawHeadline = tier != null ? tier.headline : "송치 보고서 승인 완료 ({TIME} 소요)";
                headlineText.text = rawHeadline.Replace("{TIME}", formattedElapsed);
            }

            if (partnerClosingText != null)
                partnerClosingText.text = tier != null ? tier.partnerClosing : string.Empty;

            if (timeSummaryText != null)
            {
                bool hasDeadline = episode.submissionDeadlineMinutes > 0;
                timeSummaryText.gameObject.SetActive(hasDeadline);
                if (hasDeadline)
                {
                    string deadlineTag = clearedWithinDeadline ? "마감 시한 내 송치" : "연장 수사 송치";
                    timeSummaryText.text = $"소요 시간: {formattedElapsed} / 시한: {formattedDeadline} ({deadlineTag})";
                }
            }

            if (volatileStatusText != null)
            {
                bool hasVolatile = episode.volatileDeadlineMinutes > 0;
                volatileStatusText.gameObject.SetActive(hasVolatile);
                if (hasVolatile)
                {
                    volatileStatusText.text = volatileState switch
                    {
                        VolatileClueState.Complete => "휘발성 단서: 원본 완전 확보",
                        VolatileClueState.Damaged => "휘발성 단서: 일부 훼손 확보",
                        _ => "휘발성 단서: 미확보"
                    };
                }
            }

            if (trustStatusText != null && AppRoot.Instance != null)
            {
                int lv = AppRoot.Instance.Campaign.SaveData.trustLevel;
                trustStatusText.text = $"전담팀 신뢰도: Lv.{lv} / {CampaignSaveData.MaxTrustLevel}";
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = true;
                string nextEpId = AppRoot.GetNextEpisodeId(_clearedEpisodeId);
                var btnLabel = confirmButton.GetComponentInChildren<TMP_Text>();
                if (btnLabel != null)
                {
                    btnLabel.text = !string.IsNullOrEmpty(nextEpId)
                        ? $"다음 사건 받기 ({nextEpId})"
                        : "사건 캐비닛으로";
                }
            }

            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
            }
        }

        private void OnConfirmClicked()
        {
            if (confirmButton != null) confirmButton.interactable = false;
            HideAsync().Forget();
        }

        private async UniTaskVoid HideAsync()
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                await canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
            }
            gameObject.SetActive(false);

            var app = AppRoot.Instance;
            if (app == null) return;

            string nextEpId = AppRoot.GetNextEpisodeId(_clearedEpisodeId);
            if (!string.IsNullOrEmpty(nextEpId))
            {
                await app.LoadAndStartEpisodeAsync(nextEpId, resumeSnapshot: false);
            }
            else
            {
                await app.SwitchScreenAsync(AppScreenState.CaseCabinet);
            }
        }
    }
}

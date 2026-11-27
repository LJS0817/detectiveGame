using ColdCaseOS.Core;
using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.OS
{
    /// <summary>
    /// K-POL OS 하단 작업표시줄 컨트롤러.
    /// 6개 핵심 앱(파일 탐색기, 증거판, 경찰 DB, 현장수사반, 용의자 심문, 최종 보고서) + 현재 시각·남은 시한 HUD를 표시합니다.
    /// </summary>
    public class TaskbarController : MonoBehaviour
    {
        [Header("HUD Texts")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text deadlineText;

        [Header("App Toggle Buttons (6 Core Apps)")]
        [SerializeField] private Button fileExplorerButton;
        [SerializeField] private Button evidenceBoardButton;
        [SerializeField] private Button policeDbButton;
        [SerializeField] private Button fieldCommandButton;
        [SerializeField] private Button interrogationButton;
        [SerializeField] private Button reportButton;

        [Header("Window IDs")]
        [SerializeField] private string fileExplorerWindowId = "FileExplorer";
        [SerializeField] private string evidenceBoardWindowId = "EvidenceBoard";
        [SerializeField] private string policeDbWindowId = "PoliceDB";
        [SerializeField] private string fieldCommandWindowId = "FieldCommand";
        [SerializeField] private string interrogationWindowId = "Interrogation";
        [SerializeField] private string reportWindowId = "Report";

        private void Awake()
        {
            BindToggle(fileExplorerButton, fileExplorerWindowId);
            BindToggle(evidenceBoardButton, evidenceBoardWindowId);
            BindToggle(policeDbButton, policeDbWindowId);
            BindToggle(fieldCommandButton, fieldCommandWindowId);
            BindToggle(interrogationButton, interrogationWindowId);
            BindToggle(reportButton, reportWindowId);
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnTimeAdvanced += HandleTimeAdvanced;
            GameEvents.OnVolatileDeadlinePassed += HandleDeadlineChanged;
            GameEvents.OnOvertimeEntered += HandleDeadlineChanged;
            RefreshHud();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnTimeAdvanced -= HandleTimeAdvanced;
            GameEvents.OnVolatileDeadlinePassed -= HandleDeadlineChanged;
            GameEvents.OnOvertimeEntered -= HandleDeadlineChanged;
        }

        private void BindToggle(Button btn, string windowId)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => AppRoot.Instance?.Windows?.ToggleWindow(windowId));
        }

        private void HandleEpisodeLoaded(EpisodeData _) => RefreshHud();
        private void HandleTimeAdvanced(int _, int __) => RefreshHud();
        private void HandleDeadlineChanged() => RefreshHud();

        public void RefreshHud()
        {
            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            if (app == null || state == null || state.Episode == null) return;

            if (clockText != null)
                clockText.text = app.Time.FormattedCurrentFull;

            if (deadlineText != null)
            {
                if (state.Episode.submissionDeadlineMinutes <= 0)
                {
                    deadlineText.text = "자유 수사 모드";
                }
                else if (state.IsOvertime || app.Time.RemainingSubmissionMinutes <= 0)
                {
                    deadlineText.text = "<color=#D96B6B>연장 수사 진행 중 (송치 시한 초과)</color>";
                }
                else
                {
                    string remSub = TimeManager.FormatMinutesKorean(app.Time.RemainingSubmissionMinutes);
                    string volPart = app.Time.RemainingVolatileMinutes > 0
                        ? $"골든타임 {TimeManager.FormatMinutesKorean(app.Time.RemainingVolatileMinutes)}"
                        : "<color=#D9A24B>골든타임 경과</color>";
                    deadlineText.text = $"송치 마감 -{remSub}  |  {volPart}";
                }
            }
        }
    }
}

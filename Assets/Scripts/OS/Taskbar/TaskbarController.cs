using ColdCaseOS.Core;
using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.OS
{
    /// <summary>
    /// K-POL OS 하단(또는 상단) 작업표시줄 및 HUD 컨트롤러.
    /// 현재 시각, 송치 마감·휘발성 골든타임 잔여 시간, 파트너 현재 위치, 메신저 NEW 배지 및 앱 창 토글을 관리합니다.
    /// </summary>
    public class TaskbarController : MonoBehaviour
    {
        [Header("HUD Texts")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text submissionDeadlineText;
        [SerializeField] private TMP_Text volatileDeadlineText;
        [SerializeField] private TMP_Text partnerLocationText;

        [Header("Colors")]
        [SerializeField] private Color normalDeadlineColor = Color.white;
        [SerializeField] private Color overtimeColor = new Color(0.95f, 0.32f, 0.32f, 1f);
        [SerializeField] private Color volatileActiveColor = new Color(0.95f, 0.78f, 0.25f, 1f);
        [SerializeField] private Color volatileExpiredColor = new Color(0.55f, 0.58f, 0.62f, 1f);

        [Header("App Toggle Buttons")]
        [SerializeField] private Button fileExplorerButton;
        [SerializeField] private Button evidenceBoardButton;
        [SerializeField] private Button tacticalMapButton;
        [SerializeField] private Button policeDbButton;
        [SerializeField] private Button messengerButton;
        [SerializeField] private Button reportButton;

        [Header("Window IDs")]
        [SerializeField] private string fileExplorerWindowId = "FileExplorer";
        [SerializeField] private string evidenceBoardWindowId = "EvidenceBoard";
        [SerializeField] private string tacticalMapWindowId = "TacticalMap";
        [SerializeField] private string policeDbWindowId = "PoliceDB";
        [SerializeField] private string messengerWindowId = "Messenger";
        [SerializeField] private string reportWindowId = "Report";

        [Header("Badges")]
        [SerializeField] private GameObject messengerNewBadge;

        private void Awake()
        {
            BindToggle(fileExplorerButton, fileExplorerWindowId);
            BindToggle(evidenceBoardButton, evidenceBoardWindowId);
            BindToggle(tacticalMapButton, tacticalMapWindowId);
            BindToggle(policeDbButton, policeDbWindowId);
            BindToggle(reportButton, reportWindowId);

            if (messengerButton != null)
            {
                messengerButton.onClick.AddListener(() =>
                {
                    SetMessengerBadge(false);
                    AppRoot.Instance?.Windows?.ToggleWindow(messengerWindowId);
                });
            }
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnTimeAdvanced += HandleTimeAdvanced;
            GameEvents.OnPartnerMoved += HandlePartnerMoved;
            GameEvents.OnMessengerMessageAdded += HandleMessengerMessageAdded;
            GameEvents.OnNewCaseOrderReceived += HandleNewCaseOrderReceived;
            RefreshHud();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnTimeAdvanced -= HandleTimeAdvanced;
            GameEvents.OnPartnerMoved -= HandlePartnerMoved;
            GameEvents.OnMessengerMessageAdded -= HandleMessengerMessageAdded;
            GameEvents.OnNewCaseOrderReceived -= HandleNewCaseOrderReceived;
        }

        private void BindToggle(Button btn, string windowId)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => AppRoot.Instance?.Windows?.ToggleWindow(windowId));
        }

        private void HandleEpisodeLoaded(EpisodeData _) => RefreshHud();
        private void HandleTimeAdvanced(int _, int __) => RefreshHud();
        private void HandlePartnerMoved(string _) => RefreshPartnerLocation();

        private void HandleMessengerMessageAdded(MessengerMessageData _)
        {
            var focused = AppRoot.Instance?.Windows?.FocusedWindow;
            if (focused == null || focused.WindowId != messengerWindowId)
                SetMessengerBadge(true);
        }

        private void HandleNewCaseOrderReceived(string _, string __, string ___)
        {
            SetMessengerBadge(true);
        }

        public void RefreshHud()
        {
            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            if (app == null || state == null || state.Episode == null) return;

            if (clockText != null)
                clockText.text = app.Time.FormattedCurrentFull;

            if (submissionDeadlineText != null)
            {
                int rem = app.Time.RemainingSubmissionMinutes;
                if (rem >= 0)
                {
                    submissionDeadlineText.text = $"송치 마감까지 {TimeManager.FormatMinutesKorean(rem)}";
                    submissionDeadlineText.color = normalDeadlineColor;
                }
                else
                {
                    submissionDeadlineText.text = $"연장 수사 +{TimeManager.FormatMinutesKorean(-rem)}";
                    submissionDeadlineText.color = overtimeColor;
                }
            }

            if (volatileDeadlineText != null)
            {
                int vRem = app.Time.RemainingVolatileMinutes;
                if (vRem > 0)
                {
                    volatileDeadlineText.text = $"휘발성 시한 {TimeManager.FormatMinutesKorean(vRem)}";
                    volatileDeadlineText.color = volatileActiveColor;
                }
                else
                {
                    volatileDeadlineText.text = "휘발성 시한 경과";
                    volatileDeadlineText.color = volatileExpiredColor;
                }
            }

            RefreshPartnerLocation();
            SetMessengerBadge(app.HasPendingEpisode);
        }

        private void RefreshPartnerLocation()
        {
            if (partnerLocationText == null) return;
            var state = AppRoot.Instance?.RuntimeState;
            var episode = state?.Episode;
            if (state == null || episode == null) return;

            var node = episode.mapNodes.Find(n => n.nodeId == state.CurrentPartnerNodeId);
            partnerLocationText.text = $"강민호 경사: {(node != null ? node.name : state.CurrentPartnerNodeId)}";
        }

        private void SetMessengerBadge(bool visible)
        {
            if (messengerNewBadge != null)
                messengerNewBadge.SetActive(visible);
        }
    }
}

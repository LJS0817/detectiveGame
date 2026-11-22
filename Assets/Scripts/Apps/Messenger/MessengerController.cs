using System.Collections.Generic;
using System.Linq;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.Messenger
{
    /// <summary>
    /// OSWindow를 직접 상속하는 K-POL 내부 메신저 앱 창 (확정 A안 — 좌측 2채널 사이드바 + 우측 대화창).
    /// - HQ 채널: 서울청 수사심의관 사건 배정 공지 및 [사건 파일 다운로드] 첨부 버튼
    /// - PARTNER 채널: 강민호 경사 현장 보고, 시한 알림, 2단계 힌트 요청
    /// </summary>
    public class MessengerController : OSWindow
    {
        public const string ChannelHq = "HQ";
        public const string ChannelPartner = "PARTNER";

        [Header("Left Channel Sidebar")]
        [SerializeField] private Button hqChannelButton;
        [SerializeField] private Button partnerChannelButton;
        [SerializeField] private GameObject hqUnreadBadge;
        [SerializeField] private GameObject partnerUnreadBadge;

        [Header("Right Chat UI")]
        [SerializeField] private Transform messageListContainer;
        [SerializeField] private GameObject messageItemPrefab;
        [SerializeField] private Button downloadButtonPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button askPartnerHintButton;
        [SerializeField] private TMP_Text headerStatusText;

        private readonly List<GameObject> _spawnedItems = new List<GameObject>();
        private string _activeChannelId = ChannelHq;
        private int _hintStage;
        private bool _isStartingPendingEpisode;

        protected override void Awake()
        {
            base.Awake();
            if (hqChannelButton != null) hqChannelButton.onClick.AddListener(() => SelectChannel(ChannelHq));
            if (partnerChannelButton != null) partnerChannelButton.onClick.AddListener(() => SelectChannel(ChannelPartner));
            if (askPartnerHintButton != null) askPartnerHintButton.onClick.AddListener(OnAskPartnerHintClicked);
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnMessengerMessageAdded += HandleMessageAdded;
            GameEvents.OnCaseFilesDownloaded += HandleCaseFilesDownloaded;
            RebuildMessageList();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnMessengerMessageAdded -= HandleMessageAdded;
            GameEvents.OnCaseFilesDownloaded -= HandleCaseFilesDownloaded;
        }

        protected override void OnWindowFocused()
        {
            base.OnWindowFocused();
            CheckAndStartPendingEpisodeAsync().Forget();
        }

        private async UniTaskVoid CheckAndStartPendingEpisodeAsync()
        {
            var app = AppRoot.Instance;
            if (_isStartingPendingEpisode || app == null || !app.HasPendingEpisode) return;

            _isStartingPendingEpisode = true;
            try
            {
                bool started = await app.TryStartPendingEpisodeOnMessengerOpenedAsync(WindowId);
                if (started)
                {
                    _hintStage = 0;
                    SelectChannel(ChannelHq);
                }
            }
            finally
            {
                _isStartingPendingEpisode = false;
            }
        }

        public void SelectChannel(string channelId)
        {
            _activeChannelId = string.IsNullOrEmpty(channelId) ? ChannelHq : channelId;
            if (_activeChannelId == ChannelHq && hqUnreadBadge != null) hqUnreadBadge.SetActive(false);
            if (_activeChannelId == ChannelPartner && partnerUnreadBadge != null) partnerUnreadBadge.SetActive(false);

            UpdateHeader();
            if (askPartnerHintButton != null)
                askPartnerHintButton.gameObject.SetActive(_activeChannelId == ChannelPartner);

            RebuildMessageList();
        }

        private void HandleEpisodeLoaded(EpisodeData _)
        {
            _hintStage = 0;
            SelectChannel(ChannelHq);
        }

        private void HandleCaseFilesDownloaded(string _) => RebuildMessageList();

        private void HandleMessageAdded(MessengerMessageData msg)
        {
            string msgChannel = string.IsNullOrEmpty(msg.channelId) ? ChannelPartner : msg.channelId;
            if (msgChannel == _activeChannelId)
            {
                SpawnMessageItem(msg);
                ScrollToBottom();
            }
            else
            {
                if (msgChannel == ChannelHq && hqUnreadBadge != null) hqUnreadBadge.SetActive(true);
                if (msgChannel == ChannelPartner && partnerUnreadBadge != null) partnerUnreadBadge.SetActive(true);
            }
        }

        private void UpdateHeader()
        {
            if (headerStatusText == null) return;
            var ep = AppRoot.Instance?.RuntimeState?.Episode;
            string caseTag = ep != null ? $" ({ep.caseNumber})" : string.Empty;
            headerStatusText.text = _activeChannelId == ChannelHq
                ? $"서울청 수사심의관 — 공식 사건 배정{caseTag}"
                : $"강민호 경사 — 현장 수사 파트너{caseTag}";
        }

        public void RebuildMessageList()
        {
            foreach (var go in _spawnedItems)
                if (go != null) Destroy(go);
            _spawnedItems.Clear();

            var logs = AppRoot.Instance?.RuntimeState?.MessengerLogs;
            if (logs == null) return;

            foreach (var msg in logs)
            {
                string ch = string.IsNullOrEmpty(msg.channelId) ? ChannelPartner : msg.channelId;
                if (ch == _activeChannelId)
                    SpawnMessageItem(msg);
            }

            ScrollToBottom();
        }

        private void SpawnMessageItem(MessengerMessageData msg)
        {
            if (messageListContainer == null || messageItemPrefab == null || msg == null) return;

            var go = Instantiate(messageItemPrefab, messageListContainer);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                string prefix = msg.isSystemAlert ? "[공식 지시] " : string.Empty;
                text.text = $"<b>{prefix}{msg.sender}</b> <size=80%>({msg.timestamp})</size>\n{msg.body}";
            }
            _spawnedItems.Add(go);

            // 사건 파일 다운로드 첨부 버튼
            if (!string.IsNullOrEmpty(msg.downloadableEpisodeId) && downloadButtonPrefab != null)
            {
                var app = AppRoot.Instance;
                var state = app?.RuntimeState;
                bool downloaded = state != null && state.IsCaseFilesDownloaded &&
                                  state.Episode?.episodeId == msg.downloadableEpisodeId;

                var dlBtn = Instantiate(downloadButtonPrefab, messageListContainer);
                var dlLabel = dlBtn.GetComponentInChildren<TMP_Text>();
                if (dlLabel != null)
                {
                    string epTitle = state?.Episode != null ? $"{state.Episode.title} ({state.Episode.caseNumber})" : msg.downloadableEpisodeId;
                    dlLabel.text = downloaded
                        ? $"다운로드 완료 — {epTitle}"
                        : $"사건 파일 다운로드 — {epTitle}";
                }

                dlBtn.interactable = !downloaded;
                dlBtn.onClick.AddListener(() =>
                {
                    if (AppRoot.Instance == null) return;
                    if (AppRoot.Instance.RuntimeState.DownloadCurrentCaseFiles())
                    {
                        AppRoot.Instance.Campaign.AutoSaveActiveState(AppRoot.Instance.RuntimeState);
                    }
                });
                _spawnedItems.Add(dlBtn.gameObject);
            }
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void OnAskPartnerHintClicked()
        {
            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            var episode = state?.Episode;
            if (app == null || state == null || episode == null) return;

            string hintBody;
            if (!state.IsCaseFilesDownloaded)
            {
                hintBody = "경위님, 먼저 서울청 수사심의관 채널에서 [사건 파일 다운로드]를 눌러 초동 자료부터 받으셔야 합니다.";
            }
            else if (_hintStage == 0)
            {
                var unreadDoc = episode.documents.FirstOrDefault(d =>
                    state.UnlockedDocIds.Contains(d.docId) && !state.ReadItemIds.Contains(d.docId));

                hintBody = unreadDoc != null
                    ? $"경위님, 아직 확인하지 않은 문서 [{unreadDoc.title}]가 있습니다. 핵심 인물이나 단서가 있을 수 있으니 먼저 훑어보시죠."
                    : "현재 확보한 문서는 다 보신 것 같습니다. 의심되는 이름이나 차량 번호가 있다면 경찰 DB에서 조회해 보시는 게 좋겠습니다.";
                _hintStage = 1;
            }
            else
            {
                var pendingAction = episode.actions.FirstOrDefault(a =>
                    state.UnlockedNodeIds.Contains(a.nodeId) &&
                    !a.requiresTargetSelect &&
                    !state.IsActionCompleted(a.actionId));

                if (pendingAction != null)
                {
                    var node = episode.mapNodes.Find(n => n.nodeId == pendingAction.nodeId);
                    string nodeName = node != null ? node.name : pendingAction.nodeId;
                    hintBody = $"현장 지도에서 [{nodeName}]의 '{pendingAction.title}' 조사가 아직 남아 있습니다. 지시해 주시면 바로 움직이겠습니다.";
                }
                else
                {
                    hintBody = "주요 현장 자료는 거의 모인 듯합니다. 문서의 모순 구절과 사진·CCTV 캡처를 증거판에 정리한 뒤 송치 보고서를 작성해 보시죠.";
                }
                _hintStage = 0;
            }

            state.AddMessengerMessage("강민호 경사", hintBody, app.Time.FormattedCurrentClock, isSystemAlert: false, channelId: ChannelPartner);
        }
    }
}

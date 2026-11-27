using System;
using System.Collections.Generic;
using System.Threading;
using ColdCaseOS.Core;
using ColdCaseOS.Core.LLM;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.FieldCommand
{
    /// <summary>
    /// OSWindow를 상속하는 현장수사반 지휘 앱 창 (현장반장 강민호 경사 대표 보고 채널).
    /// 자연어 지시 + 증거판 카드 첨부 + 동적 추천 칩 + 2단계 [지시 확정: 소요 시간] + 0분 가설 검증을 지원합니다.
    /// </summary>
    public class FieldCommandController : OSWindow
    {
        [Header("Header & Status")]
        [SerializeField] private TMP_Text providerStatusText;
        [SerializeField] private TMP_Text waitingStatusText;
        [SerializeField] private Button askOpinionButton;

        [Header("Chat Log & Pending Action Confirm")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private Transform chatContentRoot;
        [SerializeField] private Transform pendingActionsContainer;

        [Header("Recommended Chips & Evidence Attach")]
        [SerializeField] private Transform recommendedChipContainer;
        [SerializeField] private TMP_Dropdown evidenceDropdown;
        [SerializeField] private Button attachCardButton;
        [SerializeField] private Button clearAttachButton;
        [SerializeField] private TMP_Text attachedCardsText;

        [Header("Command Input")]
        [SerializeField] private TMP_InputField commandInputField;
        [SerializeField] private Button sendCommandButton;

        [Header("Styling")]
        [SerializeField] private TMP_FontAsset uiFont;

        private static readonly Color PlayerBubbleColor = new Color(0.894f, 0.867f, 0.816f, 1f);  // #E4DDD0
        private static readonly Color FieldBubbleColor = new Color(0.980f, 0.973f, 0.953f, 1f);   // #FAF8F3
        private static readonly Color AlertBubbleColor = new Color(0.945f, 0.910f, 0.800f, 1f);   // #F1E8CC
        private static readonly Color InkColor = new Color(0.110f, 0.106f, 0.098f, 1f);           // #1C1B19
        private static readonly Color AccentColor = new Color(0.706f, 0.525f, 0.141f, 1f);        // #B48624
        private static readonly Color DarkBtnColor = new Color(0.180f, 0.176f, 0.169f, 1f);       // #2E2D2B
        private static readonly Color ConfirmBtnColor = new Color(0.169f, 0.369f, 0.251f, 1f);    // #2B5E40
        private static readonly Color PaperTextColor = new Color(0.965f, 0.953f, 0.925f, 1f);     // #F6F3EC

        private readonly List<GameObject> _spawnedChatRows = new List<GameObject>();
        private readonly List<GameObject> _spawnedPendingRows = new List<GameObject>();
        private readonly List<GameObject> _spawnedChips = new List<GameObject>();
        private readonly List<UserClueCard> _selectableCards = new List<UserClueCard>();
        private readonly List<UserClueCard> _attachedCards = new List<UserClueCard>();

        private CancellationTokenSource _requestCts;
        private bool _isBusy;

        protected override void Awake()
        {
            base.Awake();
            if (sendCommandButton != null) sendCommandButton.onClick.AddListener(HandleSendClicked);
            if (commandInputField != null) commandInputField.onSubmit.AddListener(_ => HandleSendClicked());
            if (askOpinionButton != null) askOpinionButton.onClick.AddListener(HandleAskOpinionClicked);
            if (attachCardButton != null) attachCardButton.onClick.AddListener(HandleAttachSelectedCard);
            if (clearAttachButton != null) clearAttachButton.onClick.AddListener(ClearAttachedCards);

            GameEvents.OnFieldCommandQuoteRequested += HandleQuoteFromDocument;
        }

        private void OnDestroy()
        {
            _requestCts?.Cancel();
            _requestCts?.Dispose();
            GameEvents.OnFieldCommandQuoteRequested -= HandleQuoteFromDocument;
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded += HandleClueChanged;
            GameEvents.OnClueCardUpdated += HandleClueChanged;
            GameEvents.OnActionCompleted += HandleActionCompleted;
            RefreshAll();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded -= HandleClueChanged;
            GameEvents.OnClueCardUpdated -= HandleClueChanged;
            GameEvents.OnActionCompleted -= HandleActionCompleted;
        }

        protected override void OnWindowFocused()
        {
            RefreshProviderBadge();
            RefreshEvidenceDropdown();
            RefreshRecommendedChips();
        }

        private void HandleEpisodeLoaded(EpisodeData episode)
        {
            _attachedCards.Clear();
            ClearPendingActions();
            EnsureInitialGreeting(episode);
            RefreshAll();
        }

        private void HandleClueChanged(UserClueCard _) => RefreshEvidenceDropdown();
        private void HandleActionCompleted(string _, string __)
        {
            RefreshChatLog();
            RefreshRecommendedChips();
        }

        private void HandleQuoteFromDocument(string quote)
        {
            if (string.IsNullOrWhiteSpace(quote)) return;
            Open();
            if (commandInputField != null)
            {
                commandInputField.text = $"'{quote}' 관련해서 현장 확인 및 기록 조회해줘";
                commandInputField.ActivateInputField();
            }
        }

        private void EnsureInitialGreeting(EpisodeData episode)
        {
            var state = AppRoot.Instance?.RuntimeState;
            if (state == null || episode == null || state.FieldCommandLogs.Count > 0) return;

            state.FieldCommandLogs.Add(new FieldCommandMessageData
            {
                sender = "강민호 경사 (현장반장)",
                body = $"팀장님, [{episode.title}] 일선서 초동 자료 이관 완료됐습니다. " +
                       $"송치 보완수사 시한은 {TimeManager.FormatMinutesKorean(episode.submissionDeadlineMinutes)}, 휘발성 골든 타임은 {TimeManager.FormatMinutesKorean(episode.volatileDeadlineMinutes)}입니다. " +
                       "현장 재감식·알리바이 로그 조회·영장 집행 등 필요한 지시를 자연어로 내려주십시오.",
                timestamp = AppRoot.Instance.Time.FormattedCurrentClock,
                isPlayer = false,
                isSystemAlert = true
            });
        }

        public void RefreshAll()
        {
            EnsureInitialGreeting(AppRoot.Instance?.RuntimeState?.Episode);
            RefreshProviderBadge();
            RefreshEvidenceDropdown();
            RefreshAttachedLabel();
            RefreshRecommendedChips();
            RefreshChatLog();
            SetBusy(false, string.Empty);
        }

        private void RefreshProviderBadge()
        {
            if (providerStatusText == null) return;
            string name = AppRoot.Instance?.LlmProvider?.ProviderDisplayName ?? "미연결";
            providerStatusText.text = $"통신 엔진: {name}";
        }

        private void RefreshEvidenceDropdown()
        {
            var state = AppRoot.Instance?.RuntimeState;
            if (state == null || evidenceDropdown == null) return;

            _selectableCards.Clear();
            _selectableCards.AddRange(state.GetActiveClueCards());

            var labels = new List<string> { "-- 영장/근거 카드 첨부 선택 --" };
            foreach (var c in _selectableCards)
                labels.Add(c.displayTitle);

            evidenceDropdown.ClearOptions();
            evidenceDropdown.AddOptions(labels);
            evidenceDropdown.SetValueWithoutNotify(0);
        }

        private void HandleAttachSelectedCard()
        {
            if (evidenceDropdown == null || evidenceDropdown.value <= 0) return;
            int idx = evidenceDropdown.value - 1;
            if (idx < 0 || idx >= _selectableCards.Count) return;

            var card = _selectableCards[idx];
            if (_attachedCards.Contains(card)) return;
            if (_attachedCards.Count >= 2)
            {
                GameEvents.RaiseToastRequested("첨부 제한", "한 번의 지시에 최대 2장의 증거 카드를 첨부할 수 있습니다.");
                return;
            }

            _attachedCards.Add(card);
            RefreshAttachedLabel();
            GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
        }

        private void ClearAttachedCards()
        {
            _attachedCards.Clear();
            RefreshAttachedLabel();
        }

        private void RefreshAttachedLabel()
        {
            if (attachedCardsText == null) return;
            if (_attachedCards.Count == 0)
            {
                attachedCardsText.text = "첨부 카드 없음";
                return;
            }

            var titles = new List<string>();
            foreach (var c in _attachedCards) titles.Add(c.displayTitle);
            attachedCardsText.text = $"첨부({_attachedCards.Count}/2): {string.Join(" / ", titles)}";
        }

        private void RefreshRecommendedChips()
        {
            foreach (var go in _spawnedChips)
                if (go != null) Destroy(go);
            _spawnedChips.Clear();

            var router = AppRoot.Instance?.FieldRouter;
            if (router == null || recommendedChipContainer == null) return;

            var recs = router.GetRecommendedActions(3);
            foreach (var act in recs)
            {
                var captured = act;
                var btn = CreateDynamicButton(
                    $"Chip_{act.actionId}",
                    recommendedChipContainer,
                    $"{Shorten(act.title, 18)} ({act.baseWorkMinutes}분)",
                    DarkBtnColor,
                    PaperTextColor,
                    12,
                    32f);
                btn.onClick.AddListener(() =>
                {
                    if (_isBusy) return;
                    if (commandInputField != null)
                        commandInputField.text = $"{captured.title} 진행해줘";
                    SendCommandAsync($"{captured.title} 진행해줘").Forget();
                });
                _spawnedChips.Add(btn.gameObject);
            }
        }

        private void HandleSendClicked()
        {
            if (_isBusy || commandInputField == null) return;
            string text = commandInputField.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            commandInputField.text = string.Empty;
            SendCommandAsync(text).Forget();
        }

        private void HandleAskOpinionClicked()
        {
            if (_isBusy) return;
            SendCommandAsync("지금까지 확보된 단서 기준으로 가설 검증하고 다음에 어디부터 확인하면 좋을지 의견 말해줘").Forget();
        }

        private async UniTaskVoid SendCommandAsync(string inputText)
        {
            var router = AppRoot.Instance?.FieldRouter;
            if (router == null) return;

            _requestCts?.Cancel();
            _requestCts?.Dispose();
            _requestCts = new CancellationTokenSource();

            ClearPendingActions();
            SetBusy(true, "강민호 경사 무전 수신 및 분석 중...");
            GameEvents.RaiseSfxRequested("SFX_RADIO_TX");

            var attachedCopy = new List<UserClueCard>(_attachedCards);
            _attachedCards.Clear();
            RefreshAttachedLabel();

            try
            {
                var result = await router.RouteCommandAsync(inputText, attachedCopy, _requestCts.Token);
                RefreshChatLog();
                ShowPendingActions(result.confirmableActions);

                if (!string.IsNullOrEmpty(result.errorMessage) && result.usedMockFallback)
                {
                    GameEvents.RaiseToastRequested("오프라인 폴백 전환", "OpenAI API 연결 실패로 로컬 시뮬레이터가 응답했습니다.");
                }
            }
            catch (OperationCanceledException)
            {
                // ignored on close/cancel
            }
            finally
            {
                SetBusy(false, string.Empty);
                RefreshProviderBadge();
            }
        }

        private void ShowPendingActions(List<FieldActionData> actions)
        {
            ClearPendingActions();
            if (actions == null || actions.Count == 0 || pendingActionsContainer == null) return;

            var time = AppRoot.Instance?.Time;
            foreach (var act in actions)
            {
                var captured = act;
                int mins = time != null ? time.CalculateBreakdown(0, captured).TotalMinutes : captured.baseWorkMinutes;
                var row = new GameObject($"Confirm_{captured.actionId}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                row.transform.SetParent(pendingActionsContainer, false);
                row.GetComponent<Image>().color = AlertBubbleColor;
                row.GetComponent<LayoutElement>().minHeight = 44f;

                var hlg = row.GetComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(10, 10, 6, 6);
                hlg.spacing = 8f;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;

                var label = CreateTmp("Label", row.transform, $"<b>[지시 대기]</b> {captured.title} (예상 +{TimeManager.FormatMinutesKorean(mins)})", 13, InkColor);
                var labelLe = label.gameObject.AddComponent<LayoutElement>();
                labelLe.flexibleWidth = 1f;

                var confirmBtn = CreateDynamicButton("ConfirmBtn", row.transform, "지시 확정", ConfirmBtnColor, PaperTextColor, 13, 32f);
                var cLe = confirmBtn.gameObject.AddComponent<LayoutElement>();
                cLe.minWidth = 92f;
                cLe.preferredWidth = 96f;
                confirmBtn.onClick.AddListener(() =>
                {
                    AppRoot.Instance?.FieldRouter?.ExecuteConfirmedAction(captured);
                    ClearPendingActions();
                    RefreshChatLog();
                    RefreshRecommendedChips();
                });

                var cancelBtn = CreateDynamicButton("CancelBtn", row.transform, "취소", DarkBtnColor, PaperTextColor, 13, 32f);
                var xLe = cancelBtn.gameObject.AddComponent<LayoutElement>();
                xLe.minWidth = 60f;
                xLe.preferredWidth = 64f;
                cancelBtn.onClick.AddListener(ClearPendingActions);

                _spawnedPendingRows.Add(row);
            }
        }

        private void ClearPendingActions()
        {
            foreach (var go in _spawnedPendingRows)
                if (go != null) Destroy(go);
            _spawnedPendingRows.Clear();
        }

        private void RefreshChatLog()
        {
            foreach (var go in _spawnedChatRows)
                if (go != null) Destroy(go);
            _spawnedChatRows.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            if (state == null || chatContentRoot == null) return;

            foreach (var msg in state.FieldCommandLogs)
            {
                Color bg = msg.isSystemAlert ? AlertBubbleColor : (msg.isPlayer ? PlayerBubbleColor : FieldBubbleColor);
                var box = new GameObject("ChatRow", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                box.transform.SetParent(chatContentRoot, false);
                box.GetComponent<Image>().color = bg;

                var vlg = box.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(12, 12, 8, 8);
                vlg.spacing = 4f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var header = CreateTmp("Sender", box.transform, $"<b>{msg.sender}</b>  <size=80%>[{msg.timestamp}]</size>", 13, msg.isPlayer ? InkColor : AccentColor);
                header.richText = true;
                var body = CreateTmp("Body", box.transform, msg.body, 14, InkColor);
                body.richText = true;
                body.lineSpacing = 4f;

                _spawnedChatRows.Add(box);
            }

            if (chatScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                if (chatScrollRect.content != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(chatScrollRect.content);
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void SetBusy(bool busy, string status)
        {
            _isBusy = busy;
            if (sendCommandButton != null) sendCommandButton.interactable = !busy;
            if (askOpinionButton != null) askOpinionButton.interactable = !busy;
            if (waitingStatusText != null) waitingStatusText.text = status;
        }

        private TMP_Text CreateTmp(string objName, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) tmp.font = uiFont;
            else if (providerStatusText != null && providerStatusText.font != null) tmp.font = providerStatusText.font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private Button CreateDynamicButton(string objName, Transform parent, string label, Color bg, Color fg, float fontSize, float height)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            var txt = CreateTmp("Label", go.transform, label, fontSize, fg);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            var rt = txt.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6, 2);
            rt.offsetMax = new Vector2(-6, -2);
            return go.GetComponent<Button>();
        }

        private static string Shorten(string s, int maxLen)
            => string.IsNullOrEmpty(s) || s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
    }
}

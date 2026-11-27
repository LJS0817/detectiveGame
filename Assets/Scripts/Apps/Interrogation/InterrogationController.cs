using System;
using System.Collections.Generic;
using System.Threading;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.Interrogation
{
    /// <summary>
    /// OSWindow를 상속하는 사건 관련자 전원 심문 앱 창.
    /// EP01 기준 관련자 7인 전원(동일 45분/8턴) + 불가 예외(박성훈 의식불명)를 한 목록에 표시하며,
    /// 평정심 게이지, 감정 연동 초상화, 증거판 카드 첨부 제시(최대 2장), 심문 진술 원클릭 증거판 등록을 제공합니다.
    /// </summary>
    public class InterrogationController : OSWindow
    {
        [Header("Left Roster Sidebar")]
        [SerializeField] private Transform rosterListContainer;

        [Header("Top Suspect Profile & Composure HUD")]
        [SerializeField] private RawImage portraitImage;
        [SerializeField] private Image portraitFallbackBg;
        [SerializeField] private TMP_Text portraitInitialText;
        [SerializeField] private TMP_Text suspectNameText;
        [SerializeField] private TMP_Text suspectRoleText;
        [SerializeField] private TMP_Text emotionBadgeText;
        [SerializeField] private Slider composureSlider;
        [SerializeField] private TMP_Text composureValueText;
        [SerializeField] private TMP_Text sessionStatusText;
        [SerializeField] private TMP_Text unlockedBadgesText;
        [SerializeField] private Button startSessionButton;

        [Header("Chat History")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private Transform chatContentRoot;
        [SerializeField] private TMP_Text waitingStatusText;

        [Header("Bottom Question & Evidence Presentation")]
        [SerializeField] private TMP_Dropdown evidenceDropdown;
        [SerializeField] private Button attachCardButton;
        [SerializeField] private Button clearAttachButton;
        [SerializeField] private TMP_Text attachedCardsText;
        [SerializeField] private TMP_InputField questionInputField;
        [SerializeField] private Button sendQuestionButton;

        [Header("Styling")]
        [SerializeField] private TMP_FontAsset uiFont;

        private static readonly Color PlayerBubbleColor = new Color(0.894f, 0.867f, 0.816f, 1f);  // #E4DDD0
        private static readonly Color SuspectBubbleColor = new Color(0.980f, 0.973f, 0.953f, 1f); // #FAF8F3
        private static readonly Color SystemBubbleColor = new Color(0.915f, 0.885f, 0.785f, 1f);  // #E9E2C8
        private static readonly Color InkColor = new Color(0.110f, 0.106f, 0.098f, 1f);
        private static readonly Color AccentColor = new Color(0.706f, 0.525f, 0.141f, 1f);
        private static readonly Color WarningColor = new Color(0.510f, 0.141f, 0.141f, 1f);
        private static readonly Color DarkBtnColor = new Color(0.180f, 0.176f, 0.169f, 1f);
        private static readonly Color ActiveRowColor = new Color(0.706f, 0.525f, 0.141f, 1f);
        private static readonly Color LockedRowColor = new Color(0.320f, 0.310f, 0.300f, 1f);
        private static readonly Color PaperTextColor = new Color(0.965f, 0.953f, 0.925f, 1f);

        private readonly List<GameObject> _rosterRows = new List<GameObject>();
        private readonly List<GameObject> _chatRows = new List<GameObject>();
        private readonly List<UserClueCard> _selectableCards = new List<UserClueCard>();
        private readonly List<UserClueCard> _attachedCards = new List<UserClueCard>();

        private InterrogationProfileData _selectedProfile;
        private Texture2D _loadedPortraitTex;
        private CancellationTokenSource _turnCts;
        private bool _isBusy;

        protected override void Awake()
        {
            base.Awake();
            if (startSessionButton != null) startSessionButton.onClick.AddListener(HandleStartSessionClicked);
            if (sendQuestionButton != null) sendQuestionButton.onClick.AddListener(HandleSendQuestionClicked);
            if (questionInputField != null) questionInputField.onSubmit.AddListener(_ => HandleSendQuestionClicked());
            if (attachCardButton != null) attachCardButton.onClick.AddListener(HandleAttachSelectedCard);
            if (clearAttachButton != null) clearAttachButton.onClick.AddListener(ClearAttachedCards);
        }

        private void OnDestroy()
        {
            _turnCts?.Cancel();
            _turnCts?.Dispose();
            if (_loadedPortraitTex != null) Destroy(_loadedPortraitTex);
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded += HandleClueChanged;
            GameEvents.OnClueCardUpdated += HandleClueChanged;
            GameEvents.OnInterrogationSessionChanged += HandleSessionChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded -= HandleClueChanged;
            GameEvents.OnClueCardUpdated -= HandleClueChanged;
            GameEvents.OnInterrogationSessionChanged -= HandleSessionChanged;
        }

        protected override void OnWindowFocused()
        {
            RefreshEvidenceDropdown();
            RefreshSelectedProfileHeader();
        }

        private void HandleEpisodeLoaded(EpisodeData _)
        {
            _selectedProfile = null;
            _attachedCards.Clear();
            RefreshAll();
        }

        private void HandleClueChanged(UserClueCard _) => RefreshEvidenceDropdown();

        private void HandleSessionChanged(string suspectId)
        {
            RefreshRoster();
            if (_selectedProfile != null && _selectedProfile.suspectId == suspectId)
            {
                RefreshSelectedProfileHeader();
                RefreshChatLog();
            }
        }

        public void RefreshAll()
        {
            var profiles = AppRoot.Instance?.RuntimeState?.Episode?.interrogationProfiles;
            if (_selectedProfile == null && profiles != null && profiles.Count > 0)
                _selectedProfile = profiles[0];

            RefreshRoster();
            RefreshEvidenceDropdown();
            RefreshAttachedLabel();
            RefreshSelectedProfileHeader();
            RefreshChatLog();
            SetBusy(false, string.Empty);
        }

        private void RefreshRoster()
        {
            foreach (var go in _rosterRows)
                if (go != null) Destroy(go);
            _rosterRows.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            var profiles = state?.Episode?.interrogationProfiles;
            if (profiles == null || rosterListContainer == null) return;

            foreach (var prof in profiles)
            {
                if (prof == null) continue;
                var captured = prof;
                var st = state.GetOrCreateInterrogationState(captured);
                bool isSelected = _selectedProfile == captured;

                Color bg = !captured.isInterrogatable
                    ? LockedRowColor
                    : (isSelected ? ActiveRowColor : DarkBtnColor);

                string tag = !captured.isInterrogatable
                    ? "[불가]"
                    : (st.isSessionActive ? $"[{st.turnsRemainingInSession}턴]" : (st.sessionsUsed > 0 ? $"[{st.sessionsUsed}회]" : ""));

                string label = $"<b>{captured.displayName} ({captured.age})</b> {tag}\n<size=78%>{captured.roleLabel}</size>";

                var btn = CreateDynamicButton($"Roster_{captured.suspectId}", rosterListContainer, label, bg, PaperTextColor, 13, 52f);
                btn.onClick.AddListener(() =>
                {
                    if (_isBusy) return;
                    GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
                    _selectedProfile = captured;
                    _attachedCards.Clear();
                    RefreshRoster();
                    RefreshAttachedLabel();
                    RefreshSelectedProfileHeader();
                    RefreshChatLog();
                });
                _rosterRows.Add(btn.gameObject);
            }
        }

        private void RefreshSelectedProfileHeader()
        {
            var state = AppRoot.Instance?.RuntimeState;
            var ep = state?.Episode;
            if (_selectedProfile == null || state == null || ep == null) return;

            var st = state.GetOrCreateInterrogationState(_selectedProfile);
            if (suspectNameText != null)
                suspectNameText.text = $"{_selectedProfile.displayName} ({_selectedProfile.age}세)";
            if (suspectRoleText != null)
                suspectRoleText.text = _selectedProfile.isInterrogatable
                    ? _selectedProfile.roleLabel
                    : $"{_selectedProfile.roleLabel} — {_selectedProfile.unavailableReason}";

            if (emotionBadgeText != null)
                emotionBadgeText.text = _selectedProfile.isInterrogatable ? $"심리 상태: {FormatEmotionKorean(st.lastEmotion)}" : "심문 불가";

            if (composureSlider != null)
            {
                composureSlider.minValue = 0f;
                composureSlider.maxValue = 100f;
                composureSlider.SetValueWithoutNotify(_selectedProfile.isInterrogatable ? st.composure : 0f);
            }
            if (composureValueText != null)
                composureValueText.text = _selectedProfile.isInterrogatable ? $"평정심 {st.composure}/100" : "-";

            if (unlockedBadgesText != null)
            {
                var badges = new List<string>();
                if (_selectedProfile.layers != null)
                {
                    foreach (var l in _selectedProfile.layers)
                    {
                        if (l != null && !l.isInitial && st.HasLayer(l.layerId) && !string.IsNullOrEmpty(l.layerBadgeLabel))
                            badges.Add($"[{l.layerBadgeLabel}]");
                    }
                }
                unlockedBadgesText.text = badges.Count > 0 ? string.Join(" ", badges) : "확인된 모순·추가 진술 없음";
            }

            if (sessionStatusText != null)
            {
                if (!_selectedProfile.isInterrogatable)
                    sessionStatusText.text = "소환 불가";
                else if (st.isSessionActive)
                    sessionStatusText.text = $"제{st.sessionsUsed}차 심문 진행 중 — 남은 질문 {st.turnsRemainingInSession}/{ep.interrogationTurnsPerSession}턴";
                else
                    sessionStatusText.text = $"대기 중 (소환 시 -{ep.interrogationSessionMinutes}분 / {ep.interrogationTurnsPerSession}턴)";
            }

            if (startSessionButton != null)
            {
                bool canStart = _selectedProfile.isInterrogatable && !st.isSessionActive && !_isBusy;
                startSessionButton.interactable = canStart;
                var lbl = startSessionButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                {
                    lbl.text = !_selectedProfile.isInterrogatable
                        ? "소환 불가"
                        : (st.isSessionActive ? "심문 진행 중" : $"소환 심문 (-{ep.interrogationSessionMinutes}분)");
                }
            }

            bool canAsk = _selectedProfile.isInterrogatable && st.isSessionActive && st.turnsRemainingInSession > 0 && !_isBusy;
            if (sendQuestionButton != null) sendQuestionButton.interactable = canAsk;
            if (questionInputField != null) questionInputField.interactable = canAsk;

            LoadPortraitVisualAsync(_selectedProfile, st.lastEmotion).Forget();
        }

        private async UniTaskVoid LoadPortraitVisualAsync(InterrogationProfileData profile, string emotion)
        {
            if (portraitInitialText != null)
                portraitInitialText.text = string.IsNullOrEmpty(profile.displayName) ? "?" : profile.displayName.Substring(0, 1);

            if (portraitFallbackBg != null)
                portraitFallbackBg.color = GetEmotionBackdropColor(emotion, profile.isInterrogatable);

            if (portraitImage == null) return;
            string epId = AppRoot.Instance?.RuntimeState?.Episode?.episodeId ?? "EP01";
            string relPath = $"{epId}/{profile.portraitBaseKey}.png";
            var tex = await EpisodeLoader.LoadTextureAsync(relPath);
            if (_selectedProfile != profile)
            {
                if (tex != null) Destroy(tex);
                return;
            }

            if (_loadedPortraitTex != null) Destroy(_loadedPortraitTex);
            _loadedPortraitTex = tex;
            portraitImage.texture = _loadedPortraitTex;
            portraitImage.gameObject.SetActive(_loadedPortraitTex != null);
            if (portraitInitialText != null)
                portraitInitialText.gameObject.SetActive(_loadedPortraitTex == null);
        }

        private void HandleStartSessionClicked()
        {
            if (_selectedProfile == null || AppRoot.Instance?.Interrogation == null) return;
            if (!AppRoot.Instance.Interrogation.TryStartSession(_selectedProfile, out string reason))
            {
                GameEvents.RaiseToastRequested("심문 불가", reason);
                return;
            }
            RefreshSelectedProfileHeader();
            RefreshChatLog();
        }

        private void RefreshEvidenceDropdown()
        {
            var state = AppRoot.Instance?.RuntimeState;
            if (state == null || evidenceDropdown == null) return;

            _selectableCards.Clear();
            _selectableCards.AddRange(state.GetActiveClueCards());

            var labels = new List<string> { "-- 제시할 증거판 카드 선택 --" };
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
                GameEvents.RaiseToastRequested("증거 제시 제한", "한 질문에 최대 2장의 증거 카드를 제시할 수 있습니다.");
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
                attachedCardsText.text = "제시할 증거 없음 (증거 없이 추궁 가능)";
                return;
            }

            var titles = new List<string>();
            foreach (var c in _attachedCards) titles.Add(c.displayTitle);
            attachedCardsText.text = $"제시 증거({_attachedCards.Count}/2): {string.Join(" / ", titles)}";
        }

        private void HandleSendQuestionClicked()
        {
            if (_isBusy || _selectedProfile == null) return;
            string q = questionInputField != null ? questionInputField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(q) && _attachedCards.Count == 0) return;

            if (questionInputField != null) questionInputField.text = string.Empty;
            SendQuestionAsync(q).Forget();
        }

        private async UniTaskVoid SendQuestionAsync(string questionText)
        {
            var ism = AppRoot.Instance?.Interrogation;
            if (ism == null || _selectedProfile == null) return;

            _turnCts?.Cancel();
            _turnCts?.Dispose();
            _turnCts = new CancellationTokenSource();

            var profile = _selectedProfile;
            var attachedCopy = new List<UserClueCard>(_attachedCards);
            _attachedCards.Clear();
            RefreshAttachedLabel();

            SetBusy(true, $"{profile.displayName}이(가) 시선을 피하며 답변을 고르고 있습니다...");

            try
            {
                var res = await ism.SendQuestionAsync(profile, questionText, attachedCopy, _turnCts.Token);
                if (!res.success && !string.IsNullOrEmpty(res.errorMessage))
                {
                    GameEvents.RaiseToastRequested("심문 안내", res.errorMessage);
                }
                else if (res.newlyUnlockedLayers.Count > 0)
                {
                    var badge = res.newlyUnlockedLayers[res.newlyUnlockedLayers.Count - 1].layerBadgeLabel;
                    if (!string.IsNullOrEmpty(badge))
                        GameEvents.RaiseToastRequested("심문 돌파!", $"{profile.displayName} — {badge}");
                }

                RefreshSelectedProfileHeader();
                RefreshChatLog();
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            finally
            {
                SetBusy(false, string.Empty);
                RefreshSelectedProfileHeader();
            }
        }

        private void RefreshChatLog()
        {
            foreach (var go in _chatRows)
                if (go != null) Destroy(go);
            _chatRows.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            if (state == null || _selectedProfile == null || chatContentRoot == null) return;

            var st = state.GetOrCreateInterrogationState(_selectedProfile);
            if (st.chatHistory.Count == 0)
            {
                string emptyMsg = _selectedProfile.isInterrogatable
                    ? $"[{_selectedProfile.displayName}] 아직 소환 심문 기록이 없습니다. 상단 [소환 심문]을 눌러 조사를 시작하십시오."
                    : $"[{_selectedProfile.displayName}] {_selectedProfile.unavailableReason}";
                SpawnChatBubble(new InterrogationChatTurn { role = "system", sender = "조사실 안내", body = emptyMsg, timestamp = "--:--" });
            }
            else
            {
                foreach (var turn in st.chatHistory)
                    SpawnChatBubble(turn);
            }

            if (chatScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                if (chatScrollRect.content != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(chatScrollRect.content);
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void SpawnChatBubble(InterrogationChatTurn turn)
        {
            Color bg = turn.role == "system"
                ? SystemBubbleColor
                : (turn.role == "player" ? PlayerBubbleColor : SuspectBubbleColor);

            var box = new GameObject("TurnBubble", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
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

            string emoTag = turn.role == "suspect" ? $" [{FormatEmotionKorean(turn.emotion)}]" : string.Empty;
            var header = CreateTmp("Header", box.transform, $"<b>{turn.sender}</b>{emoTag}  <size=78%>{turn.timestamp}</size>", 13, turn.role == "suspect" ? WarningColor : AccentColor);
            header.richText = true;

            if (turn.attachedCardTitles != null && turn.attachedCardTitles.Count > 0)
            {
                var att = CreateTmp("Attached", box.transform, $"[제시 증거] {string.Join(" + ", turn.attachedCardTitles)}", 12, AccentColor);
                att.fontStyle = FontStyles.Bold;
            }

            var body = CreateTmp("Body", box.transform, turn.body, 14, InkColor);
            body.lineSpacing = 4f;

            // 핵심 진술 발췌 버튼 (증거판 등록)
            if (turn.role == "suspect" && (!string.IsNullOrEmpty(turn.extractableStatement) || !string.IsNullOrEmpty(turn.rewardEvidenceId)))
            {
                string summary = string.IsNullOrEmpty(turn.extractableStatement) ? turn.body : turn.extractableStatement;
                string evId = string.IsNullOrEmpty(turn.rewardEvidenceId) ? "NONE" : turn.rewardEvidenceId;
                string suspectName = _selectedProfile != null ? _selectedProfile.displayName : turn.sender;
                string suspectId = _selectedProfile != null ? _selectedProfile.suspectId : "INTERROGATION";

                var addBtn = CreateDynamicButton("ExtractToBoardBtn", box.transform, "+ 이 심문 진술을 증거판에 등록", DarkBtnColor, PaperTextColor, 12, 30f);
                addBtn.onClick.AddListener(() =>
                {
                    var card = UserClueCard.Create(
                        sourceId: suspectId,
                        sourceType: ClueSourceType.InterrogationStatement,
                        displayTitle: $"[심문 진술] {suspectName}",
                        hiddenEvidenceId: evId,
                        extractedText: summary);
                    AppRoot.Instance?.RuntimeState?.AddClueCard(card);
                    addBtn.interactable = false;
                    var lbl = addBtn.GetComponentInChildren<TMP_Text>();
                    if (lbl != null) lbl.text = "증거판에 등록됨";
                });
            }

            _chatRows.Add(box);
        }

        private void SetBusy(bool busy, string status)
        {
            _isBusy = busy;
            if (waitingStatusText != null) waitingStatusText.text = status;
            if (sendQuestionButton != null && busy) sendQuestionButton.interactable = false;
            if (startSessionButton != null && busy) startSessionButton.interactable = false;
        }

        private static string FormatEmotionKorean(string emotion)
        {
            return (emotion ?? "calm").ToLowerInvariant() switch
            {
                "calm" => "평온",
                "annoyed" => "불쾌·격앙",
                "nervous" => "동요·긴장",
                "panicked" => "당황·흔들림",
                "broken" => "붕괴·자백",
                "cooperative" => "협조적",
                _ => "평온"
            };
        }

        private static Color GetEmotionBackdropColor(string emotion, bool interrogatable)
        {
            if (!interrogatable) return LockedRowColor;
            return (emotion ?? "calm").ToLowerInvariant() switch
            {
                "broken" => new Color(0.51f, 0.14f, 0.14f, 1f),
                "panicked" => new Color(0.65f, 0.28f, 0.16f, 1f),
                "nervous" => new Color(0.71f, 0.53f, 0.14f, 1f),
                "cooperative" => new Color(0.20f, 0.42f, 0.30f, 1f),
                _ => DarkBtnColor
            };
        }

        private TMP_Text CreateTmp(string objName, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) tmp.font = uiFont;
            else if (suspectNameText != null && suspectNameText.font != null) tmp.font = suspectNameText.font;
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
            txt.richText = true;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            var rt = txt.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8, 4);
            rt.offsetMax = new Vector2(-8, -4);
            return go.GetComponent<Button>();
        }
    }
}

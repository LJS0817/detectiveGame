using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ColdCaseOS.Data;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ColdCaseOS.Core.LLM
{
    /// <summary>
    /// 현장수사반(강민호 경사) LLM Tool Calling 라우터 및 결정론적 실행 게이트.
    /// - LLM에는 미완료 액션 ID와 현재 해금된 문서 제목만 전달(미해금 사실 누설 0%).
    /// - 반환된 actionId는 Unity C# 코드가 유효성·완료 여부·영장 근거(requiredEvidenceIds)를 최종 검증합니다.
    /// </summary>
    public class FieldCommandRouter
    {
        private const string FieldCommandSchemaJson = @"{
          ""type"": ""object"",
          ""properties"": {
            ""replyDialogue"": { ""type"": ""string"" },
            ""matchedActionIds"": {
              ""type"": ""array"",
              ""items"": { ""type"": ""string"" }
            },
            ""isHypothesisCheck"": { ""type"": ""boolean"" }
          },
          ""required"": [""replyDialogue"", ""matchedActionIds"", ""isHypothesisCheck""],
          ""additionalProperties"": false
        }";

        private readonly Func<EpisodeRuntimeState> _getState;
        private readonly Func<TimeManager> _getTime;
        private readonly Func<ILlmProvider> _getProvider;

        public FieldCommandRouter(
            Func<EpisodeRuntimeState> getState,
            Func<TimeManager> getTime,
            Func<ILlmProvider> getProvider)
        {
            _getState = getState;
            _getTime = getTime;
            _getProvider = getProvider;
        }

        public async UniTask<FieldCommandRouteResult> RouteCommandAsync(
            string playerInput,
            List<UserClueCard> attachedCards,
            CancellationToken ct = default)
        {
            var result = new FieldCommandRouteResult();
            var state = _getState?.Invoke();
            var episode = state?.Episode;
            var provider = _getProvider?.Invoke();

            if (state == null || episode == null || provider == null)
            {
                result.errorMessage = "에피소드가 로드되지 않았습니다.";
                result.replyDialogue = "팀장님, 현재 사건 데이터를 확인할 수 없습니다.";
                return result;
            }

            string cleanInput = (playerInput ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(cleanInput))
            {
                result.replyDialogue = "팀장님, 지시하실 내용을 입력해 주십시오.";
                return result;
            }

            // 1. 플레이어 메시지 기록
            string nowClock = _getTime?.Invoke()?.FormattedCurrentClock ?? "09:00";
            string attachedSummary = BuildAttachedSummary(attachedCards);
            state.FieldCommandLogs.Add(new FieldCommandMessageData
            {
                sender = "전담팀장 (나)",
                body = string.IsNullOrEmpty(attachedSummary) ? cleanInput : $"{cleanInput}\n<size=85%>[첨부: {attachedSummary}]</size>",
                timestamp = nowClock,
                isPlayer = true
            });

            // 2. LLM 요청 조립 (미완료 액션 + 해금 문서 목록만 컨텍스트 주입)
            var req = BuildRequest(state, cleanInput, attachedCards);

            FieldCommandLlmResponse llmResp;
            try
            {
                string rawJson = await provider.CompleteJsonAsync(req, ct);
                llmResp = JsonConvert.DeserializeObject<FieldCommandLlmResponse>(rawJson);
                result.usedMockFallback = !provider.IsLiveApi;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FieldCommandRouter] Live API 실패, Mock 폴백 사용: {ex.Message}");
                result.errorMessage = ex.Message;
                result.usedMockFallback = true;
                var mock = new MockLlmProvider(state);
                string fallbackJson = await mock.CompleteJsonAsync(req, ct);
                llmResp = JsonConvert.DeserializeObject<FieldCommandLlmResponse>(fallbackJson);
            }

            if (llmResp == null)
            {
                result.replyDialogue = "팀장님, 통신이 잠시 불안정했습니다. 다시 한 번 지시해 주십시오.";
                return result;
            }

            result.replyDialogue = llmResp.replyDialogue ?? string.Empty;
            result.isHypothesisCheck = llmResp.isHypothesisCheck;

            // 3. Unity C# 결정론적 검증 (환각 ID 제거 + 완료 액션 제거 + 영장 근거 체크)
            var attachedEvIds = CollectEvidenceIds(attachedCards);
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (llmResp.matchedActionIds != null)
            {
                foreach (var aid in llmResp.matchedActionIds)
                {
                    if (string.IsNullOrWhiteSpace(aid) || !seenIds.Add(aid)) continue;
                    var act = episode.actions.Find(a => string.Equals(a.actionId, aid, StringComparison.OrdinalIgnoreCase));
                    if (act == null || state.IsActionCompleted(act.actionId)) continue;

                    if (MeetsRequiredEvidence(act, state, attachedEvIds))
                    {
                        result.confirmableActions.Add(act);
                    }
                    else
                    {
                        string rejectMsg = string.IsNullOrEmpty(act.rejectFeedback)
                            ? $"팀장님, [{act.title}] 집행을 위해서는 관련 소명 증거가 먼저 필요합니다."
                            : act.rejectFeedback;
                        result.rejectedMessages.Add(rejectMsg);
                    }
                }
            }

            // 거절된 영장이 있고 확정 가능한 액션이 없으면 강민호 대사를 거절 사유로 대체
            if (result.confirmableActions.Count == 0 && result.rejectedMessages.Count > 0)
            {
                result.replyDialogue = string.Join("\n", result.rejectedMessages);
            }

            state.FieldCommandLogs.Add(new FieldCommandMessageData
            {
                sender = "강민호 경사 (현장반장)",
                body = result.replyDialogue,
                timestamp = nowClock,
                isPlayer = false
            });

            return result;
        }

        /// <summary>
        /// 플레이어가 [지시 확정: 예상 소요 OO분] 버튼을 클릭했을 때 결정론적으로 시간 차감 및 보상을 해금합니다.
        /// </summary>
        public bool ExecuteConfirmedAction(FieldActionData action)
        {
            var state = _getState?.Invoke();
            var time = _getTime?.Invoke();
            if (state?.Episode == null || time == null || action == null) return false;
            if (state.IsActionCompleted(action.actionId)) return false;

            var breakdown = time.CalculateBreakdown(0, action);
            time.AdvanceMinutes(breakdown.TotalMinutes);

            if (action.rewardDocIds != null)
            {
                foreach (var docId in action.rewardDocIds)
                    state.UnlockDocument(docId);
            }

            if (action.rewardPhotoIds != null)
            {
                foreach (var photoId in action.rewardPhotoIds)
                    state.UnlockPhoto(photoId);
            }

            if (action.rewardCctvIds != null)
            {
                foreach (var cctvId in action.rewardCctvIds)
                    state.UnlockCctv(cctvId);
            }

            // 휘발성 단서(예: EP01 NVR 포렌식 원본 IP) 골든 타임 판정
            if (!string.IsNullOrEmpty(action.volatileBonusDocId))
            {
                state.CurrentVolatileClueState = time.IsWithinVolatileWindow
                    ? VolatileClueState.Complete
                    : VolatileClueState.Damaged;
            }

            state.MarkActionCompleted(action.actionId);

            string reportText = string.IsNullOrEmpty(action.partnerReportMessage)
                ? $"팀장님, [{action.title}] 완료했습니다. 확보한 자료를 파일 탐색기에 올려뒀습니다."
                : action.partnerReportMessage;

            state.FieldCommandLogs.Add(new FieldCommandMessageData
            {
                sender = "강민호 경사 (현장반장)",
                body = $"[완료 / +{TimeManager.FormatMinutesKorean(breakdown.TotalMinutes)}] {reportText}",
                timestamp = time.FormattedCurrentClock,
                isPlayer = false,
                isSystemAlert = true
            });

            GameEvents.RaiseSfxRequested("SFX_MSG_NOTIFY");
            GameEvents.RaiseToastRequested("현장수사반 보고 도착", $"{action.title} 완료 (+{TimeManager.FormatMinutesKorean(breakdown.TotalMinutes)})");
            return true;
        }

        public List<FieldActionData> GetRecommendedActions(int maxCount = 3)
        {
            var list = new List<FieldActionData>();
            var state = _getState?.Invoke();
            if (state?.Episode?.actions == null) return list;

            foreach (var act in state.Episode.actions)
            {
                if (act != null && act.isRecommendedChip && !state.IsActionCompleted(act.actionId))
                {
                    list.Add(act);
                    if (list.Count >= maxCount) return list;
                }
            }

            foreach (var act in state.Episode.actions)
            {
                if (act != null && !list.Contains(act) && !state.IsActionCompleted(act.actionId))
                {
                    list.Add(act);
                    if (list.Count >= maxCount) return list;
                }
            }

            return list;
        }

        private LlmChatRequest BuildRequest(EpisodeRuntimeState state, string userInput, List<UserClueCard> attachedCards)
        {
            var ep = state.Episode;
            var time = _getTime?.Invoke();
            var sb = new StringBuilder();
            sb.AppendLine("너는 서울청 중요사건 보완수사 전담팀 휘하 현장수사반 반장 '강민호 경사'(39세)다.");
            sb.AppendLine("사용자는 너의 상급자인 '전담팀장(경감)'이다. 항상 '팀장님'으로 호칭하고 현실적인 형사 존댓말(2~4문장)로 답하라.");
            sb.AppendLine("절대 규칙:");
            sb.AppendLine("1. 아래 [미완료 허용 액션 목록]에 있는 actionId만 matchedActionIds에 담아라. 목록에 없는 ID를 절대 지어내지 마라.");
            sb.AppendLine("2. 아직 해금되지 않은 문서 내용이나 진범 정체를 절대 스포일러하거나 지어내지 마라.");
            sb.AppendLine("3. 지구대 종결 민원(코인노래방·주취자 등)이나 의식불명 피해자 심문 등 사건과 무관하거나 불가능한 지시는 matchedActionIds를 빈 배열로 두고 0분 사전 필터링 안내를 하라.");
            sb.AppendLine("4. 가설 검증이나 의견 요청이면 isHypothesisCheck=true, matchedActionIds=[]로 두고 현재 확보된 문서 범위 안에서만 논리적 조언을 하라.");
            sb.AppendLine();
            sb.AppendLine($"[현재 시각] {time?.FormattedCurrentFull} / 남은 시한: {TimeManager.FormatMinutesKorean(Mathf.Max(0, time?.RemainingSubmissionMinutes ?? 0))}");
            sb.AppendLine("[현재 해금된 문서 제목]");
            foreach (var doc in ep.documents)
            {
                if (state.UnlockedDocIds.Contains(doc.docId))
                    sb.AppendLine($"- {doc.title}");
            }

            sb.AppendLine();
            sb.AppendLine("[미완료 허용 액션 목록 (actionId | 소요분 | 설명)]");
            foreach (var act in ep.actions)
            {
                if (act == null || state.IsActionCompleted(act.actionId)) continue;
                sb.AppendLine($"- {act.actionId} | {act.baseWorkMinutes}분 | {act.title} — {act.matchDescription}");
            }

            sb.AppendLine();
            sb.AppendLine("[이미 완료된 액션]");
            foreach (var act in ep.actions)
            {
                if (act != null && state.IsActionCompleted(act.actionId))
                    sb.AppendLine($"- {act.title} (완료)");
            }

            var messages = new List<LlmMessage> { new LlmMessage("system", sb.ToString()) };

            // 최근 대화 6개 문맥 유지
            int start = Math.Max(0, state.FieldCommandLogs.Count - 7);
            for (int i = start; i < state.FieldCommandLogs.Count - 1; i++)
            {
                var log = state.FieldCommandLogs[i];
                if (log == null) continue;
                messages.Add(new LlmMessage(log.isPlayer ? "user" : "assistant", log.body));
            }

            string userPrompt = userInput;
            string attachedDetail = BuildAttachedDetailForLlm(attachedCards);
            if (!string.IsNullOrEmpty(attachedDetail))
                userPrompt += $"\n\n[팀장이 첨부한 증거판 카드]\n{attachedDetail}";
            messages.Add(new LlmMessage("user", userPrompt));

            return new LlmChatRequest
            {
                kind = LlmRequestKind.FieldCommand,
                schemaName = "field_command_route",
                jsonSchemaRaw = FieldCommandSchemaJson,
                temperature = 0.25f,
                maxTokens = 450,
                messages = messages,
                mockUserText = userInput,
                mockAttachedEvidenceIds = new List<string>(CollectEvidenceIds(attachedCards))
            };
        }

        private static bool MeetsRequiredEvidence(FieldActionData act, EpisodeRuntimeState state, HashSet<string> attachedEvIds)
        {
            if (act.requiredEvidenceIds == null || act.requiredEvidenceIds.Count == 0)
                return true;

            foreach (var reqId in act.requiredEvidenceIds)
            {
                if (string.IsNullOrEmpty(reqId)) continue;
                if (attachedEvIds.Contains(reqId) || state.HasEvidenceIdInActiveCards(reqId))
                    return true;
            }
            return false;
        }

        private static HashSet<string> CollectEvidenceIds(List<UserClueCard> cards)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (cards == null) return set;
            foreach (var c in cards)
            {
                if (c != null && !c.isTooSmallCapture && !string.IsNullOrEmpty(c.hiddenEvidenceId) && c.hiddenEvidenceId != "NONE")
                    set.Add(c.hiddenEvidenceId);
            }
            return set;
        }

        private static string BuildAttachedSummary(List<UserClueCard> cards)
        {
            if (cards == null || cards.Count == 0) return string.Empty;
            var titles = new List<string>();
            foreach (var c in cards)
                if (c != null) titles.Add(c.displayTitle);
            return string.Join(", ", titles);
        }

        private static string BuildAttachedDetailForLlm(List<UserClueCard> cards)
        {
            if (cards == null || cards.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in cards)
            {
                if (c == null) continue;
                sb.AppendLine($"- {c.displayTitle}: {c.extractedText}");
            }
            return sb.ToString();
        }
    }
}

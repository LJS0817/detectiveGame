using System;
using System.Collections.Generic;
using System.Threading;
using ColdCaseOS.Data;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace ColdCaseOS.Core.LLM
{
    /// <summary>
    /// OpenAI API 키가 없거나 오프라인 환경일 때도 전체 현장 지휘·7인 전원 심문 루프를
    /// 100% 검증할 수 있도록 지원하는 결정론적 로컬 시뮬레이터.
    /// </summary>
    public class MockLlmProvider : ILlmProvider
    {
        private readonly EpisodeRuntimeState _state;

        public bool IsLiveApi => false;
        public string ProviderDisplayName => "Offline Mock (API 키 미설정)";

        public MockLlmProvider(EpisodeRuntimeState state)
        {
            _state = state;
        }

        public async UniTask<string> CompleteJsonAsync(LlmChatRequest request, CancellationToken ct = default)
        {
            // 실제 통신 감각을 유지하는 짧은 비동기 대기
            await UniTask.Delay(260, cancellationToken: ct);

            if (request.kind == LlmRequestKind.FieldCommand)
                return BuildMockFieldCommandJson(request);

            return BuildMockInterrogationJson(request);
        }

        private string BuildMockFieldCommandJson(LlmChatRequest req)
        {
            string query = (req.mockUserText ?? string.Empty).Trim();
            string lower = query.ToLowerInvariant();
            var episode = _state?.Episode;
            var resp = new FieldCommandLlmResponse();

            if (episode == null)
            {
                resp.replyDialogue = "팀장님, 현재 활성화된 사건 파일이 없습니다.";
                return JsonConvert.SerializeObject(resp);
            }

            // 1. 무관 노이즈 사전 필터링 (0분)
            if (ContainsAny(lower, "코인노래방", "노래방", "주취자", "고성방가", "병실", "박성훈 심문", "병원 면회"))
            {
                resp.isHypothesisCheck = true;
                resp.replyDialogue = "팀장님, 해당 건은 본 사건 시각·동선과 무관한 지구대 종결 민원이거나 중환자실 진술 불가 상태라 현장반 파견이 불필요합니다. 본 사건 핵심 단서부터 확인하시죠.";
                return JsonConvert.SerializeObject(resp);
            }

            // 2. 가설 검증 / 의견 묻기 (0분)
            if (ContainsAny(lower, "가설", "어떻게 생각", "정리해", "의견", "힌트", "누가 범인", "의심"))
            {
                resp.isHypothesisCheck = true;
                int unlockedCount = _state.UnlockedDocIds.Count;
                int totalCount = episode.documents.Count;
                resp.replyDialogue =
                    $"팀장님, 현재 확보된 문서는 {unlockedCount}/{totalCount}건입니다. " +
                    "초동팀은 김철수를 범인으로 봤지만 CCTV 02:14 창고 문틈 혈흔·피해자 호칭('사장님')·이동 시간표와 각 관련자 알리바이 로그를 교차 대조해 보시는 게 좋겠습니다. " +
                    "의심되는 장소나 관련자 이름을 말씀해 주시면 바로 현장반을 보내겠습니다.";
                return JsonConvert.SerializeObject(resp);
            }

            // 3. 미완료 액션 키워드 점수 매칭
            var scored = new List<(FieldActionData act, int score)>();
            foreach (var act in episode.actions)
            {
                if (act == null || _state.IsActionCompleted(act.actionId)) continue;
                int score = 0;

                if (!string.IsNullOrEmpty(act.title) && lower.Contains(act.title.ToLowerInvariant()))
                    score += 10;

                if (act.matchKeywords != null)
                {
                    foreach (var kw in act.matchKeywords)
                    {
                        if (!string.IsNullOrWhiteSpace(kw) && lower.Contains(kw.ToLowerInvariant()))
                            score += 3;
                    }
                }

                if (score > 0) scored.Add((act, score));
            }

            scored.Sort((a, b) => b.score.CompareTo(a.score));

            if (scored.Count > 0)
            {
                int take = Math.Min(2, scored.Count);
                for (int i = 0; i < take; i++)
                    resp.matchedActionIds.Add(scored[i].act.actionId);

                resp.replyDialogue = take == 1
                    ? $"알겠습니다, 팀장님. [{scored[0].act.title}] 진행할 준비가 됐습니다. 아래 확정 버튼을 눌러주시면 즉시 현장반이 움직이겠습니다."
                    : $"알겠습니다, 팀장님. 지시하신 내용과 일치하는 [{scored[0].act.title}] 등 {take}건을 확인했습니다. 확정하실 항목을 선택해 주십시오.";
            }
            else
            {
                resp.isHypothesisCheck = true;
                resp.replyDialogue = "팀장님, 지시하신 대상이 이미 확보된 자료이거나 구체적인 수색·조회 대상이 모호합니다. 확인하고 싶은 장소(창고 뒷문, NVR, 서랍, POS, 재고·퀵)나 관련자 이름(김철수·이준호·최태식·한소라·오민석·정대호·윤서진)을 짚어 주십시오.";
            }

            return JsonConvert.SerializeObject(resp);
        }

        private string BuildMockInterrogationJson(LlmChatRequest req)
        {
            var episode = _state?.Episode;
            var profile = episode?.interrogationProfiles?.Find(p => p.suspectId == req.mockSuspectId);
            var st = _state?.GetOrCreateInterrogationState(profile);

            if (profile == null || st == null)
            {
                return JsonConvert.SerializeObject(new InterrogationLlmResponse
                {
                    dialogue = "조사 대상 정보를 불러오지 못했습니다.",
                    emotion = "calm"
                });
            }

            // 1. 이번 턴에 새 레이어가 해금된 경우 해당 레이어 mockDialogue 반환
            if (!string.IsNullOrEmpty(req.mockNewlyUnlockedLayerId))
            {
                var layer = profile.layers.Find(l => l.layerId == req.mockNewlyUnlockedLayerId);
                if (layer != null && !string.IsNullOrEmpty(layer.mockDialogue))
                {
                    return JsonConvert.SerializeObject(new InterrogationLlmResponse
                    {
                        dialogue = layer.mockDialogue,
                        emotion = string.IsNullOrEmpty(layer.mockEmotion) ? "nervous" : layer.mockEmotion,
                        composureDelta = 0,
                        extractableStatement = layer.mockExtractableStatement ?? string.Empty
                    });
                }
            }

            // 2. 무관한 증거를 제시한 경우
            if (req.mockAttachedIrrelevantEvidence)
            {
                return JsonConvert.SerializeObject(new InterrogationLlmResponse
                {
                    dialogue = $"팀장님, 그 자료는 저와는 아무 상관이 없어 보이는데요. 정확한 근거를 가지고 말씀해 주시죠. ({profile.displayName}이(가) 여유를 되찾습니다.)",
                    emotion = "calm",
                    composureDelta = 0,
                    extractableStatement = string.Empty
                });
            }

            // 3. 이미 해금된 최상위 레이어 유지 + 일반 압박 질문 응답
            string q = (req.mockUserText ?? string.Empty).ToLowerInvariant();
            bool sharpQuestion = ContainsAny(q, "거짓말", "모순", "알리바이", "시간", "왜", "사실", "어디", "팔찌", "테이프", "소화기", "횡령", "신고");

            SecretLayerRule latestUnlocked = null;
            if (profile.layers != null)
            {
                foreach (var layer in profile.layers)
                {
                    if (st.HasLayer(layer.layerId))
                        latestUnlocked = layer;
                }
            }

            string baseLine = latestUnlocked != null && !string.IsNullOrEmpty(latestUnlocked.mockDialogue)
                ? latestUnlocked.mockDialogue
                : "제가 알고 있는 건 앞서 말씀드린 내용이 전부입니다.";

            string reply = sharpQuestion
                ? $"{baseLine} (증거 카드를 함께 첨부해 추궁하면 더 구체적인 모순을 파고들 수 있습니다.)"
                : baseLine;

            return JsonConvert.SerializeObject(new InterrogationLlmResponse
            {
                dialogue = reply,
                emotion = sharpQuestion ? "nervous" : (latestUnlocked?.mockEmotion ?? "calm"),
                composureDelta = sharpQuestion ? -6 : -2,
                extractableStatement = string.Empty
            });
        }

        private static bool ContainsAny(string text, params string[] words)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var w in words)
            {
                if (!string.IsNullOrEmpty(w) && text.Contains(w))
                    return true;
            }
            return false;
        }
    }
}

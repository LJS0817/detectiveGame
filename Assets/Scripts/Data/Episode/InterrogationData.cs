using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ColdCaseOS.Data
{
    /// <summary>
    /// 사건 관련자 전원 심문용 정적 프로필 DTO.
    /// 분 단위 불변 알리바이(immutableTimeline)와 코드 통제 비밀 레이어(layers)를 포함합니다.
    /// </summary>
    [Serializable]
    public class InterrogationProfileData
    {
        [JsonProperty("suspectId")] public string suspectId;
        [JsonProperty("displayName")] public string displayName;
        [JsonProperty("age")] public int age;
        [JsonProperty("roleLabel")] public string roleLabel;
        [JsonProperty("isInterrogatable")] public bool isInterrogatable = true;
        [JsonProperty("unavailableReason")] public string unavailableReason = string.Empty;
        [JsonProperty("portraitBaseKey")] public string portraitBaseKey = string.Empty;
        [JsonProperty("initialComposure")] public int initialComposure = 75;
        [JsonProperty("victimCallName")] public string victimCallName = string.Empty;
        [JsonProperty("personaPrompt")] public string personaPrompt = string.Empty;
        [JsonProperty("immutableTimeline")] public List<string> immutableTimeline = new List<string>();
        [JsonProperty("layers")] public List<SecretLayerRule> layers = new List<SecretLayerRule>();
    }

    /// <summary>
    /// Prompt Isolation(프롬프트 격리) 기반 비밀 레이어 해금 규칙.
    /// Unity C# 상태 머신이 해금하기 전까지 상위 레이어의 promptDirective는 LLM 요청 본문에 포함되지 않습니다.
    /// </summary>
    [Serializable]
    public class SecretLayerRule
    {
        [JsonProperty("layerId")] public string layerId;
        [JsonProperty("isInitial")] public bool isInitial;
        [JsonProperty("requiredEvidenceIds")] public List<string> requiredEvidenceIds = new List<string>();
        [JsonProperty("requireAllEvidence")] public bool requireAllEvidence; // false=OR, true=AND
        [JsonProperty("requiredPriorLayers")] public List<string> requiredPriorLayers = new List<string>();
        [JsonProperty("minUnlockedPriorCount")] public int minUnlockedPriorCount;
        [JsonProperty("maxComposureRequired")] public int maxComposureRequired = 100;
        [JsonProperty("composurePenaltyOnUnlock")] public int composurePenaltyOnUnlock;
        [JsonProperty("triggerKeywords")] public List<string> triggerKeywords = new List<string>(); // 선행 레이어 해금 후 질문 키워드 트리거(선택)
        [JsonProperty("promptDirective")] public string promptDirective = string.Empty;
        [JsonProperty("rewardEvidenceId")] public string rewardEvidenceId = string.Empty;
        [JsonProperty("rewardDocId")] public string rewardDocId = string.Empty;
        [JsonProperty("layerBadgeLabel")] public string layerBadgeLabel = string.Empty;
        [JsonProperty("mockDialogue")] public string mockDialogue = string.Empty;
        [JsonProperty("mockEmotion")] public string mockEmotion = "nervous";
        [JsonProperty("mockExtractableStatement")] public string mockExtractableStatement = string.Empty;
    }
}

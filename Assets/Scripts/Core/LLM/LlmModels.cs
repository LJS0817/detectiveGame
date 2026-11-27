using System;
using System.Collections.Generic;
using ColdCaseOS.Data;
using Newtonsoft.Json;

namespace ColdCaseOS.Core.LLM
{
    public enum LlmRequestKind
    {
        FieldCommand,
        Interrogation
    }

    [Serializable]
    public class LlmMessage
    {
        [JsonProperty("role")] public string role; // "system" | "user" | "assistant"
        [JsonProperty("content")] public string content;

        public LlmMessage() { }
        public LlmMessage(string role, string content)
        {
            this.role = role;
            this.content = content ?? string.Empty;
        }
    }

    public class LlmChatRequest
    {
        public LlmRequestKind kind;
        public string schemaName;
        public string jsonSchemaRaw;
        public float temperature = 0.4f;
        public int maxTokens = 512;
        public List<LlmMessage> messages = new List<LlmMessage>();

        // MockLlmProvider 결정론적 시뮬레이션용 메타데이터 (실제 OpenAI 페이로드에는 포함되지 않음)
        public string mockUserText = string.Empty;
        public string mockSuspectId = string.Empty;
        public string mockNewlyUnlockedLayerId = string.Empty;
        public bool mockAttachedIrrelevantEvidence;
        public List<string> mockAttachedEvidenceIds = new List<string>();
    }

    [Serializable]
    public class FieldCommandLlmResponse
    {
        [JsonProperty("replyDialogue")] public string replyDialogue = string.Empty;
        [JsonProperty("matchedActionIds")] public List<string> matchedActionIds = new List<string>();
        [JsonProperty("isHypothesisCheck")] public bool isHypothesisCheck;
    }

    [Serializable]
    public class InterrogationLlmResponse
    {
        [JsonProperty("dialogue")] public string dialogue = string.Empty;
        [JsonProperty("emotion")] public string emotion = "calm";
        [JsonProperty("composureDelta")] public int composureDelta;
        [JsonProperty("extractableStatement")] public string extractableStatement = string.Empty;
    }

    public class FieldCommandRouteResult
    {
        public string replyDialogue = string.Empty;
        public List<FieldActionData> confirmableActions = new List<FieldActionData>();
        public List<string> rejectedMessages = new List<string>();
        public bool isHypothesisCheck;
        public bool usedMockFallback;
        public string errorMessage = string.Empty;
    }

    public class InterrogationTurnResult
    {
        public bool success;
        public string dialogue = string.Empty;
        public string emotion = "calm";
        public int composureBefore;
        public int composureAfter;
        public List<SecretLayerRule> newlyUnlockedLayers = new List<SecretLayerRule>();
        public string rewardEvidenceId = string.Empty;
        public string unlockedDocId = string.Empty;
        public string extractableStatement = string.Empty;
        public bool usedMockFallback;
        public string errorMessage = string.Empty;
    }
}

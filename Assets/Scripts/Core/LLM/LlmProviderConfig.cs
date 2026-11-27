using System;
using System.IO;
using UnityEngine;

namespace ColdCaseOS.Core.LLM
{
    /// <summary>
    /// OpenAI API 키·모델·엔드포인트 설정을 안전하게 관리합니다.
    /// 소스 코드나 Git에 키를 저장하지 않고, 환경변수(OPENAI_API_KEY) > PlayerPrefs(설정 모달 BYOK) > persistentDataPath 로컬 파일 순으로 로드합니다.
    /// </summary>
    public class LlmProviderConfig
    {
        public const string PrefApiKey = "KPOL_OPENAI_API_KEY";
        public const string PrefModel = "KPOL_OPENAI_MODEL";
        public const string PrefBaseUrl = "KPOL_OPENAI_BASE_URL";

        public const string DefaultModel = "gpt-4o-mini";
        public const string DefaultBaseUrl = "https://api.openai.com/v1/chat/completions";

        public string ApiKey { get; private set; } = string.Empty;
        public string Model { get; private set; } = DefaultModel;
        public string EndpointUrl { get; private set; } = DefaultBaseUrl;
        public int TimeoutSeconds { get; set; } = 25;

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        public LlmProviderConfig()
        {
            Reload();
        }

        public void Reload()
        {
            ApiKey = ResolveApiKey();
            Model = ResolveSetting("OPENAI_MODEL", PrefModel, DefaultModel);
            EndpointUrl = ResolveSetting("OPENAI_BASE_URL", PrefBaseUrl, DefaultBaseUrl);
        }

        public void SaveUserApiKey(string apiKey, string modelOverride = null)
        {
            string trimmedKey = (apiKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedKey))
                PlayerPrefs.DeleteKey(PrefApiKey);
            else
                PlayerPrefs.SetString(PrefApiKey, trimmedKey);

            if (!string.IsNullOrWhiteSpace(modelOverride))
                PlayerPrefs.SetString(PrefModel, modelOverride.Trim());

            PlayerPrefs.Save();
            Reload();
        }

        public ILlmProvider CreateActiveProvider(EpisodeRuntimeState state)
        {
            Reload();
            if (HasApiKey)
                return new OpenAiLlmProvider(this);

            return new MockLlmProvider(state);
        }

        private static string ResolveApiKey()
        {
            string envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
                return envKey.Trim();

            string prefKey = PlayerPrefs.GetString(PrefApiKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(prefKey))
                return prefKey.Trim();

            try
            {
                string keyFile = Path.Combine(Application.persistentDataPath, "openai_key.txt");
                if (File.Exists(keyFile))
                {
                    string fileKey = File.ReadAllText(keyFile).Trim();
                    if (!string.IsNullOrWhiteSpace(fileKey))
                        return fileKey;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LlmProviderConfig] 로컬 키 파일 읽기 실패: {ex.Message}");
            }

            return string.Empty;
        }

        private static string ResolveSetting(string envVar, string prefKey, string fallback)
        {
            string envVal = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(envVal))
                return envVal.Trim();

            string prefVal = PlayerPrefs.GetString(prefKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(prefVal))
                return prefVal.Trim();

            return fallback;
        }
    }
}

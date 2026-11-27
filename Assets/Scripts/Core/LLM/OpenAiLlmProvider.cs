using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ColdCaseOS.Core.LLM
{
    /// <summary>
    /// OpenAI Chat Completions API (Structured Outputs: response_format.json_schema) 프로바이더.
    /// </summary>
    public class OpenAiLlmProvider : ILlmProvider
    {
        private readonly LlmProviderConfig _config;

        public bool IsLiveApi => true;
        public string ProviderDisplayName => $"OpenAI ({_config.Model})";

        public OpenAiLlmProvider(LlmProviderConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async UniTask<string> CompleteJsonAsync(LlmChatRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!_config.HasApiKey)
                throw new InvalidOperationException("OpenAI API 키가 설정되지 않았습니다.");

            var msgArray = new JArray();
            foreach (var m in request.messages)
            {
                if (m == null) continue;
                msgArray.Add(new JObject
                {
                    ["role"] = m.role,
                    ["content"] = m.content ?? string.Empty
                });
            }

            var payload = new JObject
            {
                ["model"] = _config.Model,
                ["messages"] = msgArray,
                ["temperature"] = request.temperature,
                ["max_tokens"] = request.maxTokens
            };

            if (!string.IsNullOrWhiteSpace(request.jsonSchemaRaw))
            {
                payload["response_format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JObject
                    {
                        ["name"] = string.IsNullOrEmpty(request.schemaName) ? "kpol_response" : request.schemaName,
                        ["strict"] = true,
                        ["schema"] = JObject.Parse(request.jsonSchemaRaw)
                    }
                };
            }
            else
            {
                payload["response_format"] = new JObject { ["type"] = "json_object" };
            }

            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            using var req = new UnityWebRequest(_config.EndpointUrl, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.Max(5, _config.TimeoutSeconds);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {_config.ApiKey}");

            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            string rawResp = req.downloadHandler?.text ?? string.Empty;
            if (req.result != UnityWebRequest.Result.Success)
            {
                string detail = TryExtractOpenAiError(rawResp);
                throw new Exception($"OpenAI API 오류 ({req.responseCode}): {detail}");
            }

            var parsed = JObject.Parse(rawResp);
            string content = parsed["choices"]?[0]?["message"]?["content"]?.ToString();
            if (string.IsNullOrWhiteSpace(content))
                throw new Exception("OpenAI 응답 본문(choices[0].message.content)이 비어 있습니다.");

            return content.Trim();
        }

        private static string TryExtractOpenAiError(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "네트워크 응답 없음";
            try
            {
                var obj = JObject.Parse(raw);
                string msg = obj["error"]?["message"]?.ToString();
                if (!string.IsNullOrEmpty(msg)) return msg;
            }
            catch
            {
                // ignore JSON parse failure on error body
            }
            return raw.Length > 200 ? raw.Substring(0, 200) : raw;
        }
    }
}

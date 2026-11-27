using System.Threading;
using Cysharp.Threading.Tasks;

namespace ColdCaseOS.Core.LLM
{
    /// <summary>
    /// OpenAI / Mock 등 교체 가능한 LLM API 프로바이더 추상 인터페이스.
    /// 모든 구현체는 지정된 JSON Schema에 부합하는 JSON 문자열을 반환합니다.
    /// </summary>
    public interface ILlmProvider
    {
        bool IsLiveApi { get; }
        string ProviderDisplayName { get; }
        UniTask<string> CompleteJsonAsync(LlmChatRequest request, CancellationToken ct = default);
    }
}

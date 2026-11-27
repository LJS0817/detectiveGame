using ColdCaseOS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.UI
{
    /// <summary>
    /// 음량, 전체화면, 튜토리얼 초기화, OpenAI API 키(BYOK)·모델 설정을 제어하는 환경 설정 모달.
    /// </summary>
    public class SettingsModal : MonoBehaviour
    {
        [Header("Audio & Display")]
        [SerializeField] private AudioController audioController;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button resetTutorialButton;
        [SerializeField] private Button closeButton;

        [Header("OpenAI LLM Settings (Local BYOK)")]
        [SerializeField] private TMP_InputField apiKeyInputField;
        [SerializeField] private TMP_InputField modelInputField;
        [SerializeField] private Button saveLlmButton;
        [SerializeField] private TMP_Text llmStatusText;

        private void Awake()
        {
            if (bgmSlider != null)
                bgmSlider.onValueChanged.AddListener(v => audioController?.SetBgmVolume(v));
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(v => audioController?.SetSfxVolume(v));
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(isOn => Screen.fullScreen = isOn);
            if (resetTutorialButton != null)
                resetTutorialButton.onClick.AddListener(OnResetTutorialClicked);
            if (saveLlmButton != null)
                saveLlmButton.onClick.AddListener(OnSaveLlmClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            gameObject.SetActive(false);
        }

        public void Open()
        {
            if (audioController != null)
            {
                if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(audioController.BgmVolume);
                if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audioController.SfxVolume);
            }
            if (fullscreenToggle != null)
                fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);

            var cfg = AppRoot.Instance?.LlmConfig;
            if (cfg != null)
            {
                cfg.Reload();
                if (apiKeyInputField != null) apiKeyInputField.SetTextWithoutNotify(cfg.ApiKey);
                if (modelInputField != null) modelInputField.SetTextWithoutNotify(cfg.Model);
                RefreshLlmStatusText();
            }

            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OnSaveLlmClicked()
        {
            var app = AppRoot.Instance;
            if (app?.LlmConfig == null) return;

            string key = apiKeyInputField != null ? apiKeyInputField.text : string.Empty;
            string model = modelInputField != null ? modelInputField.text : string.Empty;
            app.LlmConfig.SaveUserApiKey(key, model);
            app.RebuildLlmProvider();
            RefreshLlmStatusText();

            GameEvents.RaiseToastRequested("LLM 설정 저장", $"현재 엔진: {app.LlmProvider.ProviderDisplayName}");
        }

        private void RefreshLlmStatusText()
        {
            if (llmStatusText == null) return;
            var provider = AppRoot.Instance?.LlmProvider;
            llmStatusText.text = provider != null
                ? $"활성 엔진: {provider.ProviderDisplayName}"
                : "활성 엔진: 미초기화";
        }

        private static void OnResetTutorialClicked()
        {
            TutorialController.ResetTutorialProgress();
            GameEvents.RaiseToastRequested("설정 완료", "EP01 튜토리얼 가이드가 초기화되었습니다.");
        }
    }
}

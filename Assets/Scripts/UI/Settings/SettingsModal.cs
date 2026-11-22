using ColdCaseOS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.UI
{
    /// <summary>
    /// 음량, 전체화면, 튜토리얼 초기화 등을 제어하는 환경 설정 모달.
    /// </summary>
    public class SettingsModal : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioController audioController;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button resetTutorialButton;
        [SerializeField] private Button closeButton;

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

            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private static void OnResetTutorialClicked()
        {
            TutorialController.ResetTutorialProgress();
            GameEvents.RaiseToastRequested("설정 완료", "EP01 튜토리얼 가이드가 초기화되었습니다.");
        }
    }
}

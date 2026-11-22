using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.Forensic
{
    /// <summary>
    /// OSWindow를 직접 상속하는 오디오 파형 및 노이즈 필터 분석기 (EP2·EP5·EP7).
    /// 컷오프 주파수 슬라이더를 최적 구간에 맞추면 잡음이 제거되고 녹취록 단서를 증거판에 등록할 수 있습니다.
    /// </summary>
    public class AudioWaveAnalyzer : OSWindow
    {
        [Header("Audio Playback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioLowPassFilter lowPassFilter;
        [SerializeField] private float minCutoffHz = 300f;
        [SerializeField] private float maxCutoffHz = 6000f;
        [SerializeField] private float filterTolerance = 0.08f;

        [Header("UI Controls")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text transcriptText;
        [SerializeField] private Slider cutoffSlider;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button extractClueButton;

        private AudioClipData _currentData;
        private AudioClip _loadedClip;
        private bool _isFilterMatched;

        protected override void Awake()
        {
            base.Awake();
            if (playPauseButton != null) playPauseButton.onClick.AddListener(TogglePlayPause);
            if (extractClueButton != null) extractClueButton.onClick.AddListener(ExtractTranscriptClue);
            if (cutoffSlider != null) cutoffSlider.onValueChanged.AddListener(OnCutoffSliderChanged);
        }

        private void OnDestroy()
        {
            if (_loadedClip != null) Destroy(_loadedClip);
        }

        protected override void OnWindowMinimized() => StopPlayback();
        protected override void OnWindowClosed() => StopPlayback();

        public async UniTaskVoid OpenAudioAsync(AudioClipData clipData)
        {
            if (clipData == null) return;
            _currentData = clipData;
            _isFilterMatched = false;

            if (titleText != null) titleText.text = clipData.title;
            if (transcriptText != null) transcriptText.text = "[잡음 간섭으로 음성이 불명확합니다. 필터 주파수를 조절하세요.]";
            if (extractClueButton != null) extractClueButton.interactable = false;

            if (cutoffSlider != null)
            {
                cutoffSlider.SetValueWithoutNotify(0f);
                OnCutoffSliderChanged(0f);
            }

            StopPlayback();
            var loaded = await EpisodeLoader.LoadAudioClipAsync(clipData.audioPath);
            if (_loadedClip != null) Destroy(_loadedClip);
            _loadedClip = loaded;

            if (audioSource != null && _loadedClip != null)
                audioSource.clip = _loadedClip;

            if (AppRoot.Instance != null)
            {
                AppRoot.Instance.RuntimeState.MarkItemRead(clipData.audioId);
                AppRoot.Instance.RuntimeState.DiscoverSuspects(clipData.revealsSuspectIds);
            }

            Open();
        }

        public void TogglePlayPause()
        {
            if (audioSource == null || audioSource.clip == null) return;
            if (audioSource.isPlaying) audioSource.Pause();
            else audioSource.Play();
            GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
        }

        private void StopPlayback()
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }

        private void OnCutoffSliderChanged(float normalizedValue)
        {
            if (lowPassFilter != null)
                lowPassFilter.cutoffFrequency = Mathf.Lerp(minCutoffHz, maxCutoffHz, normalizedValue);

            if (_currentData == null) return;

            float diff = Mathf.Abs(normalizedValue - _currentData.optimalFilterCutoff);
            _isFilterMatched = diff <= filterTolerance;

            if (statusText != null)
            {
                statusText.text = _isFilterMatched
                    ? "<color=#7AD98A>신호 선명함 — 음성 복원 완료</color>"
                    : $"노이즈 필터 대역: {Mathf.RoundToInt(Mathf.Lerp(minCutoffHz, maxCutoffHz, normalizedValue))} Hz";
            }

            if (transcriptText != null)
            {
                transcriptText.text = _isFilterMatched
                    ? _currentData.transcriptAfterFilter
                    : "[잡음 간섭으로 음성이 불명확합니다. 필터 주파수를 조절하세요.]";
            }

            if (extractClueButton != null)
                extractClueButton.interactable = _isFilterMatched;
        }

        private void ExtractTranscriptClue()
        {
            if (_currentData == null || !_isFilterMatched || AppRoot.Instance == null) return;

            var card = UserClueCard.Create(
                sourceId: _currentData.audioId,
                sourceType: ClueSourceType.AudioClip,
                displayTitle: $"[음성 복원] {_currentData.title}",
                hiddenEvidenceId: _currentData.evidenceId,
                extractedText: _currentData.transcriptAfterFilter);

            AppRoot.Instance.RuntimeState.AddClueCard(card);
        }
    }
}

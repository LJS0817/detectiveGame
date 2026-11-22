using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// 싱글톤 없이 GameEvents.OnSfxRequested를 구독하여 효과음 및 BGM을 재생하는 오디오 컨트롤러.
    /// </summary>
    public class AudioController : MonoBehaviour
    {
        private const string PrefBgmVolume = "KPOL_BGM_VOL";
        private const string PrefSfxVolume = "KPOL_SFX_VOL";

        [Serializable]
        public class SfxEntry
        {
            public string key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volumeScale = 1f;
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Clips")]
        [SerializeField] private AudioClip defaultBgm;
        [SerializeField] private List<SfxEntry> sfxEntries = new List<SfxEntry>();

        private readonly Dictionary<string, SfxEntry> _sfxMap = new Dictionary<string, SfxEntry>(StringComparer.OrdinalIgnoreCase);

        public float BgmVolume { get; private set; } = 0.6f;
        public float SfxVolume { get; private set; } = 0.8f;

        private void Awake()
        {
            foreach (var entry in sfxEntries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.key) && entry.clip != null)
                    _sfxMap[entry.key] = entry;
            }

            BgmVolume = PlayerPrefs.GetFloat(PrefBgmVolume, 0.6f);
            SfxVolume = PlayerPrefs.GetFloat(PrefSfxVolume, 0.8f);
            ApplyVolumes();

            if (bgmSource != null && defaultBgm != null)
            {
                bgmSource.clip = defaultBgm;
                bgmSource.loop = true;
                bgmSource.Play();
            }
        }

        private void OnEnable()
        {
            GameEvents.OnSfxRequested += PlaySfx;
        }

        private void OnDisable()
        {
            GameEvents.OnSfxRequested -= PlaySfx;
        }

        public void SetBgmVolume(float value)
        {
            BgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefBgmVolume, BgmVolume);
            ApplyVolumes();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefSfxVolume, SfxVolume);
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            if (bgmSource != null) bgmSource.volume = BgmVolume;
            if (sfxSource != null) sfxSource.volume = SfxVolume;
        }

        public void PlaySfx(string sfxKey)
        {
            if (sfxSource == null || string.IsNullOrEmpty(sfxKey)) return;
            if (_sfxMap.TryGetValue(sfxKey, out var entry) && entry.clip != null)
            {
                sfxSource.PlayOneShot(entry.clip, SfxVolume * entry.volumeScale);
            }
        }
    }
}

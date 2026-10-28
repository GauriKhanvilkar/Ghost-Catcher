using UnityEngine;
using UnityEngine.SceneManagement;

namespace GhostCatcher
{
    /// <summary>
    /// Lightweight 2D audio. Uses inspector clips when assigned, otherwise
    /// generates short procedural SFX so the project works with no audio files.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public enum MusicTrack { None, Lobby, Gameplay }

        [Header("Optional Clips (procedural fallback if empty)")]
        [SerializeField] private AudioClip sabotageAlarmClip;
        [SerializeField] private AudioClip scanBeepClip;
        [SerializeField] private AudioClip blendWhooshClip;
        [SerializeField] private AudioClip timerTickClip;
        [SerializeField] private AudioClip lobbyMusicClip;
        [SerializeField] private AudioClip gameplayMusicClip;

        private AudioSource sfxSource;
        private AudioSource musicSource;
        private MusicTrack currentTrack = MusicTrack.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0.18f;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            ApplyMusicForScene(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyMusicForScene(scene.name);
        }

        private void ApplyMusicForScene(string sceneName)
        {
            if (sceneName == Constants.SCENE_GAME)
                PlayMusic(MusicTrack.Gameplay);
            else
                PlayMusic(MusicTrack.Lobby);
        }

        public void PlaySabotageAlarm() => PlaySfx(sabotageAlarmClip, 440f, 0.45f, 0.35f, true);
        public void PlayScanBeep()      => PlaySfx(scanBeepClip, 880f, 0.12f, 0.28f, false);
        public void PlayBlendWhoosh()   => PlaySfx(blendWhooshClip, 220f, 0.28f, 0.3f, true);
        public void PlayTimerTick()     => PlaySfx(timerTickClip, 660f, 0.06f, 0.18f, false);

        public void PlayMusic(MusicTrack track)
        {
            if (currentTrack == track) return;
            currentTrack = track;

            if (track == MusicTrack.None)
            {
                musicSource.Stop();
                musicSource.clip = null;
                return;
            }

            AudioClip clip = track == MusicTrack.Gameplay ? gameplayMusicClip : lobbyMusicClip;
            if (clip == null)
                clip = BuildMusicLoop(track == MusicTrack.Gameplay);

            musicSource.clip = clip;
            musicSource.Play();
        }

        private void PlaySfx(AudioClip clip, float freq, float duration, float volume, bool sweepDown)
        {
            if (clip == null)
                clip = BuildTone(freq, duration, volume, sweepDown);
            sfxSource.PlayOneShot(clip, 1f);
        }

        private static AudioClip BuildTone(float freq, float duration, float volume, bool sweepDown)
        {
            int sampleRate = 22050;
            int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            var clip = AudioClip.Create($"sfx_{freq:0}", samples, 1, sampleRate, false);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float f = sweepDown ? Mathf.Lerp(freq, freq * 0.45f, t) : freq;
                float envelope = (1f - t) * (1f - t);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * i / sampleRate) * envelope * volume;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildMusicLoop(bool gameplay)
        {
            int sampleRate = 22050;
            float duration = 4f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var clip = AudioClip.Create(gameplay ? "music_game" : "music_lobby", samples, 1, sampleRate, false);
            var data = new float[samples];

            float root = gameplay ? 196f : 261.63f;
            float fifth = root * 1.5f;
            float volume = gameplay ? 0.045f : 0.035f;

            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)sampleRate;
                float pulse = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * (gameplay ? 1.5f : 0.5f) * time);
                float wave =
                    Mathf.Sin(2f * Mathf.PI * root * time) * 0.6f +
                    Mathf.Sin(2f * Mathf.PI * fifth * time) * 0.4f;
                data[i] = wave * volume * pulse;
            }

            clip.SetData(data, 0);
            return clip;
        }
    }
}

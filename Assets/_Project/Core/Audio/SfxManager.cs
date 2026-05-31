using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using _Project.Core.Events;
using _Project.Core.Structs;

namespace _Project.Core.Audio
{
    public class SfxManager : MonoBehaviour
    {
        public static SfxManager Instance { get; private set; }

        [Header("Mixer Routing")]
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        [Header("Pooling")]
        [SerializeField] private int initialPoolSize = 16;
        [SerializeField] private int maxPoolSize = 64;

        [Header("Library")]
        public SfxLibrary Library;

        [Header("Feedback Volumes")]
        [SerializeField] private float hitVolume = 0.7f;
        [SerializeField] private float killVolume = 1.0f;
        [SerializeField] private float hurtVolume = 0.85f;

        private readonly Queue<AudioSource> _pool = new Queue<AudioSource>();
        private readonly List<AudioSource> _active = new List<AudioSource>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < initialPoolSize; i++)
            {
                _pool.Enqueue(CreateSource());
            }

            GameFeelEvents.OnLocalPlayerHit += HandleLocalPlayerHit;
            GameFeelEvents.OnLocalPlayerHurt += HandleLocalPlayerHurt;
        }

        private void OnDestroy()
        {
            GameFeelEvents.OnLocalPlayerHit -= HandleLocalPlayerHit;
            GameFeelEvents.OnLocalPlayerHurt -= HandleLocalPlayerHurt;
            if (Instance == this) Instance = null;
        }

        private void HandleLocalPlayerHit(DamageContext ctx, bool wasKilled)
        {
            if (Library == null) return;
            AudioClip clip = wasKilled ? Library.enemyKillConfirm : Library.hitFleshLight;
            float volume = wasKilled ? killVolume : hitVolume;
            PlayOneShot2D(clip, volume);
        }

        private void HandleLocalPlayerHurt(DamageContext ctx)
        {
            if (Library == null) return;
            PlayOneShot2D(Library.playerHurt, hurtVolume);
        }

        private AudioSource CreateSource()
        {
            var go = new GameObject("SfxSource");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = sfxGroup;
            return src;
        }

        public void PlayOneShot2D(AudioClip clip, float volume = 1f, float pitch = 1f, bool ui = false)
        {
            if (clip == null) return;
            var src = Rent();
            src.transform.position = Vector3.zero;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = ui ? uiGroup : sfxGroup;
            src.volume = volume;
            src.pitch = pitch;
            src.PlayOneShot(clip);
            StartCoroutine(ReturnWhenDone(src, clip.length / Mathf.Max(0.01f, pitch)));
        }

        public void PlayOneShot3D(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f,
                                  float minDistance = 2f, float maxDistance = 30f)
        {
            if (clip == null) return;
            var src = Rent();
            src.transform.position = position;
            src.spatialBlend = 1f;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.outputAudioMixerGroup = sfxGroup;
            src.volume = volume;
            src.pitch = pitch;
            src.PlayOneShot(clip);
            StartCoroutine(ReturnWhenDone(src, clip.length / Mathf.Max(0.01f, pitch)));
        }

        private AudioSource Rent()
        {
            if (_pool.Count > 0)
            {
                var s = _pool.Dequeue();
                _active.Add(s);
                return s;
            }

            if (_active.Count < maxPoolSize)
            {
                var s = CreateSource();
                _active.Add(s);
                return s;
            }

            // Pool exhausted — steal oldest active.
            var oldest = _active[0];
            _active.RemoveAt(0);
            _active.Add(oldest);
            oldest.Stop();
            return oldest;
        }

        private IEnumerator ReturnWhenDone(AudioSource src, float seconds)
        {
            yield return new WaitForSeconds(seconds + 0.05f);
            if (!_active.Remove(src)) yield break;
            _pool.Enqueue(src);
        }
    }
}

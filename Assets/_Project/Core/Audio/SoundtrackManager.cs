using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using _Project.Features.StageSystem.Data;
using _Project.Features.StageSystem.Scripts;

namespace _Project.Core.Audio
{
    public class SoundtrackManager : MonoBehaviour
    {
        public static SoundtrackManager Singleton { get; private set; }

        [Header("References")]
        public AudioSource audioSource;
        public MapDatabase mapDatabase;

        [Header("OST Clips")]
        public List<AudioClip> pveRegularSongs;
        public AudioClip finalStageSong;

        [Header("Settings")]
        public float pauseBetweenPveSongs = 5f;

        private Coroutine _pveRoutine;
        private int _currentPveIndex = 0;

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject);
                return;
            }
            Singleton = this;
            DontDestroyOnLoad(gameObject);

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.spatialBlend = 0f; // 2D sound, non-spatial
            audioSource.loop = false;

            if (pveRegularSongs == null || pveRegularSongs.Count == 0)
            {
                pveRegularSongs = new List<AudioClip>
                {
                    Resources.Load<AudioClip>("OST/truth"),
                    Resources.Load<AudioClip>("OST/turmoil")
                };
            }
            
            if (finalStageSong == null)
            {
                finalStageSong = Resources.Load<AudioClip>("OST/conflict");
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mapDatabase == null)
            {
                if (RunNetworkController.Singleton != null)
                {
                    mapDatabase = RunNetworkController.Singleton.mapDatabase;
                }
            }
            
            if (mapDatabase == null) return;

            string loadedSceneName = scene.name;

            if (mapDatabase.standardMapScenes.Contains(loadedSceneName))
            {
                // Regular stages play the PVE sequence
                PlayPveMusic();
            }
            else if (loadedSceneName == mapDatabase.finalPvEMapScene || loadedSceneName == mapDatabase.finalPvPMapScene)
            {
                // Final stage plays the final song
                PlayFinalMusic();
            }
            else
            {
                StopMusic();
            }
        }

        private void PlayPveMusic()
        {
            if (_pveRoutine != null) return; 
            
            audioSource.Stop(); 
            _pveRoutine = StartCoroutine(PveMusicSequence());
        }

        private void PlayFinalMusic()
        {
            StopMusic();

            if (finalStageSong != null)
            {
                audioSource.clip = finalStageSong;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void StopMusic()
        {
            if (_pveRoutine != null)
            {
                StopCoroutine(_pveRoutine);
                _pveRoutine = null;
            }
            audioSource.Stop();
            audioSource.loop = false;
        }

        private IEnumerator PveMusicSequence()
        {
            if (pveRegularSongs == null || pveRegularSongs.Count == 0) yield break;

            while (true)
            {
                AudioClip clipToPlay = pveRegularSongs[_currentPveIndex];
                
                if (clipToPlay != null)
                {
                    audioSource.clip = clipToPlay;
                    audioSource.Play();

                    // Wait until the clip finishes
                    yield return new WaitUntil(() => !audioSource.isPlaying);
                }

                _currentPveIndex = (_currentPveIndex + 1) % pveRegularSongs.Count;

                // After a full cycle, pause before repeating
                if (_currentPveIndex == 0)
                {
                    yield return new WaitForSeconds(pauseBetweenPveSongs);
                }
                else
                {
                    yield return null; 
                }
            }
        }
    }
}

using System.Collections;
using UnityEngine;

namespace _Project.Core.Managers
{
    public class HitStopManager : MonoBehaviour
    {
        private static HitStopManager _instance;
        private bool _isStopped;

        public static HitStopManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("HitStopManager");
                    _instance = go.AddComponent<HitStopManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void TriggerHitStop(float duration, float timeScale = 0.05f)
        {
            if (_isStopped) return;
            StartCoroutine(HitStopRoutine(duration, timeScale));
        }

        private IEnumerator HitStopRoutine(float duration, float timeScale)
        {
            _isStopped = true;
            Time.timeScale = timeScale;
            
            // Wait based on unscaled time, since timeScale is modified
            yield return new WaitForSecondsRealtime(duration);
            
            Time.timeScale = 1f;
            _isStopped = false;
        }
    }
}


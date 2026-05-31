using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace _Project.Features.GameFeel.Scripts
{
    public class WorldDamageNumberSpawner : MonoBehaviour
    {
        private static WorldDamageNumberSpawner _instance;

        [SerializeField] private int initialPoolSize = 24;
        [SerializeField] private int maxPoolSize = 96;
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private float floatDistance = 1.2f;
        [SerializeField] private float spawnJitter = 0.25f;
        [SerializeField] private float baseFontSize = 4f;
        [SerializeField] private float critFontSize = 6f;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color critColor = new Color(1f, 0.5f, 0.1f, 1f);
        [SerializeField] private TMP_FontAsset fontOverride;

        private readonly Queue<DamageNumber> _pool = new Queue<DamageNumber>();
        private readonly List<DamageNumber> _active = new List<DamageNumber>();
        private Camera _cam;

        public static WorldDamageNumberSpawner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("WorldDamageNumberSpawner");
                    _instance = go.AddComponent<WorldDamageNumberSpawner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < initialPoolSize; i++) _pool.Enqueue(CreateNumber());
        }

        private DamageNumber CreateNumber()
        {
            var go = new GameObject("DamageNumber");
            go.transform.SetParent(transform);
            go.SetActive(false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2f, 1f);
            rt.localScale = Vector3.one * 0.05f;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = baseFontSize;
            text.enableWordWrapping = false;
            text.text = "0";
            if (fontOverride != null) text.font = fontOverride;

            var textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return new DamageNumber { Go = go, Canvas = canvas, Text = text, Rect = rt };
        }

        public void Spawn(Vector3 worldPos, int damage, bool isCritical)
        {
            if (damage <= 0) return;
            var n = Rent();
            n.Go.SetActive(true);

            Vector3 jitter = new Vector3(Random.Range(-spawnJitter, spawnJitter), Random.Range(0f, spawnJitter * 0.5f), Random.Range(-spawnJitter, spawnJitter));
            n.StartPos = worldPos + jitter;
            n.Rect.position = n.StartPos;
            n.Text.text = damage.ToString();
            n.Text.fontSize = isCritical ? critFontSize : baseFontSize;
            n.Text.color = isCritical ? critColor : normalColor;
            n.IsCrit = isCritical;
            n.Elapsed = 0f;

            if (n.Routine != null) StopCoroutine(n.Routine);
            n.Routine = StartCoroutine(Animate(n));
        }

        private IEnumerator Animate(DamageNumber n)
        {
            float duration = lifetime;
            Color startColor = n.Text.color;
            float popScale = n.IsCrit ? 1.4f : 1.1f;

            while (n.Elapsed < duration)
            {
                float t = n.Elapsed / duration;
                Vector3 pos = n.StartPos + Vector3.up * (floatDistance * t);
                n.Rect.position = pos;

                if (_cam == null) _cam = Camera.main;
                if (_cam != null)
                {
                    n.Rect.rotation = Quaternion.LookRotation(n.Rect.position - _cam.transform.position);
                }

                float pop = t < 0.15f ? Mathf.Lerp(popScale, 1f, t / 0.15f) : 1f;
                n.Rect.localScale = Vector3.one * 0.05f * pop;

                float alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                var c = startColor; c.a = alpha; n.Text.color = c;

                n.Elapsed += Time.deltaTime;
                yield return null;
            }

            Return(n);
        }

        private DamageNumber Rent()
        {
            if (_pool.Count > 0)
            {
                var n = _pool.Dequeue();
                _active.Add(n);
                return n;
            }
            if (_active.Count < maxPoolSize)
            {
                var n = CreateNumber();
                _active.Add(n);
                return n;
            }
            var oldest = _active[0];
            _active.RemoveAt(0);
            if (oldest.Routine != null) StopCoroutine(oldest.Routine);
            _active.Add(oldest);
            return oldest;
        }

        private void Return(DamageNumber n)
        {
            n.Go.SetActive(false);
            n.Routine = null;
            if (_active.Remove(n)) _pool.Enqueue(n);
        }

        private class DamageNumber
        {
            public GameObject Go;
            public Canvas Canvas;
            public TextMeshProUGUI Text;
            public RectTransform Rect;
            public Vector3 StartPos;
            public float Elapsed;
            public bool IsCrit;
            public Coroutine Routine;
        }
    }
}

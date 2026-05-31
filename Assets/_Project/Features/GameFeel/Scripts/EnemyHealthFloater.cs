using UnityEngine;
using UnityEngine.UI;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;

namespace _Project.Features.GameFeel.Scripts
{
    /// Visual-only floating HP bar that appears on hit and fades after a moment.
    /// Attach to any GameObject that also has CharacterEventBus + CharacterStats.
    [RequireComponent(typeof(CharacterEventBus))]
    public class EnemyHealthFloater : MonoBehaviour
    {
        [SerializeField] private float yOffset = 2.0f;
        [SerializeField] private float fadeDelay = 2.5f;
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private Vector2 barSize = new Vector2(1.4f, 0.18f);
        [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color fillColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        private CharacterEventBus _bus;
        private CharacterStats _stats;
        private Canvas _canvas;
        private RectTransform _canvasRt;
        private RectTransform _fillRt;
        private Image _fillImage;
        private CanvasGroup _group;
        private Camera _cam;
        private float _lastDamageTime = -999f;
        private bool _initialized;
        private float _maxHealth;

        private const float FillLeft = 0.03f;
        private const float FillRight = 0.97f;

        private void Awake()
        {
            _bus = GetComponent<CharacterEventBus>();
            _stats = GetComponent<CharacterStats>();
        }

        private void OnEnable()
        {
            if (_bus != null)
            {
                _bus.OnDamageTaken += HandleDamageTaken;
                _bus.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_bus != null)
            {
                _bus.OnDamageTaken -= HandleDamageTaken;
                _bus.OnDeath -= HandleDeath;
            }
        }

        private void EnsureCanvas()
        {
            if (_initialized) return;
            _initialized = true;

            var go = new GameObject("HealthFloater");
            DontDestroyOnLoad(go);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 30;
            _canvasRt = _canvas.GetComponent<RectTransform>();
            _canvasRt.sizeDelta = barSize;
            _canvasRt.localScale = Vector3.one * 0.1f;

            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var bg = new GameObject("Bg");
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = backColor;
            var bgRt = bgImg.rectTransform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            _fillImage = fill.AddComponent<Image>();
            _fillImage.color = fillColor;
            _fillRt = _fillImage.rectTransform;
            _fillRt.anchorMin = new Vector2(FillLeft, 0.18f);
            _fillRt.anchorMax = new Vector2(FillRight, 0.82f);
            _fillRt.offsetMin = Vector2.zero;
            _fillRt.offsetMax = Vector2.zero;
        }

        private void HandleDamageTaken(DamageContext ctx)
        {
            EnsureCanvas();
            _lastDamageTime = Time.time;

            // Server has the MaxHealth stat; clients don't (Initialize is server-only).
            // Infer max from the highest health we've ever observed (pre-damage value).
            int currentHp = _stats != null ? _stats.CurrentHealth.Value : 0;
            float statMax = _stats != null ? _stats.GetStat(StatType.MaxHealth) : 0f;
            float preDamage = currentHp + Mathf.Max(0, ctx.Damage);
            _maxHealth = Mathf.Max(_maxHealth, Mathf.Max(statMax, preDamage));

            RefreshFill();
        }

        private void HandleDeath()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void RefreshFill()
        {
            if (_stats == null || _fillRt == null) return;
            if (_maxHealth <= 0f) { _fillRt.anchorMax = new Vector2(FillLeft, _fillRt.anchorMax.y); return; }
            float pct = Mathf.Clamp01(_stats.CurrentHealth.Value / _maxHealth);
            float rightX = Mathf.Lerp(FillLeft, FillRight, pct);
            _fillRt.anchorMax = new Vector2(rightX, _fillRt.anchorMax.y);
        }

        private void LateUpdate()
        {
            if (!_initialized || _canvas == null) return;

            _canvasRt.position = transform.position + Vector3.up * yOffset;

            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
            {
                _canvasRt.rotation = Quaternion.LookRotation(_canvasRt.position - _cam.transform.position);
            }

            float since = Time.time - _lastDamageTime;
            float target;
            if (since <= fadeDelay) target = 1f;
            else if (since >= fadeDelay + fadeDuration) target = 0f;
            else target = 1f - (since - fadeDelay) / fadeDuration;

            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.deltaTime / Mathf.Max(0.01f, fadeDuration));
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}

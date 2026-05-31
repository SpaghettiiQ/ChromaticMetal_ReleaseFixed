using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Features.Player.Scripts
{
    /// Local-player-only screen-space speed lines. Fades in when horizontal speed
    /// exceeds run speed (e.g. immediately after a Combat Slide).
    /// Procedurally generates its radial-streaks texture so no art asset is needed.
    [RequireComponent(typeof(CharacterController))]
    public class SpeedLinesOverlay : NetworkBehaviour
    {
        [Header("Speed Range")]
        [Tooltip("Below this horizontal speed, lines are invisible.")]
        [SerializeField] private float minSpeed = 13f;
        [Tooltip("At or above this speed, lines reach maxAlpha.")]
        [SerializeField] private float maxSpeed = 22f;

        [Header("Fade")]
        [Tooltip("Maximum overlay alpha at full speed.")]
        [SerializeField] private float maxAlpha = 0.55f;
        [Tooltip("How fast the overlay's alpha approaches its target each second.")]
        [SerializeField] private float fadeLerpSpeed = 10f;

        [Header("Texture")]
        [SerializeField] private int textureSize = 256;
        [SerializeField] private int streakCount = 64;
        [SerializeField] private float innerRadiusFraction = 0.35f;
        [SerializeField] private Color streakColor = Color.white;

        private CharacterController _controller;
        private Canvas _canvas;
        private Image _image;
        private float _currentAlpha;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            BuildOverlay();
        }

        private void BuildOverlay()
        {
            var go = new GameObject("SpeedLinesOverlay");
            DontDestroyOnLoad(go);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var imgGo = new GameObject("Lines");
            imgGo.transform.SetParent(go.transform, false);
            _image = imgGo.AddComponent<Image>();
            _image.raycastTarget = false;
            _image.sprite = BuildStreakSprite();
            _image.color = new Color(streakColor.r, streakColor.g, streakColor.b, 0f);
            _image.type = Image.Type.Simple;
            _image.preserveAspect = false;

            var rt = _image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private Sprite BuildStreakSprite()
        {
            int size = Mathf.Max(64, textureSize);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.5f;
            float innerR = maxR * Mathf.Clamp01(innerRadiusFraction);

            for (int s = 0; s < streakCount; s++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radial0 = Mathf.Lerp(innerR, maxR * 0.85f, Random.value);
                float radial1 = Mathf.Lerp(radial0 + 4f, maxR + 2f, Random.value);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                int steps = Mathf.CeilToInt(radial1 - radial0) * 2;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float r = Mathf.Lerp(radial0, radial1, t);
                    Vector2 p = center + dir * r;
                    int px = Mathf.RoundToInt(p.x);
                    int py = Mathf.RoundToInt(p.y);
                    if (px < 0 || px >= size || py < 0 || py >= size) continue;

                    // Alpha grows toward the edge of the screen.
                    float alphaT = Mathf.InverseLerp(innerR, maxR, r);
                    byte a = (byte)(Mathf.Clamp01(alphaT) * 255f);
                    int idx = py * size + px;
                    if (pixels[idx].a < a) pixels[idx].a = a;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private void LateUpdate()
        {
            if (_image == null) return;

            Vector3 v = _controller.velocity;
            float horizSpeed = new Vector2(v.x, v.z).magnitude;

            float t = Mathf.InverseLerp(minSpeed, maxSpeed, horizSpeed);
            float targetAlpha = t * maxAlpha;

            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, fadeLerpSpeed * Time.deltaTime);
            var c = _image.color;
            c.a = _currentAlpha;
            _image.color = c;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}

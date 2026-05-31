using UnityEngine;

namespace _Project.UI_Resources.Scripts
{
    public class FlickeringLight : MonoBehaviour
    {
        public Light lampLight;
        public float minIntensity = 0.5f;
        private float maxIntensity;
        [Tooltip("How fast the flicker happens")]
        public float flickerSpeed = 0.1f;

        private float timer;

        private Renderer _renderer;
        private Material _material;
        private Color _maxEmissionColor;
        private Color _minEmissionColor;

        void Start()
        {
            // Automatically grab the Light component if not assigned
            if (lampLight == null)
            {
                lampLight = GetComponent<Light>();
            }
            
            if (lampLight != null)
            {
                maxIntensity = lampLight.intensity;
            }

            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _material = _renderer.material;
                if (_material.HasProperty("_EmissionColor"))
                {
                    _maxEmissionColor = _material.GetColor("_EmissionColor");
                    float factor = maxIntensity > 0 ? minIntensity / maxIntensity : 0;
                    _minEmissionColor = _maxEmissionColor * factor;
                }
                else
                {
                    _material = null;
                }
            }
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer > flickerSpeed)
            {
                bool isMin = lampLight.intensity == maxIntensity;
                // Switch between the max (starting) and min values
                lampLight.intensity = isMin ? minIntensity : maxIntensity;
                
                if (_material != null)
                {
                    _material.SetColor("_EmissionColor", isMin ? _minEmissionColor : _maxEmissionColor);
                }
            
                // Randomize the next flicker interval slightly for a natural look
                timer = Random.Range(0f, flickerSpeed * 0.5f);
            }
        }
    }
}
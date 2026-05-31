using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Data
{
    [CreateAssetMenu(fileName = "NewProxyAbility", menuName = "Game Data/Proxy Ability Data")]
    public class ProxyAbilityData : ScriptableObject, IProxyAbilityData
    {
        [Header("Ability Identity")]
        public string abilityName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("Execution Parameters")]
        public float cooldownTime = 5f;

        [Header("Audiovisual Feedback")]
        public AudioClip activationSound;
        public GameObject activationVfxPrefab;
        [Tooltip("Camera shake magnitude raised on the casting player when the ability activates (0 = no shake). Same mechanism as a weapon's fire shake.")]
        public float cameraShakeMagnitude = 0f;
        [Tooltip("Camera shake duration in seconds (0 = no shake).")]
        public float cameraShakeDuration = 0f;

        [Header("Effect Logic")]
        [Tooltip("Must be a ScriptableObject that implements IAbilityEffect.")]
        public ScriptableObject effectLogic;

        public IAbilityEffect Effect => effectLogic as IAbilityEffect;

        private void OnValidate()
        {
            if (effectLogic != null && !(effectLogic is IAbilityEffect))
            {
                Debug.LogError($"[{name}] The assigned effect '{effectLogic.name}' does not implement IAbilityEffect!");
                effectLogic = null;
            }
        }
    }
}
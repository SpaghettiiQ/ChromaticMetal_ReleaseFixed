using UnityEngine;
using Unity.Netcode;
using _Project.Core.Events;
using _Project.Core.Structs;
using _Project.Core.Enums;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking
{
    public class ReturnToNothingnessTracker : MonoBehaviour
    {
        private CharacterEventBus _eventBus;
        private AbilityController _abilityController;
        private float _reductionAmount;

        public void Initialize(float reductionAmount)
        {
            _reductionAmount = reductionAmount;
            
            // Only proc logic goes through the server anyway, since passives execute on the server.
            _eventBus = GetComponent<CharacterEventBus>();
            _abilityController = GetComponent<AbilityController>();

            if (_eventBus != null)
            {
                _eventBus.OnDamageTaken += HandleDamageTaken;
            }
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.OnDamageTaken -= HandleDamageTaken;
            }
        }

        private void HandleDamageTaken(DamageContext ctx)
        {
            // Only proc if the damage wasn't completely mitigated
            if (ctx.Damage > 0 && _abilityController != null)
            {
                _abilityController.ReduceCooldown(AbilitySlot.Utility, _reductionAmount);
            }
        }
    }
}
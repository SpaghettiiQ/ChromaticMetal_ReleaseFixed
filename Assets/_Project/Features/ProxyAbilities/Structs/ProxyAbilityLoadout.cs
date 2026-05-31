using System;
using _Project.Features.ProxyAbilities.Data;

namespace _Project.Features.ProxyAbilities.Structs
{
    [Serializable]
    public struct ProxyAbilityLoadout
    {
        public ProxyAbilityData secondaryAbility;
        public ProxyAbilityData utilityAbility;
        public ProxyAbilityData specialAbility;
    }
}
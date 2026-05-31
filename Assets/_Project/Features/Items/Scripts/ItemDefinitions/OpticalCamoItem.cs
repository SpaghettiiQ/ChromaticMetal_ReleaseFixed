using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.Items.Data;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    [CreateAssetMenu(menuName = "Game Data/Items/Optical Camo", fileName = "OpticalCamo")]
    public class OpticalCamoItem : ItemDefinition, IPassiveItem
    {
        public float detectionRangeMultiplier = 0.5f;

        public void ApplyPassive(GameObject owner, int stacks)
        {
            // Requires enemy AI perception system: modify detection range/speed on nearby enemy agents.
            // Wire up once EnemyControllerBase exposes a perception radius or IPerceivable interface in Core.
        }

        public void RemovePassive(GameObject owner, int stacks)
        {
            // Revert AI perception modifications applied in ApplyPassive.
        }
    }
}
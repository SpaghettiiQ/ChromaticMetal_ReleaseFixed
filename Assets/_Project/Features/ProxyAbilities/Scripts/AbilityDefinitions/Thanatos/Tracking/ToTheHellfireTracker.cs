using UnityEngine;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Thanatos.Tracking
{
    /// <summary>
    /// Legacy tracker — no longer used by ToTheHellfireAbility (the ability now calls
    /// TheEverblackWeapon.HellfireBlast directly, which fires both barrels + auto-ejects
    /// the heat core via the standard overheat path). Kept as a vestigial component so any
    /// dangling MonoBehaviour references on existing player prefabs/scenes don't break
    /// deserialization. Safe to remove in the editor once verified unused.
    /// </summary>
    public class ToTheHellfireTracker : MonoBehaviour
    {
    }
}

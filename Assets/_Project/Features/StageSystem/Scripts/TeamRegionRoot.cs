using UnityEngine;
using UnityEngine.SceneManagement;
using _Project.Core.Enums;
using _Project.Core.Networking;

namespace _Project.Features.StageSystem.Scripts
{
    /// <summary>
    /// Attach to the empty parent GameObject of a team's region within a dual-region
    /// PvP stage scene. RunNetworkController toggles the parent's active state at
    /// runtime based on the replicated SceneOwnerEntry for the containing scene:
    /// when this team isn't currently in the scene, the whole region is disabled —
    /// hiding geometry, lighting and spawners that would otherwise bleed into the
    /// other team's view (relevant when two scenes are loaded simultaneously).
    /// </summary>
    [DisallowMultipleComponent]
    public class TeamRegionRoot : MonoBehaviour
    {
        [Tooltip("Which team this region belongs to. Set in the inspector when authoring the scene.")]
        public TeamAffiliation team = TeamAffiliation.None;

        /// <summary>
        /// Walks the loaded scene, finds every TeamRegionRoot, and SetActive each
        /// based on whether its team is in the given owner set.
        /// </summary>
        public static void ApplyOwnership(Scene scene, SceneOwnerEntry entry)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var regions = roots[i].GetComponentsInChildren<TeamRegionRoot>(true);
                for (int r = 0; r < regions.Length; r++)
                {
                    var region = regions[r];
                    bool shouldBeActive = region.team == TeamAffiliation.None || entry.Includes(region.team);
                    if (region.gameObject.activeSelf != shouldBeActive)
                        region.gameObject.SetActive(shouldBeActive);
                }
            }
        }
    }
}

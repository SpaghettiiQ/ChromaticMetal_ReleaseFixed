using Unity.Collections;
using Unity.Netcode;
using _Project.Core.Enums;

namespace _Project.Core.Networking
{
    /// <summary>
    /// Replicated description of which team(s) currently consider a given loaded scene
    /// their active stage. Drives client-side activation of dual-region authored maps:
    /// when a team isn't in a scene, that scene's TeamRegionRoot for that team is
    /// SetActive(false) so its geometry doesn't bleed into the other team's view.
    /// </summary>
    public struct SceneOwnerEntry : System.IEquatable<SceneOwnerEntry>, INetworkSerializable
    {
        public FixedString64Bytes SceneName;
        public bool HasCleansers;
        public bool HasThrive;

        public bool Includes(TeamAffiliation team)
        {
            if (team == TeamAffiliation.Cleansers) return HasCleansers;
            if (team == TeamAffiliation.Thrive) return HasThrive;
            return false;
        }

        public bool Equals(SceneOwnerEntry other) =>
            SceneName.Equals(other.SceneName) && HasCleansers == other.HasCleansers && HasThrive == other.HasThrive;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SceneName);
            serializer.SerializeValue(ref HasCleansers);
            serializer.SerializeValue(ref HasThrive);
        }
    }
}

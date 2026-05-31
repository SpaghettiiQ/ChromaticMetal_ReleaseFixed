namespace _Project.Core.Interfaces
{
    /// <summary>
    /// Implement on a NetworkBehaviour that needs to react to a stun.
    /// The implementer is responsible for replicating the locked state to the relevant client(s).
    /// Call from the server.
    /// </summary>
    public interface IStunnable
    {
        void Stun(float duration);
    }
}

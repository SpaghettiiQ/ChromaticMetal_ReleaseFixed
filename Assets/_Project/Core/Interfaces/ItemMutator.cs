namespace _Project.Core.Interfaces
{
    public interface IItemMutator
    {
        // Strictly called by the Server
        void TriggerRandomMutation();
    }
}
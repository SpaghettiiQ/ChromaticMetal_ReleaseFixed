namespace _Project.Core.Interfaces
{
    public interface IBurdenable
    {
        void AddBurden(float amount);
        void RemoveBurden(float amount);
        float GetCurrentBurden();
    }
}
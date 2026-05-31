namespace _Project.Core.Interfaces
{
    /// <summary>
    /// Lets Core and other features identify an enemy's archetype WITHOUT taking a
    /// dependency on the Enemies feature. Implemented by EnemyControllerBase, which
    /// surfaces its EnemyData.enemyName. Consumers (e.g. items that deal bonus damage
    /// to a specific enemy type) read this via GetComponentInParent&lt;IEnemyIdentity&gt;().
    /// </summary>
    public interface IEnemyIdentity
    {
        string EnemyTypeName { get; }
    }
}

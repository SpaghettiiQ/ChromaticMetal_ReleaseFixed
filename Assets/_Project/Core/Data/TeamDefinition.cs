using _Project.Core.Enums;
using UnityEngine;

namespace _Project.Core.Data
{
    [CreateAssetMenu(menuName = "Game Data/Team Definition")]
    public class TeamDefinition : ScriptableObject
    {
        [Header("Identity")]
        public TeamAffiliation teamAffiliation;
        public string teamName = "Team Alpha";
        public Color teamColor = Color.blue;
        
        [Header("Spawn Points")]
        [Tooltip("Tag used to find spawn points for this team")]
        public string spawnPointTag = "SpawnPoint_Alpha";
        
        [Header("UI")]
        public Sprite teamIcon;
        public string teamDescription = "N/a";
    }
}
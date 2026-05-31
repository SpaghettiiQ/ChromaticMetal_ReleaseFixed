using System.Collections.Generic;
using UnityEngine;

namespace _Project.Features.StageSystem.Data
{
    [CreateAssetMenu(menuName = "Game Data/Map Database")]
    public class MapDatabase : ScriptableObject
    {
        [Header("Standard Stages")]
        [Tooltip("The exact Scene names of your standard maps (Stages 1-3)")]
        public List<string> standardMapScenes;

        [Header("Final Stages")]
        [Tooltip("The exact Scene name for the final Co-op/PvE map")]
        public string finalPvEMapScene;
        
        [Tooltip("The exact Scene name for the shared Final PvP arena")]
        public string finalPvPMapScene;
    }
}
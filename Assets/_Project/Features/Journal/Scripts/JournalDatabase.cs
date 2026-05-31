using System.Collections.Generic;
using UnityEngine;

namespace _Project.Features.Journal.Scripts
{
    [CreateAssetMenu(menuName = "Game Data/Journal/Journal Database", fileName = "MasterJournalDatabase")]
    public class JournalDatabase : ScriptableObject
    {
        [Tooltip("Assign every JournalEntry in the game here.")]
        public List<JournalEntry> entries = new List<JournalEntry>();

        public IEnumerable<JournalEntry> GetByCategory(JournalCategory cat)
        {
            foreach (var e in entries)
            {
                if (e != null && e.category == cat)
                    yield return e;
            }
        }
    }
}

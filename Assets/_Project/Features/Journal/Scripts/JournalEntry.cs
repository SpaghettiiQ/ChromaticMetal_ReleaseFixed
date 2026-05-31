using UnityEngine;

namespace _Project.Features.Journal.Scripts
{
    public enum JournalCategory
    {
        Tutorial,
        Lore,
        Enemies,
        Items,
        Proxies,
        Maps
    }

    [CreateAssetMenu(menuName = "Game Data/Journal/Journal Entry", fileName = "JournalEntry")]
    public class JournalEntry : ScriptableObject
    {
        public string entryID;
        public JournalCategory category;
        public string title;
        [TextArea(6, 40)]
        public string body;
        public Sprite icon;
        public int sortOrder;
    }
}

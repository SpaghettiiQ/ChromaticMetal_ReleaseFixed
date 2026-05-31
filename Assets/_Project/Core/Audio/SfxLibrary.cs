using UnityEngine;

namespace _Project.Core.Audio
{
    [CreateAssetMenu(menuName = "Game Data/Audio/SFX Library", fileName = "SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        [Header("UI")]
        public AudioClip uiClick;
        public AudioClip uiHover;

        [Header("Combat — Hits")]
        public AudioClip hitFleshLight;
        public AudioClip hitFleshHeavy;
        public AudioClip hitMetal;

        [Header("Combat — Kills")]
        public AudioClip enemyKillConfirm;

        [Header("Enemies")]
        public AudioClip enemySpawn;
        public AudioClip enemyShoot;

        [Header("Extraction Door")]
        public AudioClip doorChargeLoop;
        public AudioClip doorChargeComplete;

        [Header("Player")]
        public AudioClip playerHurt;

        [Header("Weapon")]
        public AudioClip weaponReload;
    }
}

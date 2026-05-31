using UnityEngine;
using _Project.Features.WeaponCore.Enums;
using _Project.Core.Interfaces;

namespace _Project.Features.WeaponCore.Data
{
    [CreateAssetMenu(menuName = "Game Data/Weapon Data")]
    public class WeaponData : ScriptableObject, IWeaponData
    {
        [Header("General")]
        public string weaponName;
        public GameObject weaponPrefab;
        public int baseDamage = 25;
        public float range = 100f;

        public string WeaponName => weaponName;
        public GameObject WeaponPrefab => weaponPrefab;
        public int BaseDamage => baseDamage;

        [Header("Firing Mechanics")]
        public FireMode fireMode;
        [Tooltip("Time in seconds between shots/attacks")]
        public float fireRate = 0.1f; 
        [Tooltip("Time required to fully charge (if using Charge fire mode)")]
        public float chargeTime = 1.0f; 

        [Header("Ammo & Heat Mechanics")]
        public AmmoMechanic ammoMechanic;
        
        [Header("Magazine Settings")]
        public int maxAmmo = 30;
        public float reloadTime = 2.0f;

        [Header("Overheat Settings")]
        public float maxHeat = 100f;
        public float heatPerShot = 15f;
        [Tooltip("How much heat is naturally lost per second when not firing")]
        public float heatDecayRate = 25f;
        [Tooltip("How long the weapon is locked out if maxHeat is reached")]
        public float overheatPenaltyTime = 3.0f;

        [Header("Animations")]
        [Tooltip("1 = Variant 1, 2 = AK/Two-Handed, 3 = Pistol")]
        public int runAnimationVariant = 1;
        public bool useSlashOnAttack = false;

        [Header("Game Feel (Cosmetics & Polish)")]
        [Tooltip("Recoil force (Pitch, Yaw, Roll). Example: X=2 (up), Y=1, Z=0.5")]
        public Vector3 recoilForce;
        [Tooltip("How much the camera shakes when firing.")]
        public float cameraShakeMagnitude = 0f;
        [Tooltip("How long the camera shake lasts when firing.")]
        public float cameraShakeDuration = 0f;
        [Tooltip("If true, triggers a small hit stop on fire (more common for heavy impacts, but handy for massive guns)")]
        public bool triggerHitStopOnFire;
        [Tooltip("Hit stop freeze time in seconds (e.g., 0.02f)")]
        public float hitStopDuration = 0f;

        // Particle System Hooks
        [Header("VFX")]
        public GameObject muzzleFlashPrefab;
        public GameObject casingEjectPrefab;
    }
}
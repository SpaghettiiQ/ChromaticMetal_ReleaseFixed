using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using _Project.Core.Enums;
using _Project.Core.Networking;
using _Project.Core.Stats;
using _Project.Features.DifficultySystem.Scripts;
using _Project.Features.Enemies.Scripts.Core;
using _Project.Features.Enemies.Scripts.Behaviors;

namespace _Project.Features.Enemies.Scripts.Spawning
{
    [System.Serializable]
    public struct EnemySpawnWeight
    {
        public GameObject enemyPrefab;
        [Tooltip("Relative chance to spawn. e.g. 60 vs 40 acts as 60% vs 40%.")]
        public float weight;
    }

    public class EnemySpawner : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private List<EnemySpawnWeight> spawnableEnemies;
        [SerializeField] private float mapSpawnRadius = 100f;
        [SerializeField] private int maxSpawnAttempts = 10;

        [Header("Director")]
        [SerializeField] private int maxConcurrentEnemies = 25;
        [Tooltip("Fallback cost when an enemy prefab has no EnemyData/spawnCost.")]
        [SerializeField] private float defaultSpawnCost = 1f;
        [Tooltip("Cap how much unused budget can stockpile; prevents large bursts after lulls.")]
        [SerializeField] private float maxStockpiledBudget = 8f;
        [Tooltip("Minimum delay between spawns even when budget is rich.")]
        [SerializeField] private float minSpawnInterval = 0.25f;

        [Header("Team")]
        [Tooltip("Which team this spawner serves. Set per region in dual-region PvP scenes " +
                 "(Cleansers region → Cleansers, Thrive region → Thrive). Leave as None only " +
                 "for Coop-only scenes (server falls back to Cleansers and warns).")]
        [SerializeField] private TeamAffiliation OwnerTeam = TeamAffiliation.None;

        public TeamAffiliation Team => OwnerTeam;

        private float _budget;
        private float _lastSpawnTime;
        private readonly List<GameObject> _aliveEnemies = new List<GameObject>();
        private float _cheapestCostCache = -1f;
        private bool _warnedOwnerless;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            _cheapestCostCache = ComputeCheapestCost();

            if (OwnerTeam == TeamAffiliation.None)
            {
                // Coop / un-authored fallback. Warn loudly so PvP dual-region scenes don't
                // ship with unset spawners by accident.
                Debug.LogWarning($"[EnemySpawner] '{gameObject.scene.name}/{name}' has no OwnerTeam set in inspector. Falling back to Cleansers. " +
                                 "Set this explicitly per region for PvP dual-region maps.");
                OwnerTeam = TeamAffiliation.Cleansers;
                _warnedOwnerless = true;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            PruneDeadEnemies();

            float perSecond = DifficultyNetworkController.Singleton != null
                ? DifficultyNetworkController.Singleton.SpawnBudgetPerSecond
                : 1f;
            _budget = Mathf.Min(_budget + perSecond * Time.deltaTime, maxStockpiledBudget);

            if (_aliveEnemies.Count >= maxConcurrentEnemies) return;
            if (Time.time - _lastSpawnTime < minSpawnInterval) return;
            if (_cheapestCostCache <= 0f) return;
            if (_budget < _cheapestCostCache) return;

            GameObject prefab = GetRandomEnemyPrefab();
            if (prefab == null) return;

            float cost = GetSpawnCost(prefab);
            if (_budget < cost) return;

            GameObject instance = SpawnEnemyInstance(prefab);
            if (instance != null)
            {
                _budget -= cost;
                _lastSpawnTime = Time.time;
                _aliveEnemies.Add(instance);
            }
        }

        private void PruneDeadEnemies()
        {
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                if (_aliveEnemies[i] == null) _aliveEnemies.RemoveAt(i);
            }
        }

        private float ComputeCheapestCost()
        {
            float cheapest = float.MaxValue;
            if (spawnableEnemies == null) return -1f;
            foreach (var e in spawnableEnemies)
            {
                if (e.enemyPrefab == null) continue;
                float c = GetSpawnCost(e.enemyPrefab);
                if (c < cheapest) cheapest = c;
            }
            return cheapest == float.MaxValue ? -1f : cheapest;
        }

        private float GetSpawnCost(GameObject prefab)
        {
            if (prefab == null) return defaultSpawnCost;
            var ctrl = prefab.GetComponent<EnemyControllerBase>();
            if (ctrl != null)
            {
                var data = ctrl.GetEnemyData();
                if (data != null && data.spawnCost > 0f) return data.spawnCost;
            }
            return defaultSpawnCost;
        }

        private GameObject SpawnEnemyInstance(GameObject prefabToSpawn)
        {
            Vector3 spawnPos = transform.position;
            bool foundPosition = false;

            for (int i = 0; i < maxSpawnAttempts; i++)
            {
                Vector3 randomDirection = Random.insideUnitSphere * mapSpawnRadius;
                randomDirection += transform.position;

                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                Debug.LogWarning("EnemySpawner could not find a valid NavMesh position to spawn.");
                return null;
            }

            GameObject enemyInstance = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            // CharacterStats.Team intentionally left as None on enemies — preserves the
            // pre-existing weapon damage rule (a None-team participant can damage anyone).
            // Cross-team isolation is provided spatially by dual-region scene authoring +
            // TeamRegionRoot activation, not by team-flagging enemies.
            bool isCoop = _Project.Features.StageSystem.Scripts.RunNetworkController.Singleton != null &&
                          _Project.Features.StageSystem.Scripts.RunNetworkController.Singleton.activeRunMode.Value == GameMode.Coop;
            TeamPhase enemyPhase = isCoop ? TeamPhase.Both : OwnerTeam.ToPhase();

            if (enemyInstance.TryGetComponent<PhasedNetworkObject>(out var preSpawnPhased))
            {
                preSpawnPhased.InitialPhase = enemyPhase;
            }

            var netObj = enemyInstance.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn(true);
            return enemyInstance;
        }

        private GameObject GetRandomEnemyPrefab()
        {
            float totalWeight = 0f;
            foreach (var enemy in spawnableEnemies) totalWeight += enemy.weight;
            if (totalWeight <= 0f) return null;

            float randomValue = Random.Range(0, totalWeight);
            float currentWeight = 0f;
            foreach (var enemy in spawnableEnemies)
            {
                currentWeight += enemy.weight;
                if (randomValue <= currentWeight) return enemy.enemyPrefab;
            }
            return spawnableEnemies[spawnableEnemies.Count - 1].enemyPrefab;
        }
    }
}

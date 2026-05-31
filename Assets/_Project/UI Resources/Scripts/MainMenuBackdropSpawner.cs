using UnityEngine;

namespace _Project.UI_Resources.Scripts
{
    public class MainMenuBackdropSpawner : MonoBehaviour
    {
        private static readonly int IsWalking = Animator.StringToHash("IsWalking");
        private static readonly int Speed = Animator.StringToHash("Speed");

        [Header("References")]
        [SerializeField] private GameObject prefabToSpawn;
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;

        [Header("Settings")]
        [SerializeField] private float minSpawnRate = 2f;
        [SerializeField] private float maxSpawnRate = 6f;
        [SerializeField] private float walkSpeed = 2f;

        private float _nextSpawnTime;

        private void Start()
        {
            ScheduleNextSpawn();
        }

        private void Update()
        {
            if (Time.time >= _nextSpawnTime)
            {
                SpawnPrefab();
                ScheduleNextSpawn();
            }
        }

        private void ScheduleNextSpawn()
        {
            _nextSpawnTime = Time.time + Random.Range(minSpawnRate, maxSpawnRate);
        }

        private void SpawnPrefab()
        {
            if (prefabToSpawn == null || pointA == null || pointB == null) return;

            Vector3 startPos = pointA.position;
            Vector3 targetPos = pointB.position;
            Vector3 direction = (targetPos - startPos).normalized;
            Quaternion rotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity;

            GameObject instance = Instantiate(prefabToSpawn, startPos, rotation);
            
            BackdropWalker walker = instance.AddComponent<BackdropWalker>();
            walker.Initialize(targetPos, walkSpeed);

            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                // Try playing common walking animation parameters
                animator.SetFloat(Speed, 0.7f);
                animator.SetBool(IsWalking, true);
            }
        }
    }

    public class BackdropWalker : MonoBehaviour
    {
        private Vector3 _targetPosition;
        private float _speed;

        public void Initialize(Vector3 targetPosition, float speed)
        {
            _targetPosition = targetPosition;
            _speed = speed;
        }

        private void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _targetPosition) < 0.1f)
            {
                Destroy(gameObject);
            }
        }
    }
}


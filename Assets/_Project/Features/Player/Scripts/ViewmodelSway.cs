using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Features.Player.Scripts
{
    public class ViewmodelSway : MonoBehaviour
    {
        [Header("Sway Settings")]
        public float smooth = 8f;
        public float multiplier = 2f;

        [Header("Bob Settings")]
        public float bobSpeed = 10f;
        public float bobAmount = 0.05f;
        
        private Vector3 _originPosition;
        private float _bobTimer;

        private void Start() => _originPosition = transform.localPosition;

        public void UpdateSway(Vector2 mouseDelta, bool isMoving)
        {
            // 1. Calculate Sway (Rotation-based lag)
            float mouseX = mouseDelta.x * multiplier;
            float mouseY = mouseDelta.y * multiplier;

            Quaternion rotationX = Quaternion.AngleAxis(-mouseY, Vector3.right);
            Quaternion rotationY = Quaternion.AngleAxis(-mouseX, Vector3.up);
            Quaternion targetRotation = rotationX * rotationY;

            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smooth * Time.deltaTime);

            // 2. Calculate Bob (Movement-based wave)
            if (isMoving)
            {
                _bobTimer += Time.deltaTime * bobSpeed;
                Vector3 targetPos = _originPosition;
                targetPos.y += Mathf.Sin(_bobTimer) * bobAmount;
                targetPos.x += Mathf.Cos(_bobTimer / 2) * bobAmount;
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smooth);
            }
            else
            {
                _bobTimer = 0;
                transform.localPosition = Vector3.Lerp(transform.localPosition, _originPosition, Time.deltaTime * smooth);
            }
        }
    }
}
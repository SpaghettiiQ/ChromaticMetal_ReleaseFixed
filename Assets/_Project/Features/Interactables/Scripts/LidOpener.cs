using System.Collections;
using UnityEngine;

namespace _Project.Features.Interactables.Scripts
{
    public enum LidOpenMode { Flip, Slide }

    public class LidOpener : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Transform lid;
        [SerializeField] private LidOpenMode openMode = LidOpenMode.Flip;
        [SerializeField] private float duration = 0.5f;

        [Header("Flip Settings (Local X Axis)")]
        [SerializeField] private float flipDegreesX = -90f;

        [Header("Slide Settings (Local Y Axis)")]
        [SerializeField] private float slideAmountY = -1f;

        private bool _isOpen;

        public void OpenLid()
        {
            if (_isOpen) return;
            _isOpen = true;
            StartCoroutine(OpenRoutine());
        }

        public void OpenImmediate()
        {
            if (_isOpen) return;
            _isOpen = true;
            if (lid == null) return;
            
            if (openMode == LidOpenMode.Flip)
            {
                lid.localRotation *= Quaternion.Euler(flipDegreesX, 0, 0);
            }
            else if (openMode == LidOpenMode.Slide)
            {
                lid.localPosition += new Vector3(0, slideAmountY, 0);
            }
        }

        private IEnumerator OpenRoutine()
        {
            if (lid == null) yield break;

            float time = 0;
            Vector3 startPos = lid.localPosition;
            Quaternion startRot = lid.localRotation;

            Vector3 targetPos = startPos + (openMode == LidOpenMode.Slide ? new Vector3(0, slideAmountY, 0) : Vector3.zero);
            Quaternion targetRot = startRot * (openMode == LidOpenMode.Flip ? Quaternion.Euler(flipDegreesX, 0, 0) : Quaternion.identity);

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);

                if (openMode == LidOpenMode.Flip)
                {
                    lid.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                }
                else
                {
                    lid.localPosition = Vector3.Lerp(startPos, targetPos, t);
                }
                yield return null;
            }

            lid.localRotation = targetRot;
            lid.localPosition = targetPos;
        }
    }
}

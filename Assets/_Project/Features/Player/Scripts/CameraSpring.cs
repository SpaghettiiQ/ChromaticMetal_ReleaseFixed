using UnityEngine;

namespace _Project.Features.Player.Scripts
{
    /// <summary>
    /// A simple damped harmonic spring that settles toward zero. Used by <see cref="PlayerCamera"/>
    /// to drive procedural game-feel (jump kick, landing dip, etc.): impulses push the spring and it
    /// oscillates back to rest, giving the camera weight and bounce.
    ///
    /// Integrated with semi-implicit Euler, sub-stepped so high stiffness combined with a long frame
    /// can't overshoot into instability.
    /// </summary>
    [System.Serializable]
    public class CameraSpring
    {
        public Vector3 Value;
        public Vector3 Velocity;

        // Splitting each frame into fixed sub-steps keeps the integrator stable at the high
        // stiffness values that give a snappy, bouncy feel.
        private const int SubSteps = 6;

        /// <summary>Kick the spring with an instantaneous change in velocity.</summary>
        public void AddImpulse(Vector3 impulse) => Velocity += impulse;

        /// <summary>Advance the spring toward rest (zero) for this frame.</summary>
        public void Update(float dt, float stiffness, float damping)
        {
            if (dt <= 0f) return;

            float step = dt / SubSteps;
            for (int i = 0; i < SubSteps; i++)
            {
                Vector3 accel = -stiffness * Value - damping * Velocity;
                Velocity += accel * step;
                Value += Velocity * step;
            }
        }

        public void Reset()
        {
            Value = Vector3.zero;
            Velocity = Vector3.zero;
        }
    }
}

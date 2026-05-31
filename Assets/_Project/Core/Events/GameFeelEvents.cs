using System;
using UnityEngine;
using _Project.Core.Structs;

namespace _Project.Core.Events
{
    public static class GameFeelEvents
    {
        // Vector3: Recoil impact (x: pitch, y: yaw, z: roll)
        public static Action<Vector3> OnWeaponFiredRecoil;

        // float: intensity, float: duration
        public static Action<float, float> OnCameraShake;

        // Local SFX-feedback channels. Raised only on the client that owns the relevant party,
        // so subscribers (e.g. SfxManager) play 2D feedback without ownership checks.
        // Decoupled from CharacterEventBus.OnHit/OnKill, which carries item-proc logic.
        public static Action<DamageContext, bool /*wasKilled*/> OnLocalPlayerHit;
        public static Action<DamageContext> OnLocalPlayerHurt;

        // Extraction door charge state (raised on every peer from ExtractionTerminal when IsCharging
        // flips). Lets Items react to a StageSystem event without an illegal Items→StageSystem ref.
        // Danger Close (#10) listens; subscribers must self-guard server-authoritative work.
        public static Action OnExtractionChargeStarted;
        public static Action OnExtractionChargeStopped;
    }
}


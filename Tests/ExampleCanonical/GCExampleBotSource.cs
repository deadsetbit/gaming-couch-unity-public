// Canonical master for the generated GCExampleBot (the full example game's bot input).
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI, the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix
// (GCExampleBotSource -> GCExampleBot, and likewise for the player and ring), and injects the
// move/rename header.
//
// Everything from the first `using` below is what the user receives.
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    // Chooses stick and sprint input for a bot. GCExampleGame adds this to bot players and feeds
    // its choice to the same GCExamplePlayer.SetControls a controller uses, so a bot has the same
    // speed, stamina and collisions as a human.
    //
    // A bot drifts around inside the target circle, heads back toward the centre when the ring
    // gets close, and now and then sprints at a nearby opponent. To feel less like a machine, it
    // swings its stick gradually, its thumb wobbles a little, and each bot gets its own caution,
    // cruising speed and aggression from its seed.
    public class GCExampleBotSource : MonoBehaviour
    {
        // How far the stick can move per second. The stick spans 2 from full left to full right.
        [SerializeField]
        private float stickSpeed = 4f;

        // How far the bot's thumb drifts off the stick direction it wants.
        [SerializeField]
        private float wobble = 0.4f;

        [SerializeField]
        private float chargeRange = 4f;

        [SerializeField]
        private float chargeSeconds = 0.6f;

        private System.Random random;
        private float edgeMargin;
        // How far the bot pushes the stick when it is not in a hurry, from 0 to 1.
        private float cruise;
        private float chargeChancePerSecond;
        private float wobbleOffset;
        private Vector2 stick;
        private GCExamplePlayerSource chargeTarget;
        private float chargeSecondsLeft;
        private Vector2 wanderPoint;
        private float wanderSecondsLeft;

        public void Init(int seed)
        {
            random = new System.Random(seed);
            edgeMargin = RandomRange(0.8f, 2f);
            cruise = RandomRange(0.4f, 0.9f);
            chargeChancePerSecond = RandomRange(0.15f, 0.6f);
            wobbleOffset = RandomRange(0f, 100f);
        }

        public void Decide(
            GCExamplePlayerSource self,
            GCExampleRingSource ring,
            IReadOnlyList<GCExamplePlayerSource> players,
            float deltaSeconds,
            out Vector2 move,
            out bool sprint
        )
        {
            var wanted = ChooseStick(self, ring, players, deltaSeconds, out sprint);

            var wobbleTime = Time.time * 0.7f + wobbleOffset;
            wanted += new Vector2(Mathf.PerlinNoise(wobbleTime, 0f) - 0.5f, Mathf.PerlinNoise(0f, wobbleTime) - 0.5f) * wobble;
            stick = Vector2.MoveTowards(stick, Vector2.ClampMagnitude(wanted, 1f), stickSpeed * deltaSeconds);
            move = stick;
        }

        private Vector2 ChooseStick(
            GCExamplePlayerSource self,
            GCExampleRingSource ring,
            IReadOnlyList<GCExamplePlayerSource> players,
            float deltaSeconds,
            out bool sprint
        )
        {
            var position = self.Position;
            var hasStamina = self.Stamina > 0f;

            if (ring.Radius - (position.magnitude + self.Radius) < edgeMargin)
            {
                sprint = hasStamina;
                return -position.normalized;
            }

            chargeSecondsLeft -= deltaSeconds;
            if (chargeSecondsLeft <= 0f && random.NextDouble() < chargeChancePerSecond * deltaSeconds)
            {
                chargeTarget = FindNearestOpponent(self, players);
                chargeSecondsLeft = chargeSeconds;
            }

            if (chargeSecondsLeft > 0f && chargeTarget != null && chargeTarget.gameObject.activeSelf)
            {
                sprint = hasStamina;
                return (chargeTarget.Position - position).normalized;
            }

            sprint = false;
            var toWanderPoint = wanderPoint - position;
            wanderSecondsLeft -= deltaSeconds;
            if (wanderSecondsLeft <= 0f || toWanderPoint.magnitude < self.Radius)
            {
                var angle = RandomRange(0f, 2f * Mathf.PI);
                var distance = RandomRange(0f, Mathf.Max(0f, ring.TargetRadius * 0.8f - self.Radius));
                wanderPoint = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                wanderSecondsLeft = RandomRange(1.5f, 4f);
                toWanderPoint = wanderPoint - position;
            }

            // Ease off the stick when close, the way a player slows down to stop on a spot.
            return toWanderPoint.normalized * cruise * Mathf.Clamp01(toWanderPoint.magnitude);
        }

        private GCExamplePlayerSource FindNearestOpponent(
            GCExamplePlayerSource self,
            IReadOnlyList<GCExamplePlayerSource> players
        )
        {
            GCExamplePlayerSource nearest = null;
            var nearestDistance = chargeRange;
            foreach (var player in players)
            {
                var distance = Vector2.Distance(player.Position, self.Position);
                if (player != self && distance < nearestDistance)
                {
                    nearest = player;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}

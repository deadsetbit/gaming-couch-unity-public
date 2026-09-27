// Canonical master for the generated GCExamplePlayer (the full example game's player).
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI — the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix
// (GCExamplePlayerSource -> GCExamplePlayer), and injects the move/rename header. The "Source"
// suffix keeps this master's simple type name distinct from the generated GCExamplePlayer so the
// FindTypeByName guard in GamingCouchActiveSceneSetup does not refuse generation.
//
// Everything from the first `using` below is what the user receives.
using System.Collections.Generic;
using DSB.GC;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    // One ball. GCExampleGame passes it stick and sprint input with SetControls, from a controller
    // or from GCExampleBot, and the ball moves the same way for both.
    //
    // Holding sprint raises the top speed and acceleration until the stamina runs out. Stamina
    // recharges only while sprint is released, so holding it at empty keeps the ball at normal speed.
    //
    // Balls also repel each other like reversed gravity: the push weakens with the square of the
    // distance between them, so crowded balls keep drifting apart and have to fight for space. A
    // line between two balls shows how hard they push.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class GCExamplePlayerSource : GCPlayer
    {
        [SerializeField]
        private Renderer colorRenderer;

        [SerializeField]
        private float topSpeed = 5f;

        // Units per second squared. A low limit gives the ball some momentum and lets a bump push
        // it further before it steers back.
        [SerializeField]
        private float acceleration = 20f;

        [SerializeField]
        private float sprintSpeedMultiplier = 1.5f;

        [SerializeField]
        private float sprintAcceleration = 35f;

        [SerializeField]
        private float sprintSeconds = 2f;

        [SerializeField]
        private float rechargeSeconds = 3f;

        [SerializeField]
        private float bounciness = 0.8f;

        // Speed at which two touching balls drift apart. At twice that distance it is a quarter.
        [SerializeField]
        private float repulsionSpeed = 3f;

        // Beyond this distance balls do not push, so a crowd that has spread out comes to rest.
        [SerializeField]
        private float repulsionRange = 2.5f;

        [SerializeField]
        private bool showForceLines = true;

        [SerializeField]
        private float forceLineWidth = 0.1f;

        private Rigidbody2D body;
        private CircleCollider2D circle;
        private Vector2 move;
        private bool isSprintHeld;
        private readonly List<Collider2D> nearbyColliders = new List<Collider2D>();
        private readonly List<GCExamplePlayerSource> nearbyBalls = new List<GCExamplePlayerSource>();
        private readonly List<LineRenderer> forceLines = new List<LineRenderer>();
        private Material forceLineMaterial;

        // From 0 (empty) to 1 (full).
        public float Stamina { get; private set; } = 1f;
        public bool IsSprinting => isSprintHeld && Stamina > 0f;
        public Vector2 Position => body.position;
        public float Radius => circle.radius * transform.lossyScale.x;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            circle = GetComponent<CircleCollider2D>();
            circle.sharedMaterial = new PhysicsMaterial2D { bounciness = bounciness, friction = 0f };
        }

        public void ApplyPlayerColor()
        {
            if (colorRenderer == null)
            {
                colorRenderer = GetComponentInChildren<Renderer>();
            }

            // Sprites/Default shows its tint in every render pipeline, unlike the pipeline's default
            // mesh material, which the 2D renderer draws without it.
            if (colorRenderer != null)
            {
                colorRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = ColorBase };
            }
        }

        public void SetControls(Vector2 stick, bool sprint)
        {
            move = Vector2.ClampMagnitude(stick, 1f);
            isSprintHeld = sprint;
        }

        // Changes the ball's velocity at once, as the ring's reset blast does. The ball then steers
        // back at its normal acceleration.
        public void Knock(Vector2 velocityChange)
        {
            body.AddForce(velocityChange * body.mass, ForceMode2D.Impulse);
        }

        private void FixedUpdate()
        {
            var sprinting = IsSprinting;
            if (sprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - Time.fixedDeltaTime / sprintSeconds);
            }
            else if (!isSprintHeld)
            {
                Stamina = Mathf.Min(1f, Stamina + Time.fixedDeltaTime / rechargeSeconds);
            }

            // Steer toward the stick's velocity plus the push from nearby balls, changing speed by at
            // most the acceleration limit. Adding the push to the steering target, rather than as a
            // separate force, keeps a ball that is standing still from braking against it.
            var stickVelocity = move * (sprinting ? topSpeed * sprintSpeedMultiplier : topSpeed);
            var targetVelocity = stickVelocity + GetRepulsion();
            var neededAcceleration = (targetVelocity - body.linearVelocity) / Time.fixedDeltaTime;
            var limit = sprinting ? sprintAcceleration : acceleration;
            body.AddForce(Vector2.ClampMagnitude(neededAcceleration, limit) * body.mass);
        }

        private Vector2 GetRepulsion()
        {
            var repulsion = Vector2.zero;
            FindNearbyBalls();
            foreach (var other in nearbyBalls)
            {
                var away = body.position - other.Position;
                repulsion += away.normalized * repulsionSpeed * GetRepulsionFalloff(away.magnitude);
            }

            return repulsion;
        }

        // 1 for touching balls, a quarter at twice that distance, and so on.
        private float GetRepulsionFalloff(float distance)
        {
            var touchingDistance = Radius * 2f;
            var clampedDistance = Mathf.Max(distance, touchingDistance);
            return touchingDistance * touchingDistance / (clampedDistance * clampedDistance);
        }

        private void FindNearbyBalls()
        {
            nearbyBalls.Clear();
            Physics2D.OverlapCircle(body.position, repulsionRange, new ContactFilter2D().NoFilter(), nearbyColliders);
            foreach (var other in nearbyColliders)
            {
                var ball = other.GetComponent<GCExamplePlayerSource>();
                if (ball != null && ball != this)
                {
                    nearbyBalls.Add(ball);
                }
            }
        }

        // Draws a line from edge to edge between this ball and each nearby ball, brighter and wider
        // the harder they push apart. The ball with the lower index draws each pair's line.
        private void LateUpdate()
        {
            var lineCount = 0;
            if (showForceLines)
            {
                FindNearbyBalls();
                foreach (var other in nearbyBalls)
                {
                    if (other.Index <= Index)
                    {
                        continue;
                    }

                    var towardOther = (other.Position - body.position).normalized;
                    var strength = GetRepulsionFalloff(Vector2.Distance(body.position, other.Position));
                    var line = GetForceLine(lineCount++);
                    line.SetPosition(0, body.position + towardOther * Radius);
                    line.SetPosition(1, other.Position - towardOther * other.Radius);
                    line.startColor = new Color(ColorBase.r, ColorBase.g, ColorBase.b, strength);
                    line.endColor = new Color(other.ColorBase.r, other.ColorBase.g, other.ColorBase.b, strength);
                    line.widthMultiplier = forceLineWidth * (0.3f + 0.7f * strength);
                }
            }

            for (var i = 0; i < forceLines.Count; i++)
            {
                forceLines[i].enabled = i < lineCount;
            }
        }

        private LineRenderer GetForceLine(int lineIndex)
        {
            if (lineIndex < forceLines.Count)
            {
                return forceLines[lineIndex];
            }

            if (forceLineMaterial == null)
            {
                forceLineMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            var line = new GameObject("Force line").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = forceLineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            forceLines.Add(line);
            return line;
        }
    }
}

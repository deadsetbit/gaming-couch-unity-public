// Canonical master for the generated GCExampleRing (the full example game's shrinking boundary).
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI, the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix
// (GCExampleRingSource -> GCExampleRing), and injects the move/rename header.
//
// Everything from the first `using` below is what the user receives.
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    // The lethal circle around the arena's centre. Each cycle waits a moment at full size, then
    // shrinks toward a target radius, slowing down as it gets close. When it reaches the target it
    // jumps straight back to full size with a blast that knocks the balls out from the centre, and
    // the next cycle begins. The last cycle closes to zero.
    //
    // Each cycle's target radius and shrink time are listed for eight players and scaled by the
    // square root of (players left / 8) when the cycle starts, so fewer players get a smaller,
    // faster ring. A faint inner circle shows the current target.
    //
    // GCExampleGame drives the ring with Step once per physics step, and calls
    // PauseForElimination when a ball goes out.
    public class GCExampleRingSource : MonoBehaviour
    {
        private const int MaxPlayers = 8;

        [SerializeField]
        private float fullRadius = 8f;

        [SerializeField]
        private float breathingSeconds = 2f;

        [SerializeField]
        private float eliminationPauseSeconds = 0.5f;

        // Speed the reset blast gives a ball at the centre, fading to nothing at the full radius.
        [SerializeField]
        private float resetBlastSpeed = 10f;

        // How long the blast's white circle takes to grow from the centre to the full radius.
        [SerializeField]
        private float blastEffectSeconds = 0.6f;

        [System.Serializable]
        public struct RingCycle
        {
            public float targetRadius;
            public float shrinkSeconds;

            public RingCycle(float targetRadius, float shrinkSeconds)
            {
                this.targetRadius = targetRadius;
                this.shrinkSeconds = shrinkSeconds;
            }
        }

        // Tuned for eight players. Later targets are too small for every ball to fit, so the ring
        // itself forces players out. With these values an eight-player game ends within 60 seconds,
        // even with an elimination pause for every ball that goes out.
        [SerializeField]
        private RingCycle[] cycles =
        {
            new RingCycle(5f, 14f),
            new RingCycle(2.4f, 12f),
            new RingCycle(1.4f, 10f),
            new RingCycle(0f, 8f),
        };

        [SerializeField]
        private Color boundaryColor = new Color(1f, 0.3f, 0.25f);

        [SerializeField]
        private Color flashColor = Color.white;

        [SerializeField]
        private Color previewColor = new Color(1f, 1f, 1f, 0.2f);

        private int cycle;
        private float cycleSeconds;
        private float cycleShrinkSeconds;
        private float pauseSecondsLeft;
        private float blastEffectSecondsLeft;
        private LineRenderer boundaryLine;
        private LineRenderer previewLine;
        private LineRenderer blastLine;

        public float FullRadius => fullRadius;
        public float Radius { get; private set; }
        public float TargetRadius { get; private set; }
        public int Cycle => cycle;
        public bool IsPaused => pauseSecondsLeft > 0f;

        private void Awake()
        {
            Radius = fullRadius;
            TargetRadius = fullRadius;

            var material = new Material(Shader.Find("Sprites/Default"));
            boundaryLine = CreateLine("Boundary", 0.15f, material);
            previewLine = CreateLine("Target", 0.05f, material);
            blastLine = CreateLine("Blast", 0.3f, material);
        }

        public void Begin(int playerCount)
        {
            cycle = 0;
            pauseSecondsLeft = 0f;
            blastEffectSecondsLeft = 0f;
            StartCycle(playerCount);
        }

        // Returns true on the step the ring resets to full size for a new cycle. playerCount is only
        // read when a new cycle starts, so a cycle's target and timing never change halfway through.
        public bool Step(float deltaSeconds, int playerCount)
        {
            if (pauseSecondsLeft > 0f)
            {
                pauseSecondsLeft -= deltaSeconds;
                return false;
            }

            cycleSeconds += deltaSeconds;
            var shrinkProgress = Mathf.Clamp01((cycleSeconds - breathingSeconds) / cycleShrinkSeconds);
            // Starts at 1.5 times the average speed and slows to half of it, so the ring eases in
            // without crawling near the target.
            var easedProgress = 1.5f * shrinkProgress - 0.5f * shrinkProgress * shrinkProgress;
            Radius = Mathf.Lerp(fullRadius, TargetRadius, easedProgress);

            var isLastCycle = cycle == cycles.Length - 1;
            if (shrinkProgress < 1f || isLastCycle)
            {
                return false;
            }

            cycle++;
            StartCycle(playerCount);
            blastEffectSecondsLeft = blastEffectSeconds;
            return true;
        }

        // The knock a ball gets when the ring resets: straight out from the centre, hardest near it.
        public Vector2 GetResetBlast(Vector2 position)
        {
            var falloff = Mathf.Clamp01(1f - position.magnitude / fullRadius);
            return position.normalized * resetBlastSpeed * falloff;
        }

        // Stops the shrinking for a moment and flashes the ring. Another elimination during the
        // pause shares it rather than making it longer.
        public void PauseForElimination()
        {
            if (pauseSecondsLeft <= 0f)
            {
                pauseSecondsLeft = eliminationPauseSeconds;
            }
        }

        // A ball is out as soon as any part of it reaches the ring.
        public bool IsTouching(Vector2 position, float ballRadius)
        {
            return position.magnitude + ballRadius >= Radius;
        }

        private void StartCycle(int playerCount)
        {
            var scale = Mathf.Sqrt(Mathf.Clamp(playerCount, 1, MaxPlayers) / (float)MaxPlayers);
            TargetRadius = cycles[cycle].targetRadius * scale;
            cycleShrinkSeconds = cycles[cycle].shrinkSeconds * scale;
            cycleSeconds = 0f;
            Radius = fullRadius;
        }

        private void LateUpdate()
        {
            var isFlashOn = IsPaused && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
            DrawCircle(boundaryLine, Radius, isFlashOn ? flashColor : boundaryColor);
            DrawCircle(previewLine, TargetRadius, previewColor);

            // The blast is a white circle that starts bright at the centre and fades as it grows.
            blastEffectSecondsLeft -= Time.deltaTime;
            blastLine.enabled = blastEffectSecondsLeft > 0f;
            if (blastLine.enabled)
            {
                var progress = 1f - blastEffectSecondsLeft / blastEffectSeconds;
                var grownRadius = fullRadius * (1f - (1f - progress) * (1f - progress));
                DrawCircle(blastLine, grownRadius, new Color(1f, 1f, 1f, 1f - progress));
            }
        }

        private LineRenderer CreateLine(string lineName, float width, Material material)
        {
            var line = new GameObject(lineName).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = width;
            line.positionCount = 64;
            return line;
        }

        private static void DrawCircle(LineRenderer line, float radius, Color color)
        {
            line.startColor = color;
            line.endColor = color;
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = i * 2f * Mathf.PI / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }
    }
}

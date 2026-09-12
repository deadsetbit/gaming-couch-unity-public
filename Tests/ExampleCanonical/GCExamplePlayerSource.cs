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
using DSB.GC;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    public class GCExamplePlayerSource : GCPlayer
    {
        [SerializeField]
        private Renderer colorRenderer;

        private void Reset()
        {
            FindColorRenderer();
        }

        private void OnValidate()
        {
            FindColorRenderer();
        }

        private void Start()
        {
            ApplyPlayerColor();
        }

        public void ApplyPlayerColor()
        {
            FindColorRenderer();

            if (colorRenderer != null)
            {
                colorRenderer.material.color = ColorBase;
            }
        }

        public override string GetHudValueText()
        {
            return Score.ToString();
        }

        private void FindColorRenderer()
        {
            if (colorRenderer == null)
            {
                colorRenderer = GetComponentInChildren<Renderer>();
            }
        }
    }
}

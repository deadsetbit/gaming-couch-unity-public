using DSB.GC;
using UnityEngine;

// Lives in the runtime GamingCouch.Tests.Fixtures assembly (not the editor test assembly) and in its
// own matching-name file so EnsureExamplePlayerPrefab can attach it to a real saved prefab and wire
// colorRenderer. Unity refuses to attach an editor-assembly MonoBehaviour to a prefab ("... because
// it is an editor script") and can only serialize a component that has a MonoScript (one per
// matching-name .cs file). In production the player type is always the generated runtime
// GCExamplePlayer, so this fixture mirrors that by being a runtime type too.
public sealed class ColorPlaceholderPrefabPlayer : GCPlayer
{
    [SerializeField]
    private Renderer colorRenderer;
}

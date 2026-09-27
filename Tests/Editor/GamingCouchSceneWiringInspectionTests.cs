using System.Collections.Generic;
using DSB.GC;
using NUnit.Framework;
using UnityEngine.SceneManagement;

// The boundaries of what the Start Screen counts as the active scene's GamingCouch objects.
// SelectGamingCouchesForInspection turns on one property: whether the object's scene is one
// SceneManager enumerates. The DontDestroyOnLoad scene is not, an additively loaded scene is, and
// both are driven here through the enumerated-scene set. The real Play Mode move is driven for real
// in GamingCouchStartScreenPlayModeReadinessTests.
public sealed class GamingCouchSceneWiringInspectionTests
{
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        GamingCouchEditorTestSupport.DestroyTrackedObjects(objectsToDestroy);
    }

    [Test]
    public void InspectionCountsAGamingCouchOutsideEveryEnumeratedScene()
    {
        var survivor = CreateGamingCouch();

        var selected = GamingCouchSceneWiring.SelectGamingCouchesForInspection(
            new GamingCouch[0],
            new[] { survivor },
            new Scene[0]
        );

        Assert.That(selected, Is.EqualTo(new[] { survivor }));
    }

    [Test]
    public void InspectionIgnoresAGamingCouchInAnEnumeratedSceneOtherThanTheActiveOne()
    {
        var elsewhere = CreateGamingCouch();

        var selected = GamingCouchSceneWiring.SelectGamingCouchesForInspection(
            new GamingCouch[0],
            new[] { elsewhere },
            new[] { elsewhere.gameObject.scene }
        );

        Assert.That(selected, Is.Empty);
    }

    [Test]
    public void InspectionKeepsTheActiveSceneObjectsOnceEach()
    {
        var inActiveScene = CreateGamingCouch();

        var selected = GamingCouchSceneWiring.SelectGamingCouchesForInspection(
            new[] { inActiveScene },
            new[] { inActiveScene },
            new Scene[0]
        );

        Assert.That(selected, Is.EqualTo(new[] { inActiveScene }));
    }

    private GamingCouch CreateGamingCouch()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        objectsToDestroy.Add(gamingCouch.gameObject);
        return gamingCouch;
    }
}

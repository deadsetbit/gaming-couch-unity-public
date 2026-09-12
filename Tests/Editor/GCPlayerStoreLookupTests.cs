using System.Collections.Generic;
using DSB.GC;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;

public sealed class GCPlayerStoreLookupTests
{
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        GCRuntimeMessageOutput.ResetForTests(() => 0);
        GCLog.logLevel = LogLevel.None;
    }

    [TearDown]
    public void TearDown()
    {
        GamingCouchEditorTestSupport.DestroyTrackedObjects(objectsToDestroy);
        GCRuntimeMessageOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
    }

    [Test]
    public void GetPlayerByIndexReturnsPlayerRegisteredUnderThatIndex()
    {
        var playerZero = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var playerTwo = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 2);
        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(playerZero);
        store.AddPlayer(playerTwo);

        Assert.That(store.GetPlayerByIndex(0), Is.SameAs(playerZero));
        Assert.That(store.GetPlayerByIndex(2), Is.SameAs(playerTwo));
    }

    [Test]
    public void GetPlayerByIndexReturnsNullForGapInIndicesRatherThanListPositionPlayer()
    {
        // Indices 0 and 2 occupy list positions 0 and 1. Index 1 is unregistered.
        // The removed list-position fallback used to return the index-2 player (list slot 1).
        var playerZero = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var playerTwo = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 2);
        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(playerZero);
        store.AddPlayer(playerTwo);

        Assert.That(store.GetPlayerByIndex(1), Is.Null);
    }

    [Test]
    public void GetPlayerByIndexReturnsNullForOutOfRangeIndexWithoutThrowing()
    {
        var playerZero = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(playerZero);

        // The removed list-position fallback threw ArgumentOutOfRangeException here.
        Assert.That(store.GetPlayerByIndex(99), Is.Null);
    }
}

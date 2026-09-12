using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;

public sealed class GCRuntimeObsoleteMigrationMarkerTests
{
    private static readonly ExpectedMarker[] ExpectedMarkers =
    {
        Marker("T:DSB.GC.Hud.GCNameTag", "GCPlayerOverhead"),
        Marker("P:DSB.GC.GCPlayer.Id", "GCPlayer.Index"),
        Marker("P:DSB.GC.GCPlayer.PlayerName", "Player names are platform-owned"),
        Marker("P:DSB.GC.GCPlayer.LastSetEliminatedTime", "LastSetEliminatedGameTime", "LastSetEliminatedPermanentGameTime", "LastSetEliminatedRevokableGameTime"),
        Marker("P:DSB.GC.GCPlayer.LastSetUneliminatedTime", "LastSetRevokeEliminatedGameTime"),
        Marker("P:DSB.GC.GCPlayer.FinishedTime", "LastSetFinishedGameTime", "LastSetFinishedPermanentGameTime", "LastSetFinishedRevokableGameTime"),
        Marker("M:DSB.GC.GCPlayer.SetEliminated(System.String)", "SetEliminatedPermanent", "SetEliminatedRevokable"),
        Marker("M:DSB.GC.GCPlayer.SetUneliminated(System.String)", "SetRevokeEliminated"),
        Marker("M:DSB.GC.GCPlayer.SetFinished(System.String)", "SetFinishedPermanent", "SetFinishedRevokable"),
        Marker("P:DSB.GC.GCPlayerStore`1.PlayersEnumerable", "Players"),
        Marker("P:DSB.GC.GCPlayerStore`1.PlayerCount", "Players.Count"),
        Marker("P:DSB.GC.GCPlayerStore`1.UneliminatedPlayers", "PlayersUneliminated", "eliminationState is None"),
        Marker("P:DSB.GC.GCPlayerStore`1.UneliminatedPlayerCount", "PlayersUneliminated.Count"),
        Marker("P:DSB.GC.GCPlayerStore`1.UneliminatedPlayersEnumerable", "PlayersUneliminated", "eliminationState is None"),
        Marker("P:DSB.GC.GCPlayerStore`1.UneliminatedNonBotPlayers", "PlayersUneliminatedNonBot"),
        Marker("P:DSB.GC.GCPlayerStore`1.UneliminatedNonBotPlayersEnumerable", "PlayersUneliminatedNonBot"),
        Marker("P:DSB.GC.GCPlayerStore`1.UneliminatedBotPlayers", "PlayersUneliminatedBot"),
        Marker("P:DSB.GC.GCPlayerStore`1.UneliminatedBotPlayersEnumerable", "PlayersUneliminatedBot"),
        Marker("P:DSB.GC.GCPlayerStore`1.EliminatedPlayers", "PlayersEliminated", "permanent and revokable"),
        Marker("P:DSB.GC.GCPlayerStore`1.EliminatedPlayerCount", "PlayersEliminated.Count"),
        Marker("P:DSB.GC.GCPlayerStore`1.EliminatedPlayersEnumerable", "PlayersEliminated", "permanent and revokable"),
        Marker("P:DSB.GC.GCPlayerStore`1.EliminatedNonBotPlayers", "PlayersEliminatedNonBot"),
        Marker("P:DSB.GC.GCPlayerStore`1.EliminatedNonBotPlayersEnumerable", "PlayersEliminatedNonBot"),
        Marker("P:DSB.GC.GCPlayerStore`1.EliminatedBotPlayers", "PlayersEliminatedBot"),
        Marker("P:DSB.GC.GCPlayerStore`1.EliminatedBotPlayersEnumerable", "PlayersEliminatedBot"),
        Marker("M:DSB.GC.GCPlayerStore`1.GetPlayerById(System.Int32)", "GetPlayerByIndex"),
        Marker("P:GCPlayerStoreOutput`1.PlayerCount", "Players.Count"),
        Marker("P:GCPlayerStoreOutput`1.PlayersEnumerable", "Players"),
        Marker("P:GCPlayerStoreOutput`1.UneliminatedPlayersEnumerable", "PlayersUneliminated", "eliminationState is None"),
        Marker("P:GCPlayerStoreOutput`1.UneliminatedPlayerCount", "PlayersUneliminated.Count"),
        Marker("P:GCPlayerStoreOutput`1.EliminatedPlayersEnumerable", "PlayersEliminated", "permanent and revokable"),
        Marker("P:GCPlayerStoreOutput`1.EliminatedPlayerCount", "PlayersEliminated.Count"),
        Marker("M:GCPlayerStoreOutput`1.GetPlayerById(System.Int32)", "GetPlayerByIndex"),
        Marker("M:DSB.GC.GamingCouch.GetInputsByPlayerId(System.Int32)", "GetInputsByPlayerIndex"),
        Marker("M:DSB.GC.Hud.GCHud.UpdatePlayers(DSB.GC.Hud.GCPlayersHudData)", "GCPlayer score/lives/status/meter APIs", "runtime_messages state snapshots"),
        Marker("M:DSB.GC.Hud.GCHud.UpdateScreenPointHud(DSB.GC.Hud.GCScreenPointData)", "QueuePointData")
    };

    [Test]
    public void RuntimeObsoleteMigrationMarkersAreHardErrorsWithGuidance()
    {
        var actualMarkers = FindRuntimeObsoleteMarkers();
        var actualIds = actualMarkers.Keys.OrderBy(id => id).ToArray();
        var expectedIds = ExpectedMarkers.Select(marker => marker.Id).OrderBy(id => id).ToArray();

        Assert.That(actualIds, Is.EqualTo(expectedIds));

        foreach (var expectedMarker in ExpectedMarkers)
        {
            var obsolete = actualMarkers[expectedMarker.Id];
            Assert.That(obsolete.IsError, Is.True, expectedMarker.Id);

            foreach (var guidanceFragment in expectedMarker.GuidanceFragments)
            {
                Assert.That(obsolete.Message, Does.Contain(guidanceFragment), expectedMarker.Id);
            }
        }
    }

    private static Dictionary<string, ObsoleteAttribute> FindRuntimeObsoleteMarkers()
    {
        var markers = new Dictionary<string, ObsoleteAttribute>();
        var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        foreach (var type in typeof(GCPlayer).Assembly.GetTypes())
        {
            AddMarker(markers, TypeId(type), type);

            foreach (var member in type.GetMembers(bindingFlags))
            {
                if (member is Type)
                {
                    continue;
                }

                if (member is MethodInfo method && method.IsSpecialName)
                {
                    continue;
                }

                AddMarker(markers, MemberId(member), member);
            }
        }

        return markers;
    }

    private static void AddMarker(Dictionary<string, ObsoleteAttribute> markers, string id, MemberInfo member)
    {
        var obsolete = member.GetCustomAttribute<ObsoleteAttribute>(false);
        if (obsolete == null)
        {
            return;
        }

        markers.Add(id, obsolete);
    }

    private static string TypeId(Type type)
    {
        return "T:" + type.FullName;
    }

    private static string MemberId(MemberInfo member)
    {
        var declaringTypeName = member.DeclaringType.FullName;
        if (member is MethodBase method)
        {
            var parameterTypes = string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName));
            return "M:" + declaringTypeName + "." + member.Name + "(" + parameterTypes + ")";
        }

        if (member.MemberType == MemberTypes.Property)
        {
            return "P:" + declaringTypeName + "." + member.Name;
        }

        if (member.MemberType == MemberTypes.Field)
        {
            return "F:" + declaringTypeName + "." + member.Name;
        }

        if (member.MemberType == MemberTypes.Event)
        {
            return "E:" + declaringTypeName + "." + member.Name;
        }

        return member.MemberType + ":" + declaringTypeName + "." + member.Name;
    }

    private static ExpectedMarker Marker(string id, params string[] guidanceFragments)
    {
        return new ExpectedMarker(id, guidanceFragments);
    }

    private sealed class ExpectedMarker
    {
        public readonly string Id;
        public readonly string[] GuidanceFragments;

        public ExpectedMarker(string id, string[] guidanceFragments)
        {
            Id = id;
            GuidanceFragments = guidanceFragments;
        }
    }
}

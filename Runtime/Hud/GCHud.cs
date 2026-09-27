using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DSB.GC.RuntimeMessages;
using UnityEngine;

namespace DSB.GC.Hud
{
    public enum PlayersHudValueType
    {
        None,
        PointsSmall,
        Status,
        Text,
        Lives
    }

    public enum PlayersHudMeterType
    {
        None,
        Bar,
    }

    /// <summary>
    /// Configuration for the players hud.
    /// </summary>
    [Serializable]
    public struct GCHudPlayersConfig : ISerializationCallbackReceiver
    {
        [NonSerialized]
        public PlayersHudValueType valueTypeEnum;
        [SerializeField]
        private string valueType;

        [NonSerialized]
        public PlayersHudMeterType meterTypeEnum;
        [SerializeField]
        private string meterType;

        public void OnBeforeSerialize()
        {
            var str = valueTypeEnum.ToString();
            valueType = char.ToLower(str[0]) + str[1..]; // set first letter to lowercase

            str = meterTypeEnum.ToString();
            meterType = char.ToLower(str[0]) + str[1..]; // set first letter to lowercase
        }

        public void OnAfterDeserialize()
        {
            valueTypeEnum = (PlayersHudValueType)Enum.Parse(typeof(PlayersHudValueType), valueType);
            meterTypeEnum = (PlayersHudMeterType)Enum.Parse(typeof(PlayersHudMeterType), meterType);
        }
    }

    [Serializable]
    public struct GCHudConfig
    {
        public GCHudPlayersConfig players;
    }

    [Serializable]
    public struct GCPlayersHudDataPlayer
    {
        /// <summary>
        /// Player index.
        /// </summary>
        public int playerIndex;
        /// <summary>
        /// Current runtime score for the player.
        /// </summary>
        public int score;
        /// <summary>
        /// Current runtime lives for the player.
        /// </summary>
        public int lives;
        /// <summary>
        /// Current runtime status enum name for the player.
        /// </summary>
        public string status;
        /// <summary>
        /// Current runtime status text for the player.
        /// </summary>
        public string statusText;
        /// <summary>
        /// Current runtime elimination state enum name for the player.
        /// </summary>
        public string eliminationState;
        /// <summary>
        /// Current runtime finish state enum name for the player.
        /// </summary>
        public string finishState;
        /// <summary>
        /// Compatibility projection for older HUD receivers. Prefer eliminationState.
        /// </summary>
        public bool eliminated;
        /// <summary>
        /// One-based runtime placement for the player. 1 is first place, 2 is second place, etc.
        /// Players in the HUD will be sorted based on this value to indicate placements at given time.
        /// </summary>
        public int placement;
        /// <summary>
        /// The value to display for the player. This can be points, lives, etc. based on the valueType in the GCHudPlayersConfig.
        /// </summary>
        public string value;
        /// <summary>
        /// The meter value (-1-100) to display for the player. This can be health, boost, etc. based on the meterType in the GCHudPlayersConfig.
        /// </summary>
        public int meter;
    }

    [Serializable]
    public struct GCPlayersHudData
    {
        /// <summary>
        /// The players to display in the HUD.
        /// </summary>
        public GCPlayersHudDataPlayer[] players;
    }

    [Serializable]
    public struct GCScreenPointDataPoint
    {
        public string type;
        /// <summary>
        /// Player index.
        /// </summary>
        public int playerIndex;
        /// <summary>
        /// The x position of the point in percentages eg. 0-1. Values outside this range are considered off screen but not disregarded.
        /// </summary>
        public float x;
        /// <summary>
        /// The y position of the point in percentages eg. 0-1. Values outside this range are considered off screen but not disregarded.
        /// </summary>
        public float y;
        /// <summary>
        /// If the point is off screen. When point is off screen, the X or Y coordinates are still clamped to 0-1.
        /// </summary>
        public bool isOffScreen;
    }

    [Serializable]
    public struct GCScreenPointData
    {
        /// <summary>
        /// List of points to display in the HUD.
        /// </summary>
        public GCScreenPointDataPoint[] points;
    }

    public class GCHud
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchSetupHud(string hudConfigJson);

        private Camera camera = null;
        public Camera Camera
        {
            get
            {
                if (camera == null)
                {
                    camera = Camera.main;
                }

                return camera;
            }
        }

        /// <summary>
        /// Setup the HUD. Should be called once at the start of the game and before UpdatePlayers.
        /// </summary>
        public void Setup(GCHudConfig config)
        {
            string playersHudDataJson = JsonUtility.ToJson(config);
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchSetupHud(playersHudDataJson);
#endif
        }

        /// <summary>
        /// Update the players in the HUD. Call Setup first.
        /// </summary>
        [Obsolete("GCHud.UpdatePlayers has been removed from the game-facing runtime contract. Use GCPlayer score/lives/status/meter APIs; the hosted HUD consumes runtime_messages state snapshots.", true)]
        public void UpdatePlayers(GCPlayersHudData playersHudData)
        {
            throw new InvalidOperationException("GCHud.UpdatePlayers has been removed. Use GCPlayer state APIs.");
        }

        [Obsolete("GCHud.UpdateScreenPointHud has been removed from the game-facing runtime contract. Use QueuePointData for screen-space HUD anchors.", true)]
        public void UpdateScreenPointHud(GCScreenPointData pointData)
        {
            throw new InvalidOperationException("GCHud.UpdateScreenPointHud has been removed. Use QueuePointData.");
        }

        private List<GCRuntimeScreenSpaceAnchor> screenSpaceQueue = new List<GCRuntimeScreenSpaceAnchor>();

        public void QueuePointData(GCScreenPointDataPoint pointData)
        {
            if (!GCRuntimeOutput.IsScreenSpaceEnabled)
            {
                return;
            }

            QueueScreenSpaceAnchor(pointData);
        }

        public void HandleQueue()
        {
            if (screenSpaceQueue.Count == 0)
            {
                return;
            }

            try
            {
                GCRuntimeOutput.EmitScreenSpace(Time.frameCount, screenSpaceQueue);
            }
            catch (Exception exception)
            {
                GCDiagnostics.Emit(
                    GCDiagnosticCodes.MalformedScreenSpace,
                    GCDiagnosticSeverity.Warning,
                    GCDiagnosticSourceAreas.ScreenSpace,
                    "Malformed screen-space output was rejected.",
                    new GCDiagnosticContext().AddDetail("reason", exception.Message)
                );
            }
            finally
            {
                screenSpaceQueue.Clear();
            }
        }

        public void SetCamera(Camera camera)
        {
            this.camera = camera;
        }

        private void QueueScreenSpaceAnchor(GCScreenPointDataPoint pointData)
        {
            if (!GCRuntimeScreenSpaceAnchorTypes.IsKnown(pointData.type))
            {
                return;
            }

            var gamingCouch = GamingCouch.Instance;
            if (gamingCouch != null &&
                gamingCouch.Status == GCStatus.Playing &&
                !gamingCouch.TryValidatePlayerIndex(pointData.playerIndex, "screen_space", out _))
            {
                return;
            }

            try
            {
                screenSpaceQueue.Add(new GCRuntimeScreenSpaceAnchor(
                    pointData.type,
                    pointData.playerIndex,
                    pointData.x,
                    pointData.y,
                    pointData.isOffScreen
                ));
            }
            catch (Exception exception)
            {
                GCDiagnostics.Emit(
                    GCDiagnosticCodes.MalformedScreenSpace,
                    GCDiagnosticSeverity.Warning,
                    GCDiagnosticSourceAreas.ScreenSpace,
                    "Malformed screen-space anchor was rejected.",
                    new GCDiagnosticContext()
                        .AddDetail("type", pointData.type)
                        .AddDetail("reason", exception.Message)
                );
            }
        }
    }
}

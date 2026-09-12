#if GC_UNITY_NETCODE_GAMEOBJECTS
using UnityEngine;
using Unity.Netcode;

namespace DSB.GC.Unity.NGO
{
    [RequireComponent(typeof(GCPlayer))]
    [RequireComponent(typeof(NetworkObject))]
    public class GCNetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<uint> netPlayerIndex = new NetworkVariable<uint>(0);
        public NetworkVariable<bool> isEliminated = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> isFinished = new NetworkVariable<bool>(false);
        public NetworkVariable<int> score = new NetworkVariable<int>(0);
        public NetworkVariable<int> lives = new NetworkVariable<int>(0);
        public NetworkVariable<GCPlayerStatus> status = new NetworkVariable<GCPlayerStatus>(0);
        public NetworkVariable<GCPlayerType> playerType = new NetworkVariable<GCPlayerType>(0);
        private GCPlayer player;
        public GCPlayer Player => player;

        private static string TEMP_REASON_NOT_SYNCED = "not available (not synced over net)";

        protected virtual void Awake()
        {
            player = GetComponent<GCPlayer>();
            isEliminated.Value = player.IsEliminated;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                StateSyncServer();
            }
            else
            {
                StateSyncClient();
            }
        }

        private void StateSyncClient()
        {
            var playerOptions = GamingCouch.Instance.GetPlayerOptions((int)netPlayerIndex.Value);
            GamingCouch.Instance._InternalSetPlayerProperties(player, playerOptions);

            isEliminated.OnValueChanged += (oldValue, newValue) =>
            {
                if (oldValue == newValue)
                {
                    return;
                }

                if (newValue)
                {
                    player.SetEliminatedRevokable(TEMP_REASON_NOT_SYNCED);
                }
                else
                {
                    player.SetRevokeEliminated(TEMP_REASON_NOT_SYNCED);
                }
            };

            isFinished.OnValueChanged += (oldValue, newValue) =>
            {
                if (newValue)
                {
                    player.SetFinishedRevokable(TEMP_REASON_NOT_SYNCED);
                }
                else
                {
                    player.SetRevokeFinished(TEMP_REASON_NOT_SYNCED);
                }
            };

            score.OnValueChanged += (oldValue, newValue) =>
            {
                player.SetScore(newValue, TEMP_REASON_NOT_SYNCED);
            };

            lives.OnValueChanged += (oldValue, newValue) =>
            {
                player.SetLives(newValue, TEMP_REASON_NOT_SYNCED);
            };


            status.OnValueChanged += (oldValue, newValue) =>
            {
                player.SetStatus(newValue, "TODO", TEMP_REASON_NOT_SYNCED);
            };
        }

        private void StateSyncServer()
        {
            // init the network values
            var playerIndex = player.Index;
            Debug.Assert(playerIndex >= 0, "Player index not set");
            netPlayerIndex.Value = (uint)playerIndex;

            isEliminated.Value = player.IsEliminated;
            isFinished.Value = player.IsFinished;
            score.Value = player.Score;
            lives.Value = player.Lives;
            status.Value = player.Status;
            playerType.Value = player.PlayerType;

            // sync basic GCPlayer state changes by default
            player.OnEliminationStateChanged += args => isEliminated.Value = player.IsEliminated;
            player.OnFinishStateChanged += args => isFinished.Value = player.IsFinished;
            player.OnScoreChanged += (oldScore, newScore, reason) =>
            {
                this.score.Value = newScore;
            };
            player.OnLivesChanged += (oldLives, newLives, reason) =>
            {
                this.lives.Value = newLives;
            };
            player.OnStatusChanged += (status, statusText, reason) =>
            {
                this.status.Value = status;
            };
        }
    }
}
#endif

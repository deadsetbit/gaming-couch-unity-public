using UnityEngine;

namespace DSB.GC.Hud
{
    public class GCPlayerPosition : MonoBehaviour
    {
        [SerializeField]
        private bool disableWhenEliminated = true;

        [SerializeField]
        private bool disableWhenOutOfScreen = false;

        private GCPlayer player;

        private GCPlayer FindPlayerComponent()
        {
            var player = GetComponent<GCPlayer>();
            if (player != null)
            {
                return player;
            }

            player = GetComponentInParent<GCPlayer>(true);
            if (player != null)
            {
                return player;
            }

            return null;
        }

        private void Start()
        {
            if (!player)
            {
                player = FindPlayerComponent();
            }

            if (!player)
            {
                Debug.LogError("GCPlayerPosition: Player index not set. Attach GCPlayerPosition to a player (GCPlayer), have it as a child or set player manually via GCPlayerPosition.SetPlayer before Start.");
                return;
            }
        }

        public void SetPlayer(GCPlayer player)
        {
            this.player = player;
        }

        private void Update()
        {
            if (!player) return;
            if (disableWhenEliminated && player.IsEliminated) return;

            var screenPosition = GamingCouch.Instance.Hud.Camera.WorldToScreenPoint(transform.position);

            var inScreen = screenPosition.z >= 0 && screenPosition.x >= 0 && screenPosition.x <= Screen.width && screenPosition.y >= 0 && screenPosition.y <= Screen.height;
            if (disableWhenOutOfScreen && !inScreen) return;

            GamingCouch.Instance.Hud.QueuePointData(new GCScreenPointDataPoint
            {
                type = "playerPosition",
                playerIndex = player.Index,
                x = Mathf.Clamp01(screenPosition.x / Screen.width),
                y = Mathf.Clamp01(screenPosition.y / Screen.height),
                isOffScreen = !inScreen
            });
        }


        [Header("Debug")]
        [SerializeField]
        private bool drawDebugGizmo = false;
        [SerializeField]
        private Color gizmoColor = Color.green;
        [SerializeField]
        private float gizmoRadius = 2.0f;

        private void OnDrawGizmos()
        {
            if (!drawDebugGizmo) return;
            if (player == null || player.Index == -1) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 screenPosition = camera.WorldToScreenPoint(transform.position);

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(screenPosition, gizmoRadius);
        }
    }
}

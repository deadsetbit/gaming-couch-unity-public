#if UNITY_EDITOR
using System;
using System.Text;
using DSB.GC.Log;
using UnityEditor;
using UnityEngine;

namespace DSB.GC.Dev
{
    /// <summary>
    /// The on-screen description of the editor keyboard, in the seat numbers DevApp shows.
    /// </summary>
    internal static class GCEditorKeyboardHint
    {
        internal const string ShowMenuPath = "GamingCouch/Editor Settings/Show Keyboard Hint";
        private const string HiddenKey = "DSB.GC.EditorKeyboardHint.Hidden";
        private const int MinFontSize = 9;
        private static GUIStyle style;
        private static GUIStyle closeButtonStyle;

        // Per user and per project, so hiding it never reaches a teammate's editor.
        internal static bool Hidden
        {
            get { return EditorUserSettings.GetConfigValue(HiddenKey) == "true"; }
            set { EditorUserSettings.SetConfigValue(HiddenKey, value ? "true" : "false"); }
        }

        internal static string Build(
            string moveKeys,
            string primaryKey,
            string secondaryKey,
            string keySource,
            GCSeatIdentity[] mappedSeatIdentities,
            int controlSeatNumber
        )
        {
            if (!GCSeatNumbering.TryGetPlayerIndex(mappedSeatIdentities, controlSeatNumber, out var controlPlayerIndex))
            {
                return null;
            }

            var controlSeat = mappedSeatIdentities[controlPlayerIndex];
            var hint = new StringBuilder();
            hint.Append("Keyboard: seat ").Append(controlSeatNumber)
                .Append(" (").Append(controlSeat.playerColor).Append(")\n");
            // The keyboard reaches a bot seat like any other, but a game can drive its bots itself
            // and ignore their input, as the example game does.
            if (controlSeat.playerType == GCPlayerType.bot)
            {
                hint.Append("<color=#ff6b6b>Bot seat: the game may ignore these keys</color>\n");
            }

            hint.Append("Move: ").Append(moveKeys).Append('\n');
            hint.Append("Primary: ").Append(primaryKey).Append('\n');
            hint.Append("Secondary: ").Append(secondaryKey).Append('\n');
            if (keySource != null)
            {
                hint.Append(keySource).Append('\n');
            }

            if (mappedSeatIdentities.Length > 1)
            {
                var lowestSeatNumber = int.MaxValue;
                var highestSeatNumber = int.MinValue;
                for (var playerIndex = 0; playerIndex < mappedSeatIdentities.Length; playerIndex++)
                {
                    var seatNumber = GCSeatNumbering.GetSeatNumber(mappedSeatIdentities[playerIndex], playerIndex);
                    lowestSeatNumber = Math.Min(lowestSeatNumber, seatNumber);
                    highestSeatNumber = Math.Max(highestSeatNumber, seatNumber);
                }

                hint.Append("Switch seat: press ").Append(lowestSeatNumber).Append('-').Append(highestSeatNumber).Append('\n');
            }

            hint.Append("Click the Game view first");
            return hint.ToString();
        }

        // Bottom-left, scaled to the Game view height, so it stays clear of a centred arena at
        // landscape sizes.
        internal static void Draw(string hint)
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
                closeButtonStyle = new GUIStyle(GUI.skin.button) { padding = new RectOffset(0, 0, 0, 0) };
            }

            var fontSize = Mathf.Max(MinFontSize, Screen.height / 40);
            var padding = fontSize / 2;
            var closeButtonSize = fontSize + padding;
            style.fontSize = fontSize;
            style.padding = new RectOffset(padding, padding * 2 + closeButtonSize, padding, padding);
            closeButtonStyle.fontSize = fontSize;
            var content = new GUIContent(hint);
            var size = style.CalcSize(content);
            var box = new Rect(padding, Screen.height - size.y - padding, size.x, size.y);
            GUI.Box(box, content, style);

            var closeButton = new Rect(box.xMax - closeButtonSize - padding / 2, box.y + padding / 2, closeButtonSize, closeButtonSize);
            if (GUI.Button(closeButton, new GUIContent("\u00d7", "Hide"), closeButtonStyle))
            {
                Hidden = true;
                GCLog.LogInfo("Keyboard hint hidden. Show it again from " + ShowMenuPath.Replace("/", " > ") + ".");
            }
        }
    }
}
#endif

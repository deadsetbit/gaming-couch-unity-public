#if UNITY_EDITOR
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && GC_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DSB.GC
{
    /// <summary>
    /// The keyboard behind editor playtesting, read from whichever input backend the project's
    /// Active Input Handling selects. Compile-time branches keep the package building under
    /// Input Manager (Old), Input System Package (New), and Both.
    /// </summary>
    internal static class GCEditorKeyboard
    {
#if ENABLE_INPUT_SYSTEM && GC_INPUT_SYSTEM
        internal static bool IsSeatSelectKeyDown(int seatNumber)
        {
            var keyboard = Keyboard.current;
            // Digit1 through Digit9 are consecutive, so a seat number maps straight onto them.
            if (keyboard == null || seatNumber < 1 || seatNumber > 9)
            {
                return false;
            }

            return keyboard[(Key)((int)Key.Digit1 + seatNumber - 1)].wasPressedThisFrame;
        }

        internal static GCControllerInputsData Read(string axisX, string axisY, string buttonPrimary, string buttonSecondary)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return new GCControllerInputsData();
            }

            return new GCControllerInputsData
            {
                a0 = ReadAxis(keyboard, Key.A, Key.LeftArrow, Key.D, Key.RightArrow),
                a1 = ReadAxis(keyboard, Key.S, Key.DownArrow, Key.W, Key.UpArrow),
                b0 = keyboard[Key.Space].isPressed ? 1 : 0,
                b1 = keyboard[Key.LeftCtrl].isPressed ? 1 : 0,
            };
        }

        internal static string DescribeMoveKeys(string axisX, string axisY)
        {
            return "WASD or arrow keys";
        }

        internal static string DescribePrimaryKey(string buttonPrimary)
        {
            return "Space";
        }

        internal static string DescribeSecondaryKey(string buttonSecondary)
        {
            return "Left Ctrl";
        }

        internal static string DescribeKeySource(string axisX, string axisY, string buttonPrimary, string buttonSecondary)
        {
            return null;
        }

        private static float ReadAxis(Keyboard keyboard, Key negative, Key negativeAlternative, Key positive, Key positiveAlternative)
        {
            var value = 0.0f;
            if (keyboard[negative].isPressed || keyboard[negativeAlternative].isPressed) value -= 1.0f;
            if (keyboard[positive].isPressed || keyboard[positiveAlternative].isPressed) value += 1.0f;
            return value;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        internal static bool IsSeatSelectKeyDown(int seatNumber)
        {
            return Input.GetKeyDown(seatNumber.ToString());
        }

        internal static GCControllerInputsData Read(string axisX, string axisY, string buttonPrimary, string buttonSecondary)
        {
            return new GCControllerInputsData
            {
                a0 = Input.GetAxis(axisX),
                a1 = Input.GetAxis(axisY),
                b0 = Input.GetButton(buttonPrimary) ? 1 : 0,
                b1 = Input.GetButton(buttonSecondary) ? 1 : 0,
            };
        }

        // The Input Manager can remap any axis or button, so keys are named only for its stock
        // axis and button names, and the hint says they are defaults.
        internal static string DescribeMoveKeys(string axisX, string axisY)
        {
            return HasDefaultMoveAxes(axisX, axisY)
                ? "WASD or arrow keys"
                : "Input Manager axes " + axisX + " and " + axisY;
        }

        internal static string DescribePrimaryKey(string buttonPrimary)
        {
            return buttonPrimary == "Jump" ? "Space" : "Input Manager button " + buttonPrimary;
        }

        internal static string DescribeSecondaryKey(string buttonSecondary)
        {
            return buttonSecondary == "Fire1" ? "Left Ctrl" : "Input Manager button " + buttonSecondary;
        }

        internal static string DescribeKeySource(string axisX, string axisY, string buttonPrimary, string buttonSecondary)
        {
            return HasDefaultMoveAxes(axisX, axisY) || buttonPrimary == "Jump" || buttonSecondary == "Fire1"
                ? "Input Manager default keys"
                : null;
        }

        private static bool HasDefaultMoveAxes(string axisX, string axisY)
        {
            return axisX == "Horizontal" && axisY == "Vertical";
        }
#else
        private static bool unavailabilityReported;

        internal static bool IsSeatSelectKeyDown(int seatNumber)
        {
            return false;
        }

        internal static GCControllerInputsData Read(string axisX, string axisY, string buttonPrimary, string buttonSecondary)
        {
            ReportUnavailableOnce();
            return new GCControllerInputsData();
        }

        internal static string DescribeMoveKeys(string axisX, string axisY)
        {
            return null;
        }

        internal static string DescribePrimaryKey(string buttonPrimary)
        {
            return null;
        }

        internal static string DescribeSecondaryKey(string buttonSecondary)
        {
            return null;
        }

        internal static string DescribeKeySource(string axisX, string axisY, string buttonPrimary, string buttonSecondary)
        {
            return null;
        }

        private static void ReportUnavailableOnce()
        {
            if (unavailabilityReported) return;
            unavailabilityReported = true;

            Debug.LogWarning(
                "[GamingCouch] The editor playtest keyboard is unavailable. Active Input Handling is set to " +
                "Input System Package (New), but the Input System package is not installed. Install " +
                "com.unity.inputsystem, or set Project Settings > Player > Active Input Handling to Both or " +
                "Input Manager (Old). DevApp controllers are unaffected."
            );
        }
#endif
    }
}
#endif

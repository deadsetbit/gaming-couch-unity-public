using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]

    /// <summary>
    /// The input namings and indexes are following on Gamepad API standard.
    /// eg.
    /// a0 = axis[0] // This is expected to be "Left stick X-axis", but it may vary depending on browser, controller, and OS.
    /// b0 = button[0] // This is expected to be "A button" on xbox controller, but it may vary depending on browser, controller, and OS.
    /// </summary>
    public class GCControllerInputsData
    {
        /// <summary>
        /// Left stick X-axis
        /// </summary>
        public float a0;
        /// <summary>
        /// Left stick Y-axis
        /// </summary>
        public float a1;
        /// <summary>
        /// Button index 0 - Would match A on Xbox controller
        /// </summary>
        public int b0;
        /// <summary>
        /// Button index 1 - Would match B on Xbox controller
        /// </summary>
        public int b1;
        /// <summary>
        /// Button index 2. Exposed to games as <see cref="GCControllerInputs.alt"/> — see that
        /// property for the intended (accessibility) semantics.
        /// </summary>
        public int b2;

        public static GCControllerInputsData CreateFromJSON(string inputsDataJson)
        {
            return JsonUtility.FromJson<GCControllerInputsData>(inputsDataJson);
        }
    }
}

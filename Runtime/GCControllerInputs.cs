using UnityEngine;

namespace DSB.GC
{
    public class GCControllerInputs
    {
        public GCControllerInputs(GCControllerInputsData data)
        {
            this.data = data;
        }

        private GCControllerInputsData data;
        public GCControllerInputsData RawData => data;

        public static GCControllerInputs CreateFromJSON(string inputsDataJson)
        {
            return new GCControllerInputs(GCControllerInputsData.CreateFromJSON(inputsDataJson));
        }

        /**
         * The left stick X-axis.
         * -1.0 is left
         * 1.0 is right
         **/
        public float leftX => data.a0;
        /**
         * The left stick Y-axis.
         * -1.0 is down
         * 1.0 is up
         **/
        public float leftY => data.a1;
        /**
         * Primary action button. Represents the A button on an Xbox controller layout.
         **/
        public bool primary => data.b0 == 1;
        /**
         * Secondary action button. Represents the B button on an Xbox controller layout.
         **/
        public bool secondary => data.b1 == 1;
        /**
         * A special button that should not be used for basic game mechanics
         * (such as combat) due to its limited accessibility on touch-screen controllers.
         *
         * This button can instead be used for actions such as "reset player" in case player is stuck,
         * or something else that is not commonly required in the heat of the moment.
         *
         * Games should be primarily designed to work without this button.
         **/
        public bool alt => data.b2 == 1;
    }
}

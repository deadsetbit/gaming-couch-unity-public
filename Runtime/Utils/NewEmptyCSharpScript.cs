namespace DSB.GC.Utils
{
    public static class Assert
    {
        public static void IsTrue(bool condition, string message)
        {
            if (!condition)
                throw new System.Exception(message);
        }

        public static void IsNotNull(object obj, string message)
        {
            if (obj == null)
                throw new System.Exception(message);
        }
    }
}
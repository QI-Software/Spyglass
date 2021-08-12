namespace Spyglass.Utilities
{
    public static class Assert
    {
        public static void IsNotNull(object obj, string message)
        {
            if (obj is null) Throw($"An assertion has failed: {message}");
        }

        public static void IsTrue(bool condition, string message)
        {
            if (!condition) Throw($"An assertion has failed: {message}");
        }

        public static void QuerySuccessful(QueryResult query, string message)
        {
            if (!query.Successful) Throw($"An assertion has failed: {message}");
        }

        public static void QuerySuccessful<T>(QueryResult<T> query, string message)
        {
            if (!query.Successful) Throw($"An assertion has failed: {message}");
        }

        private static void Throw(string message)
        {
            throw new AssertionException($"An assertion has failed: {message}");
        }
    }
}
using System;

namespace Spyglass.Utilities
{
    public class AssertionException : Exception
    {
        public AssertionException() : base()
        {
        }

        public AssertionException(string message) : base(message)
        {
        }

        public AssertionException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
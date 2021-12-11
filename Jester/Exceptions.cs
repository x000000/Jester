using System;
using System.IO;

namespace x0.Jester
{
    public class JesterReadException : JesterException
    {
        public JesterReadException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }

    public class JesterAttributeException : JesterException
    {
        public JesterAttributeException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }

    public abstract class JesterException : Exception
    {
        internal JesterException()
        {
        }

        internal JesterException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }
}

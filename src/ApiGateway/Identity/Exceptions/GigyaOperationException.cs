using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace ApiGateway.Identity.Exceptions
{
    [ExcludeFromCodeCoverage]
    public class GigyaOperationException : Exception
    {
        public GigyaOperationException()
        {
        }

        public GigyaOperationException(string? message) : base(message)
        {
        }

        public GigyaOperationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected GigyaOperationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

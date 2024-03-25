namespace ApiGateway.Contact.Exceptions
{
    public class ContactNotFoundException : Exception
    {
        public static string DefaultMessage => "No contact have been found.";

        public ContactNotFoundException()
            : base(DefaultMessage)
        {
        }

        public ContactNotFoundException(string message)
            : base(message)
        {
        }

        public ContactNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

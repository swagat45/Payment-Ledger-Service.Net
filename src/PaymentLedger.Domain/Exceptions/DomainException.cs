namespace PaymentLedger.Domain.Exceptions;

public class DomainException : Exception
{
    public string? ErrorCode { get; }

    public DomainException(string message, string? errorCode = null, Exception? innerException = null) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

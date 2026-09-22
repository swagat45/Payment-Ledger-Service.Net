using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Business.Exceptions;

public interface IExceptionToResponseMapper
{
    (int StatusCode, ErrorResponse Response) Map(Exception exception, string correlationId);
}

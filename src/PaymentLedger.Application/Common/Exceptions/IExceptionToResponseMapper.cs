using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Application.Common.Exceptions;

public interface IExceptionToResponseMapper
{
    (int StatusCode, ErrorResponse Response) Map(Exception exception, string correlationId);
}

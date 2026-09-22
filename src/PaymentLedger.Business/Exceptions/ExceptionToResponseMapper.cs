using System.Net;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Business.Exceptions;

public class ExceptionToResponseMapper : IExceptionToResponseMapper
{
    private const string DefaultInternalErrorMessage = "An unexpected error occurred. Please try again later.";

    public (int StatusCode, ErrorResponse Response) Map(Exception exception, string correlationId)
    {
        return exception switch
        {
            ValidationException appValidationException =>
                MapValidationException(appValidationException, correlationId),

            DomainException domainException =>
                MapDomainException(domainException, correlationId),

            _ => MapGenericException(correlationId)
        };
    }

    private static (int StatusCode, ErrorResponse Response) MapValidationException(
        ValidationException exception,
        string correlationId)
    {
        var errors = exception.Errors
            .SelectMany(kvp => kvp.Value.Select(message => new ErrorDetail(ErrorCodes.ValidationFailed, kvp.Key, message)))
            .ToList();

        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "One or more validation failures have occurred."
            : exception.Message;

        var response = ErrorResponse.Create(message, errors, correlationId);
        return ((int)HttpStatusCode.BadRequest, response);
    }

    private static (int StatusCode, ErrorResponse Response) MapDomainException(
        DomainException exception,
        string correlationId)
    {
        var errorCode = exception.ErrorCode;
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            var colonIndex = exception.Message.IndexOf(':');
            if (colonIndex > 0)
            {
                var candidate = exception.Message.Substring(0, colonIndex).Trim();
                if (candidate.All(c => char.IsLetterOrDigit(c) || c == '_'))
                {
                    errorCode = candidate;
                }
            }
        }

        int statusCode;
        if (string.Equals(errorCode, ErrorCodes.UserNotFound, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, ErrorCodes.WalletNotFound, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, ErrorCodes.TransactionNotFound, StringComparison.OrdinalIgnoreCase))
        {
            statusCode = (int)HttpStatusCode.NotFound;
        }
        else if (string.Equals(errorCode, ErrorCodes.InsufficientBalance, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(errorCode, ErrorCodes.IdempotencyConflict, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(errorCode, ErrorCodes.ConcurrencyConflict, StringComparison.OrdinalIgnoreCase))
        {
            statusCode = (int)HttpStatusCode.Conflict;
        }
        else if (string.Equals(errorCode, ErrorCodes.DuplicateRequest, StringComparison.OrdinalIgnoreCase))
        {
            statusCode = (int)HttpStatusCode.TooManyRequests;
        }
        else
        {
            statusCode = (int)HttpStatusCode.BadRequest;
        }

        var resolvedCode = !string.IsNullOrWhiteSpace(errorCode) ? errorCode : ErrorCodes.ValidationFailed;
        var errorDetail = new ErrorDetail(resolvedCode, null, exception.Message);

        var response = ErrorResponse.Create(
            exception.Message,
            new[] { errorDetail },
            correlationId);

        return (statusCode, response);
    }

    private static (int StatusCode, ErrorResponse Response) MapGenericException(string correlationId)
    {
        var errorDetail = new ErrorDetail(ErrorCodes.InternalServerError, null, DefaultInternalErrorMessage);
        var response = ErrorResponse.Create(
            DefaultInternalErrorMessage,
            new[] { errorDetail },
            correlationId);

        return ((int)HttpStatusCode.InternalServerError, response);
    }
}

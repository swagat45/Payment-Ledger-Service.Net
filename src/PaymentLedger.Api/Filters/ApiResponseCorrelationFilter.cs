using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PaymentLedger.Shared.Context;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Api.Filters;

public class ApiResponseCorrelationFilter : IAsyncResultFilter, IResultFilter
{
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public ApiResponseCorrelationFilter(ICorrelationIdProvider correlationIdProvider)
    {
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ApplyCorrelationId(context);
        await next();
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        ApplyCorrelationId(context);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private void ApplyCorrelationId(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: IApiResponse response })
        {
            if (string.IsNullOrWhiteSpace(response.CorrelationId))
            {
                response.CorrelationId = _correlationIdProvider.CorrelationId;
            }
        }
    }
}

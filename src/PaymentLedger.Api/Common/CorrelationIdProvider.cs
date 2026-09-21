using PaymentLedger.Application.Abstractions.Common;

namespace PaymentLedger.Api.Common;

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CorrelationId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Items.TryGetValue("CorrelationId", out var item) == true && item is string correlationId)
            {
                return correlationId;
            }

            return httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
        }
    }
}

namespace PaymentLedger.Shared.Responses;

public sealed class PagedResponse<T> : ApiResponse<IReadOnlyCollection<T>>
{
    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public long TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResponse<T> Ok(
        IReadOnlyCollection<T> data,
        int pageNumber,
        int pageSize,
        long totalCount,
        string message = "Request completed successfully.",
        string? correlationId = null)
    {
        return new PagedResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            CorrelationId = correlationId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

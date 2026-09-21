namespace PaymentLedger.Shared.Constants;

public static class ErrorCodes
{
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserAlreadyExists = "USER_ALREADY_EXISTS";
    public const string UserHasAssociatedWallets = "USER_HAS_ASSOCIATED_WALLETS";
    public const string UserNotActive = "USER_NOT_ACTIVE";
    public const string WalletNotFound = "WALLET_NOT_FOUND";
    public const string WalletAlreadyExists = "WALLET_ALREADY_EXISTS";
    public const string WalletNotActive = "WALLET_NOT_ACTIVE";
    public const string InvalidAmount = "INVALID_AMOUNT";
    public const string InvalidCurrency = "INVALID_CURRENCY";
    public const string CurrencyMismatch = "CURRENCY_MISMATCH";
    public const string InsufficientBalance = "INSUFFICIENT_BALANCE";
    public const string TransactionNotFound = "TRANSACTION_NOT_FOUND";
    public const string TransactionNotRefundable = "TRANSACTION_NOT_REFUNDABLE";
    public const string RefundAmountExceeded = "REFUND_AMOUNT_EXCEEDED";
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string DuplicateRequest = "DUPLICATE_REQUEST";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
}

namespace PaymentLedger.Shared.Constants;

public static class SecurityConstants
{
    public static class Scopes
    {
        public const string ReadWallet = "read:wallet";
        public const string WriteWallet = "write:wallet";
        public const string AdminLedger = "admin:ledger";

        public static readonly string[] AllScopes = [ReadWallet, WriteWallet, AdminLedger];
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Customer = "Customer";
        public const string Service = "Service";

        public static readonly string[] AllRoles = [Admin, Customer, Service];
    }

    public static class Policies
    {
        public const string ReadWallet = "ReadWallet";
        public const string WriteWallet = "WriteWallet";
        public const string AdminLedger = "AdminLedger";
    }

    public static class ClaimTypes
    {
        public const string Scope = "scope";
        public const string Role = "role";
        public const string UserId = "sub";
        public const string Email = "email";
    }
}

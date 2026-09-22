using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Persistence.Configurations;

public sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("idempotency_requests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(request => request.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(request => request.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(request => request.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("jsonb");

        builder.Property(request => request.StatusCode)
            .HasColumnName("status_code");

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(request => request.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(request => request.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(request => new { request.IdempotencyKey, request.Endpoint })
            .IsUnique();

        builder.HasIndex(request => request.ExpiresAt);
        builder.HasIndex(request => request.Status);
    }
}

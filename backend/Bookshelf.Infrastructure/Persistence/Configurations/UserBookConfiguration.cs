using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookshelf.Infrastructure.Persistence.Configurations;

public class UserBookConfiguration : IEntityTypeConfiguration<UserBook>
{
    public void Configure(EntityTypeBuilder<UserBook> builder)
    {
        builder.ToTable("UserBook", table =>
            table.HasCheckConstraint("CK_UserBook_Rating", "\"Rating\" BETWEEN 1 AND 5"));

        builder.HasKey(ub => ub.Id);

        builder.Property(ub => ub.Status)
            .IsRequired();

        builder.Property(ub => ub.AddedAt)
            .IsRequired();

        // Guards against duplicates even when two requests for the same book arrive at once.
        builder.HasIndex(ub => new { ub.UserId, ub.BookId })
            .IsUnique();

        // The shelf is always read per user, split by status.
        builder.HasIndex(ub => new { ub.UserId, ub.Status });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A book stays in the catalogue as long as anyone has it on a shelf.
        builder.HasOne(ub => ub.Book)
            .WithMany()
            .HasForeignKey(ub => ub.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

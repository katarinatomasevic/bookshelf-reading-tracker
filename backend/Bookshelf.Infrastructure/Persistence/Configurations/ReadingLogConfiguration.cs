using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookshelf.Infrastructure.Persistence.Configurations;

public class ReadingLogConfiguration : IEntityTypeConfiguration<ReadingLog>
{
    public void Configure(EntityTypeBuilder<ReadingLog> builder)
    {
        // An entry that records no pages is not progress; the API rejects it too, but a shelf
        // this table feeds statistics from should not depend on the API being the only writer.
        builder.ToTable("ReadingLog", table =>
            table.HasCheckConstraint("CK_ReadingLog_PagesRead", "\"PagesRead\" > 0"));

        builder.HasKey(log => log.Id);

        builder.Property(log => log.Date)
            .IsRequired();

        builder.Property(log => log.PagesRead)
            .IsRequired();

        builder.Property(log => log.CreatedAt)
            .IsRequired();

        // One row per book per day: a second entry on the same day adds to the first instead of
        // creating another row, otherwise the activity chart would count that day twice. The
        // descending Date makes this the same index the reading history reads newest-first, so
        // the uniqueness rule and that access path cost one index rather than two.
        builder.HasIndex(log => new { log.UserBookId, log.Date })
            .IsUnique()
            .IsDescending(false, true);

        // Removing a book from the shelf takes its progress with it: the log has no meaning
        // without the reading it belongs to.
        builder.HasOne<UserBook>()
            .WithMany()
            .HasForeignKey(log => log.UserBookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using PubmedDataFetch.Models;

namespace PubmedDataFetch.Data
{
	public class PublicationDbContext():DbContext
	{
		public DbSet<Publication> Publications { get; set; } = null!;
		public DbSet<PublicationAuthor> PublicationAuthors { get; set; } = null!;


		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			base.OnConfiguring(optionsBuilder);
			optionsBuilder.UseSqlite("Data Source=./publications.db");
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// Unique publication constraint
			modelBuilder.Entity<Publication>()
				.HasIndex(p => new { p.Provider, p.ProviderId })
				.IsUnique();

			// One-to-many: Publication -> PublicationAuthors
			modelBuilder.Entity<PublicationAuthor>()
				.HasOne(pa => pa.Publication)
				.WithMany(p => p.Authors)
				.HasForeignKey(pa => pa.PublicationId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}

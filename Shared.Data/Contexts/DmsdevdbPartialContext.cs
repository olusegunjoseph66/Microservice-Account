using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Shared.Data.Models
{
    public partial class dmsdevdbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json");
                IConfiguration Configuration = builder.Build();
                optionsBuilder.UseSqlServer(Configuration.GetConnectionString("DMSConnection"));
            }
        }


        internal virtual int SaveChanges(string? userId = null)
        {
            OnBeforeSaveChanges(userId);
            var result = base.SaveChangesAsync().Result;
            return result;
        }

        internal virtual async Task<int> SaveChangesAsync(string? userId = null)
        {
            OnBeforeSaveChanges(userId);
            var result = await base.SaveChangesAsync();
            return result;
        }

        private void OnBeforeSaveChanges(string? userId)
        {
            ChangeTracker.DetectChanges();
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // Add tables with IsDeleted Property so that it always select records with IsDeleted = false only.
            modelBuilder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);

        }

    }
}

using EndometriosisClient.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace EndometriosisClient.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<AppUser> Users { get; set; }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<MRIStudy> MRIStudies { get; set; }
        public DbSet<SegmentationResult> SegmentationResults { get; set; }

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string basePath = Directory.GetCurrentDirectory();
                string dbFolder = Path.Combine(basePath, "Database");
                Directory.CreateDirectory(dbFolder);

                string dbPath = Path.Combine(dbFolder, "app.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Login)
                .IsUnique();

            modelBuilder.Entity<Patient>()
                .HasOne(p => p.Doctor)
                .WithMany(d => d.Patients)
                .HasForeignKey(p => p.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MRIStudy>()
                .HasOne(m => m.Patient)
                .WithMany()
                .HasForeignKey(m => m.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SegmentationResult>()
                .HasOne(r => r.MRIStudy)
                .WithMany()
                .HasForeignKey(r => r.MRIStudyId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
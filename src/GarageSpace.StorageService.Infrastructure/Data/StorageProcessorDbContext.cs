using GarageSpace.StorageService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Infrastructure.Data
{
    public class StorageProcessorDbContext : DbContext
    {
        public StorageProcessorDbContext(DbContextOptions<StorageProcessorDbContext> options)
            : base(options)
        {
        }

        public DbSet<FileMetadata> FileMetadata { get; set; }
        public DbSet<UploadSession> UploadSessions { get; set; }
        public DbSet<UploadPart> UploadParts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // FileMetadata configuration
            modelBuilder.Entity<FileMetadata>()
                .HasKey(f => f.Id);

            modelBuilder.Entity<FileMetadata>()
                .HasIndex(f => f.S3Key)
                .IsUnique();

            modelBuilder.Entity<FileMetadata>()
                .HasIndex(f => f.UploadedByUserId);

            modelBuilder.Entity<FileMetadata>()
                .HasIndex(f => f.Status);

            // UploadSession configuration
            modelBuilder.Entity<UploadSession>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<UploadSession>()
                .HasIndex(u => u.UserId);

            modelBuilder.Entity<UploadSession>()
                .HasMany(u => u.Parts)
                .WithOne()
                .HasForeignKey(p => p.UploadSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // UploadPart configuration
            modelBuilder.Entity<UploadPart>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<UploadPart>()
                .HasIndex(p => new { p.UploadSessionId, p.PartNumber })
                .IsUnique();
        }
    }
}

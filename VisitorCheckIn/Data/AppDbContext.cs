using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Models;

namespace VisitorCheckIn.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<VisitHost> Hosts => Set<VisitHost>();
    public DbSet<HostDevice> HostDevices => Set<HostDevice>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitAnswer> VisitAnswers => Set<VisitAnswer>();
    public DbSet<KioskDevice> KioskDevices => Set<KioskDevice>();
    public DbSet<EnrollmentToken> EnrollmentTokens => Set<EnrollmentToken>();
    public DbSet<VisitPhoto> VisitPhotos => Set<VisitPhoto>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<VisitHost>().ToTable("Hosts");

        b.Entity<HostDevice>().HasOne(d => d.Host).WithMany(h => h.Devices)
            .HasForeignKey(d => d.HostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HostDevice>().Property(d => d.Endpoint).HasMaxLength(2000);

        b.Entity<Visit>().HasOne(v => v.Host).WithMany()
            .HasForeignKey(v => v.HostId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Visit>().HasOne(v => v.KioskDevice).WithMany()
            .HasForeignKey(v => v.KioskDeviceId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Visit>().Property(v => v.PhotoToken).HasMaxLength(64);

        b.Entity<VisitPhoto>().HasOne(p => p.Visit).WithMany()
            .HasForeignKey(p => p.VisitId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<VisitPhoto>().Property(p => p.Token).HasMaxLength(64).IsRequired();
        b.Entity<VisitPhoto>().Property(p => p.ContentType).HasMaxLength(50).IsRequired();
        b.Entity<VisitPhoto>().Property(p => p.Data).IsRequired();
        b.Entity<VisitPhoto>().HasIndex(p => p.Token).IsUnique();

        b.Entity<AppSetting>().HasKey(x => x.Key);
        b.Entity<AppSetting>().Property(x => x.Key).HasMaxLength(100);

        b.Entity<VisitAnswer>().HasOne(a => a.Visit).WithMany(v => v.Answers)
            .HasForeignKey(a => a.VisitId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<KioskDevice>().Property(d => d.TokenHash).HasMaxLength(64);
        b.Entity<KioskDevice>().HasIndex(d => d.TokenHash).IsUnique();

        b.Entity<EnrollmentToken>().Property(e => e.Code).HasMaxLength(10);
        b.Entity<EnrollmentToken>().Property(e => e.LinkTokenHash).HasMaxLength(64);
        b.Entity<EnrollmentToken>().HasIndex(e => e.Code);
        b.Entity<EnrollmentToken>().HasIndex(e => e.LinkTokenHash);
    }
}

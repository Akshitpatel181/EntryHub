using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Models;
using VisitorCheckIn.Services;

namespace VisitorCheckIn.Data;

public static class DbInitializer
{
    public static async Task InitAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var cfg = sp.GetRequiredService<IConfiguration>();
        // For production use EF migrations (dotnet ef migrations add Initial) instead of EnsureCreated.
        await db.Database.EnsureCreatedAsync();
        await UpgradeSchemaAsync(db);

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { "Admin", "Host" })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var email = cfg["Admin:Email"] ?? "admin@example.com";
        if (await users.FindByEmailAsync(email) == null)
        {
            var admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var res = await users.CreateAsync(admin, cfg["Admin:Password"] ?? "ChangeMe123");
            if (res.Succeeded) await users.AddToRoleAsync(admin, "Admin");
        }

        if (!await db.Questions.AnyAsync())
        {
            db.Questions.AddRange(
                new Question { Text = "Phone number", Type = QuestionType.Phone, IsRequired = true, SortOrder = 1, VisitorType = "All" },
                new Question { Text = "Do you have a company laptop with you?", Type = QuestionType.YesNo, IsRequired = true, SortOrder = 2, VisitorType = "All" },
                new Question { Text = "Reason for visit", Type = QuestionType.Dropdown, OptionsText = "Meeting\nDelivery\nInterview\nMaintenance\nOther", IsRequired = true, SortOrder = 3, VisitorType = "All" },
                new Question { Text = "I agree to the site safety and confidentiality terms (sign below)", Type = QuestionType.Signature, IsRequired = true, SortOrder = 4, VisitorType = "Contractor" });
            await db.SaveChangesAsync();
        }

        // Push keys: use appsettings if given, otherwise create once and keep them in the database.
        await sp.GetRequiredService<VapidKeyService>().InitAsync(db, cfg);
    }

    /// <summary>EnsureCreated never changes an existing database, so anything added after the first release is
    /// added here. Every statement is guarded and safe to run on each start.</summary>
    private static async Task UpgradeSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "IF COL_LENGTH('Visits', 'PhotoToken') IS NULL ALTER TABLE [Visits] ADD [PhotoToken] nvarchar(64) NULL;");

        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[VisitPhotos]', N'U') IS NULL
BEGIN
  CREATE TABLE [VisitPhotos] (
    [Id] int NOT NULL IDENTITY(1,1) PRIMARY KEY,
    [VisitId] int NOT NULL,
    [Token] nvarchar(64) NOT NULL,
    [Data] varbinary(max) NOT NULL,
    [ContentType] nvarchar(50) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [FK_VisitPhotos_Visits_VisitId] FOREIGN KEY ([VisitId]) REFERENCES [Visits]([Id]) ON DELETE CASCADE
  );
  CREATE UNIQUE INDEX [IX_VisitPhotos_Token] ON [VisitPhotos]([Token]);
  CREATE INDEX [IX_VisitPhotos_VisitId] ON [VisitPhotos]([VisitId]);
END");

        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Settings]', N'U') IS NULL
  CREATE TABLE [Settings] ([Key] nvarchar(100) NOT NULL PRIMARY KEY, [Value] nvarchar(max) NULL);");
    }
}

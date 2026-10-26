
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VisitorCheckIn.Models;
using VisitorCheckIn.Services;

namespace VisitorCheckIn.Data;

public static class DbInitializer
{
    public static async Task InitAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var cfg = sp.GetRequiredService<IConfiguration>();

        // Use EF Core migrations for database management.
        await db.Database.MigrateAsync();

        // ============================================
        // CREATE ROLES
        // ============================================

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in new[] { "Admin", "Host" })
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                var roleResult = await roles.CreateAsync(
                    new IdentityRole(roleName));

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        " | ",
                        roleResult.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        "Role creation failed: " + errors);
                }
            }
        }

        // ============================================
        // CREATE ADMIN ACCOUNT
        // ============================================

        var users = sp.GetRequiredService<UserManager<IdentityUser>>();

        var email = cfg["Admin:Email"]?.Trim();
        var password = cfg["Admin:Password"];

        if (!string.IsNullOrWhiteSpace(email) &&
            !string.IsNullOrWhiteSpace(password))
        {
            var admin = await users.FindByEmailAsync(email);

            if (admin == null)
            {
                admin = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await users.CreateAsync(
                    admin,
                    password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        " | ",
                        result.Errors.Select(e =>
                            $"{e.Code}: {e.Description}"));

                    throw new InvalidOperationException(
                        "ADMIN CREATION ERROR DETAILS: " + errors);
                }
            }

            // Ensure the configured administrator has
            // the Admin role.
            if (!await users.IsInRoleAsync(admin, "Admin"))
            {
                var roleResult = await users.AddToRoleAsync(
                    admin,
                    "Admin");

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        " | ",
                        roleResult.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        "Admin role assignment failed: " + errors);
                }
            }
        }

        // ============================================
        // DEFAULT KIOSK QUESTIONS
        // ============================================

        if (!await db.Questions.AnyAsync())
        {
            db.Questions.AddRange(
                new Question
                {
                    Text = "Phone number",
                    Type = QuestionType.Phone,
                    IsRequired = true,
                    SortOrder = 1,
                    VisitorType = "All"
                },
                new Question
                {
                    Text = "Do you have a company laptop with you?",
                    Type = QuestionType.YesNo,
                    IsRequired = true,
                    SortOrder = 2,
                    VisitorType = "All"
                },
                new Question
                {
                    Text = "Reason for visit",
                    Type = QuestionType.Dropdown,
                    OptionsText =
                        "Meeting\nDelivery\nInterview\nMaintenance\nOther",
                    IsRequired = true,
                    SortOrder = 3,
                    VisitorType = "All"
                },
                new Question
                {
                    Text = "I agree to the site safety and confidentiality terms (sign below)",
                    Type = QuestionType.Signature,
                    IsRequired = true,
                    SortOrder = 4,
                    VisitorType = "Contractor"
                }
            );

            await db.SaveChangesAsync();
        }

        // ============================================
        // PUSH NOTIFICATION KEYS
        // ============================================

        await sp.GetRequiredService<VapidKeyService>()
            .InitAsync(db, cfg);
    }
}

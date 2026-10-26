
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using VisitorCheckIn.Models;

namespace VisitorCheckIn.ViewModels;

// ============================================
// LOGIN
// ============================================

public class LoginVm
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool Remember { get; set; }
}

// ============================================
// HOST MANAGEMENT
// ============================================

public class HostFormVm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Host name is required.")]
    [StringLength(
        120,
        MinimumLength = 2,
        ErrorMessage = "Name must be between 2 and 120 characters.")]
    [RegularExpression(
        @"^[\p{L}\p{M}]+(?:[ '\u2019.-][\p{L}\p{M}]+)*$",
        ErrorMessage = "Enter a valid name using letters.")]
    public string Name { get; set; } = "";

    [StringLength(
        254,
        ErrorMessage = "Email address is too long.")]
    [EmailAddress(
        ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [RegularExpression(
        @"^(?:\+61|0)[\s-]?(?:4(?:[\s-]?\d){8}|[2378](?:[\s-]?\d){8})$",
        ErrorMessage = "Enter a valid Australian mobile or landline number.")]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public bool CreateLogin { get; set; }

    [DataType(DataType.Password)]
    [MinLength(
        12,
        ErrorMessage = "Password must be at least 12 characters.")]
    [MaxLength(
        128,
        ErrorMessage = "Password cannot exceed 128 characters.")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(
        nameof(Password),
        ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    public bool HasLogin { get; set; }
}

// ============================================
// DASHBOARD
// ============================================

public class DashboardVm
{
    public int HostCount { get; set; }

    public int QuestionCount { get; set; }

    public int DeviceCount { get; set; }

    public int OnlineDevices { get; set; }

    public int TodayVisits { get; set; }

    public List<Visit> OnSite { get; set; } = new();
}

// ============================================
// KIOSK DEVICES
// ============================================

public class DevicesIndexVm
{
    public List<KioskDevice> Devices { get; set; } = new();

    public List<EnrollmentToken> Pending { get; set; } = new();
}

// ============================================
// KIOSK ENROLLMENT RESULT
// ============================================

public class EnrollmentResultVm
{
    public string DeviceName { get; set; } = "";

    public string Code { get; set; } = "";

    public string Link { get; set; } = "";

    public string QrBase64 { get; set; } = "";

    public string PairUrl { get; set; } = "";

    public DateTime ExpiresUtc { get; set; }

    public int MaxUses { get; set; }

    public bool IsLocalhost { get; set; }
}

// ============================================
// KIOSK ENROLLMENT
// ============================================

public class EnrollVm
{
    [Required(ErrorMessage = "Enrollment token is required.")]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Device name is required.")]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "Device name must be between 2 and 100 characters.")]
    public string DeviceName { get; set; } = "";
}

// ============================================
// KIOSK QUESTIONS
// ============================================

public class KioskQuestion
{
    public int Id { get; set; }

    public string Text { get; set; } = "";

    public string Type { get; set; } = "";

    public string[] Options { get; set; } = Array.Empty<string>();

    public bool IsRequired { get; set; }

    public string VisitorType { get; set; } = "";
}

// ============================================
// KIOSK HOST
// ============================================

public class KioskHost
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

// ============================================
// MAIN KIOSK
// ============================================

public class KioskVm
{
    public string CompanyName { get; set; } = "";

    public string WelcomeText { get; set; } = "";

    public string DeviceName { get; set; } = "";

    public bool RequirePhoto { get; set; } = true;

    public string[] VisitorTypes { get; set; } = Array.Empty<string>();

    public List<KioskQuestion> Questions { get; set; } = new();

    public List<KioskHost> Hosts { get; set; } = new();
}

// ============================================
// VISITOR QUESTION ANSWERS
// ============================================

public class AnswerDto
{
    public int QuestionId { get; set; }

    public string Value { get; set; } = "";
}

// ============================================
// VISITOR CHECK-IN
// ============================================

public class CheckInDto
{
    [Required(ErrorMessage = "Visitor name is required.")]
    [StringLength(
        120,
        MinimumLength = 2,
        ErrorMessage = "Visitor name must be between 2 and 120 characters.")]
    public string Name { get; set; } = "";

    [StringLength(
        150,
        ErrorMessage = "Company name cannot exceed 150 characters.")]
    public string Company { get; set; } = "";

    [Required(ErrorMessage = "Visitor type is required.")]
    public string VisitorType { get; set; } = "";

    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Please select a host.")]
    public int HostId { get; set; }

    public string Photo { get; set; } = "";

    public List<AnswerDto> Answers { get; set; } = new();

    // Duplicate-name confirmation.
    public bool ConfirmDuplicate { get; set; } = false;

    // Duplicate-submission protection.
    public Guid SubmissionId { get; set; }
}

// ============================================
// GENERIC ID
// ============================================

public class IdDto
{
    public int Id { get; set; }
}

// ============================================
// HOST PUSH NOTIFICATIONS
// ============================================

public class SubscribeDto
{
    public string Endpoint { get; set; } = "";

    public string P256dh { get; set; } = "";

    public string Auth { get; set; } = "";

    public string Label { get; set; } = "";
}

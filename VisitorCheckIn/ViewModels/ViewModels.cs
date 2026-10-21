using System.ComponentModel.DataAnnotations;
using VisitorCheckIn.Models;

namespace VisitorCheckIn.ViewModels;

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; }
    [Required, DataType(DataType.Password)] public string Password { get; set; }
    public bool Remember { get; set; }
}

public class HostFormVm
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; }
    [EmailAddress] public string Email { get; set; }
    public string Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool CreateLogin { get; set; }
    [DataType(DataType.Password)] public string Password { get; set; }
    public bool HasLogin { get; set; }
}

public class DashboardVm
{
    public int HostCount { get; set; }
    public int QuestionCount { get; set; }
    public int DeviceCount { get; set; }
    public int OnlineDevices { get; set; }
    public int TodayVisits { get; set; }
    public List<Visit> OnSite { get; set; } = new();
}

public class DevicesIndexVm
{
    public List<KioskDevice> Devices { get; set; } = new();
    public List<EnrollmentToken> Pending { get; set; } = new();
}

public class EnrollmentResultVm
{
    public string DeviceName { get; set; }
    public string Code { get; set; }
    public string Link { get; set; }
    public string QrBase64 { get; set; }
    public string PairUrl { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public int MaxUses { get; set; }
    public bool IsLocalhost { get; set; }
}

public class EnrollVm
{
    public string Token { get; set; }
    public string DeviceName { get; set; }
}

public class KioskQuestion
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string Type { get; set; }
    public string[] Options { get; set; }
    public bool IsRequired { get; set; }
    public string VisitorType { get; set; }
}

public class KioskHost
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class KioskVm
{
    public string CompanyName { get; set; }
    public string WelcomeText { get; set; }
    public string DeviceName { get; set; }
    public bool RequirePhoto { get; set; } = true;
    public string[] VisitorTypes { get; set; }
    public List<KioskQuestion> Questions { get; set; } = new();
    public List<KioskHost> Hosts { get; set; } = new();
}

public class AnswerDto { public int QuestionId { get; set; } public string Value { get; set; } }

public class CheckInDto
{
    public string Name { get; set; }
    public string Company { get; set; }
    public string VisitorType { get; set; }
    public int HostId { get; set; }
    /// <summary>JPEG data URL (data:image/jpeg;base64,...) taken by the kiosk camera.</summary>
    public string Photo { get; set; }
    public List<AnswerDto> Answers { get; set; } = new();
}

public class IdDto { public int Id { get; set; } }

public class SubscribeDto
{
    public string Endpoint { get; set; }
    public string P256dh { get; set; }
    public string Auth { get; set; }
    public string Label { get; set; }
}

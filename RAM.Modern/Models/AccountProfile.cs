namespace RAM.Modern.Models;

public sealed class AccountProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long UserId { get; set; }
    public string Group { get; set; } = "General";
    public string Note { get; set; } = "";
    public bool Favorite { get; set; }
    public DateTimeOffset AddedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSelectedUtc { get; set; }
    public long PreferredPlaceId { get; set; }
    public string PreferredJobId { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public int SortOrder { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName;
    public string Handle => string.IsNullOrWhiteSpace(Username) ? "@profile" : $"@{Username}";
    public string GroupDisplay => RAM.Modern.Services.GroupSort.Parse(Group).DisplayName;
    public string UserIdLabel => UserId > 0 ? $"ID {UserId}" : "No ID";
    public string AddedLabel => AddedUtc.ToString("dd MMM yyyy");

    public override string ToString() => $"{(Favorite ? "★ " : "")}{DisplayTitle}   @{Username}   [{Group}]";
}

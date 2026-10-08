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
 public override string ToString() => $"{(Favorite ? "★ " : "")}{(string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName)}   @{Username}   [{Group}]";
}

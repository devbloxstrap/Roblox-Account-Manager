using System.Diagnostics;
namespace RAM.Modern.Services;
public static class RobloxService
{
 // Open the official website in the user's regular browser. We do not read, import,
 // persist, or inject .ROBLOSECURITY cookies or promise silent account switching.
 public static void Login() => Open("https://www.roblox.com/login");
 public static void Home() => Open("https://www.roblox.com/home");
 public static void OpenProfile(long userId)
 {
  if(userId <= 0) throw new ArgumentException("Add a valid Roblox numeric user ID first.");
  Open($"https://www.roblox.com/users/{userId}/profile");
 }
 public static void OpenGame(long placeId)
 {
  if(placeId <= 0) throw new ArgumentException("Enter a valid Roblox place ID.");
  // Browser handles official Roblox installation, handoff, login, and joining.
  Open($"https://www.roblox.com/games/{placeId}");
 }
 private static void Open(string url) => Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
}

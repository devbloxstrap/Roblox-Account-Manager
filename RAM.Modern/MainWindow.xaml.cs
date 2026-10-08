using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RAM.Modern.Models;
using RAM.Modern.Services;
namespace RAM.Modern;
public partial class MainWindow : Window
{
 private readonly List<AccountProfile> _profiles;
 private readonly ObservableCollection<AccountProfile> _displayed = new();
 private Guid? _selectedId;
 public MainWindow()
 {
  InitializeComponent();
  try { _profiles=ProfileStore.Load(); } catch(Exception e) { MessageBox.Show(e.Message,"Profile data error"); _profiles=new(); }
  AccountsList.ItemsSource=_displayed;
  Refresh();
 }
 private void Refresh()
 {
  string q=SearchBox.Text.Trim();
  _displayed.Clear();
  foreach(var p in _profiles.Where(p=>q.Length==0 || (p.Username+" "+p.DisplayName+" "+p.Group+" "+p.Note+" "+p.UserId).Contains(q,StringComparison.OrdinalIgnoreCase)).OrderByDescending(p=>p.Favorite).ThenBy(p=>p.Group).ThenBy(p=>p.Username)) _displayed.Add(p);
  if (_selectedId is Guid id) AccountsList.SelectedItem=_displayed.FirstOrDefault(x=>x.Id==id);
  StatusText.Text=$"{_profiles.Count} profiles stored locally. Login sessions are protected per Windows user; Roblox may require re-login.";
 }
 private void Search_Changed(object sender,TextChangedEventArgs e){ if(AccountsList is not null) Refresh(); }
 private void Account_Selected(object sender,SelectionChangedEventArgs e)
 {
  if(AccountsList.SelectedItem is not AccountProfile p) return;
  _selectedId=p.Id; UsernameBox.Text=p.Username;DisplayBox.Text=p.DisplayName;IdBox.Text=p.UserId>0?p.UserId.ToString():"";GroupBox.Text=p.Group;NoteBox.Text=p.Note;FavoriteBox.IsChecked=p.Favorite;
 }
 private void Save_Click(object sender,RoutedEventArgs e)
 {
  string username=UsernameBox.Text.Trim().TrimStart('@');
  if(string.IsNullOrWhiteSpace(username)){MessageBox.Show("Enter an account username.");return;}
  if(!string.IsNullOrWhiteSpace(IdBox.Text)&&(!long.TryParse(IdBox.Text,out long parsed)||parsed<=0)){MessageBox.Show("User ID must be a positive number.");return;}
  long id=string.IsNullOrWhiteSpace(IdBox.Text)?0:long.Parse(IdBox.Text);
  var p=_profiles.FirstOrDefault(x=>x.Id==_selectedId) ?? new AccountProfile();
  if(_profiles.Any(x=>x.Id!=p.Id && (x.Username.Equals(username,StringComparison.OrdinalIgnoreCase) || (id>0&&x.UserId==id)))) { MessageBox.Show("This account is already in the list.");return; }
  p.Username=username;p.DisplayName=DisplayBox.Text.Trim();p.UserId=id;p.Group=string.IsNullOrWhiteSpace(GroupBox.Text)?"General":GroupBox.Text.Trim();p.Note=NoteBox.Text.Trim();p.Favorite=FavoriteBox.IsChecked==true;
  if(!_profiles.Any(x=>x.Id==p.Id))_profiles.Add(p);
  try{ProfileStore.Save(_profiles);_selectedId=p.Id;Refresh();}catch(Exception ex){MessageBox.Show(ex.Message,"Save failed");}
 }
 private void New_Click(object sender,RoutedEventArgs e){_selectedId=null;AccountsList.SelectedItem=null;UsernameBox.Clear();DisplayBox.Clear();IdBox.Clear();GroupBox.Text="General";NoteBox.Clear();FavoriteBox.IsChecked=false;}
 private void Delete_Click(object sender,RoutedEventArgs e){var p=_profiles.FirstOrDefault(x=>x.Id==_selectedId);if(p==null)return;if(MessageBox.Show($"Remove @{p.Username}?","Confirm",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;_profiles.Remove(p);try{ProfileStore.Save(_profiles);SessionSwitcher.Delete(p.Id);New_Click(sender,e);Refresh();}catch(Exception ex){MessageBox.Show(ex.Message);}}
 private static void Safe(Action a){try{a();}catch(Exception e){MessageBox.Show(e.Message,"Action failed");}}
 private void SaveSession_Click(object sender,RoutedEventArgs e) => Safe(()=>
 {
  var p=_profiles.FirstOrDefault(x=>x.Id==_selectedId)??throw new InvalidOperationException("Select a profile first.");
  if(MessageBox.Show($"Save the CURRENT Roblox client login for @{p.Username}? Ensure it is the same account.\n\nThis action does not verify the username in the login file.","Confirm session",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
  SessionSwitcher.SaveCurrent(p.Id);
  StatusText.Text=$"Saved an encrypted session snapshot for @{p.Username}.";
 });
 private void RestoreSession_Click(object sender,RoutedEventArgs e) => Safe(()=>
 {
  var p=_profiles.FirstOrDefault(x=>x.Id==_selectedId)??throw new InvalidOperationException("Select a profile first.");
  if(MessageBox.Show($"Restore the saved client session for @{p.Username}? All Roblox windows must be closed.\n\nThis replaces the client login file, not your browser login. Roblox may still ask you to sign in.","Confirm restore",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
  SessionSwitcher.Restore(p.Id);
  p.LastSelectedUtc=DateTimeOffset.UtcNow;
  ProfileStore.Save(_profiles);
  StatusText.Text=$"Restored local session for @{p.Username}. Open Roblox to check whether it is accepted; re-login if expired.";
 });
 private void Login_Click(object sender,RoutedEventArgs e)=>Safe(RobloxService.Login);
 private void Home_Click(object sender,RoutedEventArgs e)=>Safe(RobloxService.Home);
 private void Profile_Click(object sender,RoutedEventArgs e)=>Safe(()=>{var p=_profiles.FirstOrDefault(x=>x.Id==_selectedId)??throw new InvalidOperationException("Select an account first.");RobloxService.OpenProfile(p.UserId);});
 private void Game_Click(object sender,RoutedEventArgs e)=>Safe(()=>{if(!long.TryParse(PlaceBox.Text,out long place)||place<=0)throw new InvalidOperationException("Enter a valid place ID.");RobloxService.OpenGame(place);});
 private void Folder_Click(object sender,RoutedEventArgs e)=>Safe(()=>{Directory.CreateDirectory(ProfileStore.Folder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProfileStore.Folder){UseShellExecute=true});});
}

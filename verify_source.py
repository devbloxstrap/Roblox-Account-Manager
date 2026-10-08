"""Portable source-level checks; actual WPF compilation happens via GitHub Actions on Windows."""
from pathlib import Path
from xml.etree import ElementTree as ET
import re, zipfile, sys
root=Path(__file__).parent
ui=root/'RAM.Modern'
errors=[]
for xml_file in ui.glob('*.xaml'):
    try: tree=ET.parse(xml_file)
    except Exception as exc: errors.append(f'{xml_file.name}: XML error {exc}');continue
    code=(xml_file.with_suffix('.xaml.cs')).read_text(encoding='utf-8') if xml_file.with_suffix('.xaml.cs').exists() else ''
    names=[]
    for el in tree.iter():
        for key,value in el.attrib.items():
            if key.endswith('}Name') or key=='Name': names.append(value)
    for handler in re.findall(r'\b(?:Click|SelectionChanged|TextChanged|MouseLeftButtonDown|StateChanged)="([A-Za-z_]\w*)"', xml_file.read_text(encoding='utf-8')):
        if not re.search(r'\b'+re.escape(handler)+r'\s*\(',code):errors.append(f'{xml_file.name}: missing {handler}')
    dup={name for name in names if names.count(name)>1}
    if dup:errors.append(f'{xml_file.name}: duplicate names {sorted(dup)}')
    print(f'PASS XAML {xml_file.name} named={len(names)}')

try:
    workflow=(root/'.github/workflows/ram-modern-windows.yml').read_text(encoding='utf-8')
    assert 'jobs:' in workflow and 'build-windows:' in workflow
    assert 'RAM.Modern.SmokeTests' in workflow and 'dotnet publish' in workflow
    print('PASS GitHub Actions YAML / smoke steps')
except Exception as exc:errors.append(f'GitHub Workflow: {exc}')

for file in ['RAM.Modern.SmokeTests/Program.cs','RAM.Modern/Services/PublicFeatureTools.cs','RAM.Modern/Services/DiagnosticsReport.cs','ONE_PASS_TESTING.md']:
    if not (root/file).exists(): errors.append(f'Missing {file}')

if 'auto-update' in (root/'RAM.Modern/Services/PublicFeatureTools.cs').read_text(encoding='utf-8').lower():errors.append('Forbidden updater reference')
# Catch common missing System.IO imports before the Windows compiler starts.
io_re = re.compile(r'\b(?:File|Directory|Path)\s*\.|\b(?:InvalidDataException|FileNotFoundException|FileStream|IOException)\b')
for csfile in sorted((root/'RAM.Modern').rglob('*.cs')) + sorted((root/'RAM.Modern.SmokeTests').rglob('*.cs')):
    code = csfile.read_text(encoding='utf-8')
    if io_re.search(code) and 'using System.IO;' not in code and 'global using System.IO;' not in code:
        errors.append(f'{csfile.relative_to(root)}: missing using System.IO;')
if not any('missing using System.IO' in issue for issue in errors):
    print('PASS System.IO namespace preflight across C# files')

# Preview 7 regression: keep one game-launch handler, never reintroduce local-file restore
# in the primary account switching path.
main = (ui/'MainWindow.xaml.cs').read_text(encoding='utf-8')
if main.count('private async void RestoreAndLaunch_Click(') != 1:
    errors.append('MainWindow: duplicate/missing selected-account launch handler')
if 'RestoreVerifiedAsync(' in main:
    errors.append('MainWindow: old snapshot-restore path still referenced')
for newfile in ['BrowserLoginWindow.cs', 'Services/BrowserLoginCapture.cs',
                'Services/AccountAuthStore.cs', 'Services/RobloxTicketLauncher.cs']:
    if not (ui/newfile).exists(): errors.append('Missing ticket-login module: '+newfile)
if 'AccountAuthStore.Save(' not in main or 'RobloxTicketLauncher.LaunchAsync(' not in main:
    errors.append('MainWindow: new per-account login/launch workflow missing')
if not any('RobloxTicketLauncher.LaunchAsync(' in f.read_text(encoding='utf-8')
           for f in [ui/'ToolsWindow.xaml.cs',ui/'AdvancedWindow.xaml.cs']):
    errors.append('Account-specific server launch not wired')
if not errors:print('PASS Preview 7 account ticket/source regression checks')

print('RESULT:', 'PASS' if not errors else 'FAILED')
for item in errors:print('ERROR:',item)
sys.exit(0 if not errors else 1)

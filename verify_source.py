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
    code=(xml_file.with_suffix('.xaml.cs')).read_text() if xml_file.with_suffix('.xaml.cs').exists() else ''
    names=[]
    for el in tree.iter():
        for key,value in el.attrib.items():
            if key.endswith('}Name') or key=='Name': names.append(value)
    for handler in re.findall(r'\b(?:Click|SelectionChanged|TextChanged|MouseLeftButtonDown|StateChanged)="([A-Za-z_]\w*)"', xml_file.read_text()):
        if not re.search(r'\b'+re.escape(handler)+r'\s*\(',code):errors.append(f'{xml_file.name}: missing {handler}')
    dup={name for name in names if names.count(name)>1}
    if dup:errors.append(f'{xml_file.name}: duplicate names {sorted(dup)}')
    print(f'PASS XAML {xml_file.name} named={len(names)}')

try:
    workflow=(root/'.github/workflows/ram-modern-windows.yml').read_text()
    assert 'jobs:' in workflow and 'build-windows:' in workflow
    assert 'RAM.Modern.SmokeTests' in workflow and 'dotnet publish' in workflow
    print('PASS GitHub Actions YAML / smoke steps')
except Exception as exc:errors.append(f'GitHub Workflow: {exc}')

for file in ['RAM.Modern.SmokeTests/Program.cs','RAM.Modern/Services/PublicFeatureTools.cs','RAM.Modern/Services/DiagnosticsReport.cs','ONE_PASS_TESTING.md']:
    if not (root/file).exists(): errors.append(f'Missing {file}')

if 'auto-update' in (root/'RAM.Modern/Services/PublicFeatureTools.cs').read_text().lower():errors.append('Forbidden updater reference')
print('RESULT:', 'PASS' if not errors else 'FAILED')
for item in errors:print('ERROR:',item)
sys.exit(0 if not errors else 1)

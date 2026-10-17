"""Compare real production GUI/session code with identical deterministic game boundaries.

Run from repository: python Tests/BondOverviewUI/Performance/Compare.py
Outputs stay in .builds/bond-overview-ui-opt/performance; no game process is used.
"""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('--baseline', default='98b3ce6d59415651a6b480ea521eaf0de575b534')
parser.add_argument('--rounds', type=int, default=3)
args = parser.parse_args()
baseline = subprocess.check_output(['git', 'rev-parse', args.baseline], cwd=root, text=True).strip()
out = root / '.builds/bond-overview-ui-opt/performance'
out.mkdir(parents=True, exist_ok=True)
host = root / 'Tests/BondOverviewUI'
def historical(path):
    return subprocess.check_output(['git', 'show', baseline+':'+path], cwd=root)
def digest(data):
    return hashlib.sha256(data).hexdigest()

# Both sides use precisely the same GUI metric, scan counters and reset helper.
# The metric models call shape and allocations, not real fonts, native portraits or FPS.
boundaries = {'UIStubs.cs': (host/'UIStubs.cs').read_bytes(),
              'Program.cs': historical('Tests/BondOverviewUI/Program.cs'),
              'TestPaths.cs': (root/'Tests/Shared/TestPaths.cs').read_bytes(),
              'PerfEntry.cs': (Path(__file__).parent/'PerfEntry.cs.txt').read_bytes()}
projects = {}
hashes = {}
for mode in ('baseline', 'optimized'):
    folder = out/mode
    folder.mkdir(exist_ok=True)
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    props = ET.SubElement(project, 'PropertyGroup')
    for k, v in {'OutputType':'Exe', 'TargetFramework':'net9.0', 'ImplicitUsings':'enable',
                 'EnableDefaultCompileItems':'false', 'StartupObject':'PerfEntry', 'NoWarn':'CS0108'}.items():
        ET.SubElement(props, k).text = v
    group = ET.SubElement(project, 'ItemGroup')
    hashes[mode] = {}
    for i, item in enumerate(ET.parse(host/'BondOverviewUI.csproj').getroot().findall('ItemGroup/Compile')):
        src = (host/item.attrib['Include']).resolve()
        rel = src.relative_to(root).as_posix()
        data = historical(rel) if mode=='baseline' and rel.startswith('Sexslavecraft/') else src.read_bytes()
        path = folder/(str(i)+'-'+src.name)
        path.write_bytes(data)
        hashes[mode][rel] = digest(data.replace(b'\r\n', b'\n'))
        ET.SubElement(group, 'Compile', Include=path.as_posix())
    for name, data in boundaries.items():
        path = folder/name
        path.write_bytes(data)
        ET.SubElement(group, 'Compile', Include=path.as_posix())
    ET.ElementTree(project).write(folder/'Perf.csproj', encoding='utf-8', xml_declaration=True)
    (folder/'NuGet.Config').write_bytes((root/'Tests/NuGet.Config').read_bytes())
    build = subprocess.run(['dotnet', 'build', str(folder/'Perf.csproj'), '-c', 'Release', '--nologo'], cwd=root, capture_output=True)
    (folder/'build.log').write_bytes(build.stdout+build.stderr)
    assert build.returncode == 0, (folder/'build.log').read_text(encoding='utf-8-sig')
    projects[mode] = folder/'bin/Release/net9.0/Perf.dll'

runs = {mode: [] for mode in projects}
for round_index in range(args.rounds):
    for mode in (('baseline', 'optimized') if round_index%2==0 else ('optimized', 'baseline')):
        run = subprocess.run(['dotnet', str(projects[mode])], cwd=root, capture_output=True)
        (out/(mode+'-'+str(round_index)+'.jsonl')).write_bytes(run.stdout)
        assert run.returncode == 0, run.stderr.decode('utf-8', errors='replace')
        runs[mode].append([json.loads(line) for line in run.stdout.decode('utf-8').splitlines()])

# Fail comparisons that silently reduce visible work or introduce GUI world queries.
for round_index in range(args.rounds):
    before = {i['name']:i for i in runs['baseline'][round_index] if 'medianBytes' in i}
    after = {i['name']:i for i in runs['optimized'][round_index] if 'medianBytes' in i}
    assert before.keys() == after.keys()
    for name, item in after.items():
        for counter in ('mapQueries', 'portraitRequests', 'canJumpChecks'):
            assert item[counter] == before[name][counter], (name, counter)
        if 'window' in name or 'gizmo' in name:
            assert item['mapQueries'] == 0, name
        if name.startswith('warm-window') or name.startswith('warm-gizmo'):
            assert item['truncations'] == 0, name

result = {'baseline':baseline, 'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),
          'sdk':subprocess.check_output(['dotnet','--version'],text=True).strip(), 'rounds':args.rounds,
          'boundaryHashes':{k:digest(v) for k,v in boundaries.items()}, 'sourceHashes':hashes, 'runs':runs,
          'limitations':'net9 deterministic GUI boundaries; no real font/portrait rendering or in-game FPS measurement'}
(out/'comparison.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
print('Compared baseline and optimized production code in', args.rounds, 'alternating rounds:', out)

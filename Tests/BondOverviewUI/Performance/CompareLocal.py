"""Compare local refresh, hover, selection and diagnostics with the frozen 776b8af baseline.

Run: python Tests/BondOverviewUI/Performance/CompareLocal.py
Production inputs are frozen per side. Both use identical current boundary stubs,
the baseline regression host (only Reset is invoked), and the same benchmark.
Only copied health/model constructors receive allocation-free integer counters.
"""
from pathlib import Path
import argparse,hashlib,json,subprocess
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[3];host=root/'Tests/BondOverviewUI'
parser=argparse.ArgumentParser()
parser.add_argument('--baseline',default='776b8afdbc5dc734d87759b94d00d77774a90b48')
parser.add_argument('--rounds',type=int,default=3)
parser.add_argument('--output',default='.builds/bond-overview-local-perf/comparison')
args=parser.parse_args();out=root/args.output;out.mkdir(parents=True,exist_ok=True)
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
base=git('rev-parse',args.baseline).decode().strip()
def historical(rel):return git('show',base+':'+rel)
def digest(data):return hashlib.sha256(data).hexdigest()
def run(cmd):return subprocess.run(cmd,cwd=root,capture_output=True)

baseline_host=[p for p in git('ls-tree','-r','--name-only',base,'Tests/BondOverviewUI').decode().splitlines()
               if p.startswith('Tests/BondOverviewUI/Program') and p.endswith('.cs')]
inputs=ET.fromstring(historical('Tests/BondOverviewUI/BondOverviewUI.csproj'))
sources=[(host/i.attrib['Include']).resolve().relative_to(root).as_posix() for i in inputs.findall('ItemGroup/Compile')]
sources+=['Tests/BondOverviewUI/UIStubs.cs','Tests/Shared/TestPaths.cs']+baseline_host
projects={};manifests={}
for mode in ('baseline','optimized'):
 folder=out/mode;folder.mkdir(exist_ok=True)
 project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
 for k,v in dict(OutputType='Exe',TargetFramework='net9.0',ImplicitUsings='enable',EnableDefaultCompileItems='false',StartupObject='AuditEntry',NoWarn='CS0108').items():ET.SubElement(props,k).text=v
 group=ET.SubElement(project,'ItemGroup');manifest={}
 for i,rel in enumerate(sources):
  data=historical(rel) if rel in baseline_host or mode=='baseline' and rel.startswith('Sexslavecraft/') else (root/rel).read_bytes()
  text=data.decode('utf-8-sig').replace('\r\n','\n')
  if rel.endswith('/GameStubs.cs'):
   text=text.replace('public Hediff GetFirstHediffOfDef(HediffDef def)\n        {','public static int LookupCalls, LookupSteps;\n        public Hediff GetFirstHediffOfDef(HediffDef def)\n        {\n            LookupCalls++;')
   text=text.replace('foreach (Hediff hediff in hediffs) if (hediff.def == def) return hediff;', 'foreach (Hediff hediff in hediffs) { LookupSteps++; if (hediff.def == def) return hediff; }')
  if rel.endswith('/SSCBondOverviewModel.cs'):
   text=text.replace('            Kind = kind;', '            PerfCounts.Locations++;\n            Kind = kind;')
   text=text.replace('            Pawn = related;', '            PerfCounts.Entries++;\n            Pawn = related;')
   text=text.replace('            Game = game;', '            PerfCounts.Snapshots++;\n            Game = game;')
  name=f'{i}-{Path(rel).name}';(folder/name).write_text(text,encoding='utf-8')
  manifest[rel]={'sourceSha256':digest(data),'compiledSha256':digest(text.encode())}
  ET.SubElement(group,'Compile',Include=name)
 data=(Path(__file__).parent/'LocalPerfEntry.cs.txt').read_bytes();(folder/'AuditEntry.cs').write_bytes(data)
 ET.SubElement(group,'Compile',Include='AuditEntry.cs');manifest['benchmark']={'sourceSha256':digest(data)}
 ET.ElementTree(project).write(folder/'Perf.csproj',encoding='utf-8',xml_declaration=True)
 (folder/'NuGet.Config').write_bytes((root/'Tests/NuGet.Config').read_bytes())
 build=run(['dotnet','build',str(folder/'Perf.csproj'),'-c','Release','--nologo'])
 (folder/'build.log').write_bytes(build.stdout+build.stderr)
 assert build.returncode==0,str(folder/'build.log')
 projects[mode]=folder/'bin/Release/net9.0/Perf.dll';manifests[mode]=manifest

runs={k:[] for k in projects}
for i in range(args.rounds):
 for mode in (('baseline','optimized') if i%2==0 else ('optimized','baseline')):
  result=run(['dotnet',str(projects[mode]),str(root)])
  (out/f'{mode}-{i}.jsonl').write_bytes(result.stdout);assert result.returncode==0,result.stderr.decode(errors='replace')
  runs[mode].append([json.loads(line) for line in result.stdout.decode('utf-8-sig').splitlines()])

for a,b in zip(runs['baseline'],runs['optimized']):
 assert len(a)==len(b)
 for left,right in zip(a,b):
  assert left['name']==right['name']
  for metric in ('mapScans','portraits','builds','worldScans'):
   if metric in left:assert left[metric]==right[metric],(left['name'],metric)
  if left['name'].startswith('bulk-'):
   for metric in ('modelMedianBytes','healthReads','entries','locations','snapshots'):assert left[metric]==right[metric]
  if right['name'].startswith('diagnostic-query-') or right['name']=='diagnostic-ship-cargo200':
   assert right['entries']==right['locations']==right['snapshots']==0
  if right['name']=='unchanged-refresh-cache':assert not right['displayReplaced'] and not right['rowReplaced']

summary={'baselineCommit':base,'optimizedBaseCommit':git('rev-parse','HEAD').decode().strip(),
         'optimizedWorkingTreeDirty':bool(git('status','--porcelain')),'rounds':args.rounds,'inputHashes':manifests,
         'instrumentation':'Identical health lookup and model-constructor integer counters; no production logic rewrite.',
         'scope':'Model allocations/call counts and timings, not native portraits/fonts/FPS. No game process used.',
         'runs':runs}
(out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'rounds':args.rounds,'scenarios':len(runs['optimized'][0]),'invariantsPassed':True,'output':str(out)}))

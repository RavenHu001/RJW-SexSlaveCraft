"""Compare committed overview query files and current files against identical net9 model boundaries."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import statistics
import subprocess
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline', default='d8b354d')
    parser.add_argument('--harmony-path', required=True)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--runs', type=int, default=3)
    parser.add_argument('--output', default='.builds/bond-overview-opt/performance')
    args = parser.parse_args()
    if args.runs < 1:
        parser.error('--runs must be positive')
    root = Path(__file__).resolve().parents[2]
    out = (root / args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    baseline = subprocess.check_output(['git', 'rev-parse', '--verify', args.baseline + '^{commit}'], cwd=root, text=True).strip()
    # 仅替换三个生产查询文件；两侧链接相同的关系代码、引擎替身和基准入口。
    # 在独立输出目录构建，不切换工作区版本，也不覆盖现有生产文件。
    overrides = {'SSCBondOverviewModel.cs', 'SSCBondOverviewUtility.cs', 'SSCBondOverviewWorld.cs'}
    source = out / 'baseline-src'
    source.mkdir(exist_ok=True)
    for name in overrides:
        (source / name).write_bytes(subprocess.check_output(['git', 'show', baseline + ':Sexslavecraft/common/' + name], cwd=root))
    projects = {}
    for mode in ('baseline', 'current'):
        directory = out / mode
        directory.mkdir(exist_ok=True)
        project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
        props = ET.SubElement(project, 'PropertyGroup')
        for key, value in {'OutputType': 'Exe', 'TargetFramework': 'net9.0', 'EnableDefaultCompileItems': 'false'}.items():
            ET.SubElement(props, key).text = value
        group = ET.SubElement(project, 'ItemGroup')
        for item in ET.parse(root / 'Tests/BondRelations/BondRelations.csproj').getroot().findall('ItemGroup/Compile'):
            path = root / item.attrib['Include'].replace('$(SscSourceRoot)', 'Sexslavecraft')
            if mode == 'baseline' and path.name in overrides:
                path = source / path.name
            ET.SubElement(group, 'Compile', Include=path.as_posix())
        for name in ('GameStubs.cs', 'OverviewWorldStubs.cs', 'BondOverviewBenchmark.cs'):
            ET.SubElement(group, 'Compile', Include=(root / 'Tests/BondRelations' / name).as_posix())
        entry = directory / 'Entry.cs'
        entry.write_text('static class Entry { static int Main() => BondOverviewBenchmark.Run(); }\n', encoding='utf-8')
        ET.SubElement(group, 'Compile', Include=entry.as_posix())
        reference = ET.SubElement(group, 'Reference', Include='0Harmony')
        ET.SubElement(reference, 'HintPath').text = Path(args.harmony_path).resolve().as_posix()
        path = directory / 'OverviewPerf.csproj'
        ET.ElementTree(project).write(path, encoding='utf-8', xml_declaration=True)
        shutil.copyfile(root / 'Tests/NuGet.Config', directory / 'NuGet.Config')
        result = subprocess.run([args.dotnet, 'build', str(path), '--configuration', 'Release', '--nologo'], cwd=root, capture_output=True)
        (directory / 'build.log').write_bytes(result.stdout + result.stderr)
        if result.returncode:
            raise RuntimeError(f'{mode} benchmark build failed; see {directory / "build.log"}')
        projects[mode] = directory / 'bin/Release/net9.0/OverviewPerf.dll'

    samples = {mode: [] for mode in projects}
    # 逐轮交替执行顺序，保留每次原始输出，减少固定先后次序造成的偏差。
    # 各进程自行预热，比较的是模型稳态，不能换算为目标游戏的帧率或首次调用成本。
    for run in range(args.runs):
        for mode in (('baseline', 'current') if run % 2 == 0 else ('current', 'baseline')):
            result = subprocess.run([args.dotnet, str(projects[mode])], cwd=root, capture_output=True)
            (out / f'{mode}-{run + 1}.jsonl').write_bytes(result.stdout + result.stderr)
            if result.returncode:
                raise RuntimeError(f'{mode} benchmark run {run + 1} failed')
            rows = [json.loads(line) for line in result.stdout.decode('utf-8').splitlines() if line.startswith('{')]
            if len(rows) != 7:
                raise RuntimeError('expected seven benchmark scenarios')
            samples[mode].append({row['name']: row for row in rows})
    rows = []
    for name in samples['baseline'][0]:
        # 每一轮先验证候选数量和有序状态摘要，再汇总各轮中位数；不以性能改善替代语义一致性。
        for index in range(args.runs):
            before, after = samples['baseline'][index][name], samples['current'][index][name]
            if before['count'] != after['count'] or before['digest'] != after['digest']:
                raise RuntimeError(f'candidate/order/state digest mismatch in {name}')
        def median(mode, key):
            return statistics.median(run[name][key] for run in samples[mode])
        rows.append({'name': name, 'baselineMs': median('baseline', 'medianMs'), 'currentMs': median('current', 'medianMs'),
                     'baselineBytes': median('baseline', 'medianAllocatedBytes'), 'currentBytes': median('current', 'medianAllocatedBytes'),
                     'baselineMapPasses': median('baseline', 'mapPasses'), 'currentMapPasses': median('current', 'mapPasses'),
                     'baselineHolderVisits': median('baseline', 'holderVisits'), 'currentHolderVisits': median('current', 'holderVisits'),
                     'baselineShipItemVisits': median('baseline', 'shipVisits'), 'currentShipItemVisits': median('current', 'shipVisits')})
    # 保存实际参与比较的原始文件哈希、完整提交和 SDK，后续源码即便只补注释也能区分证据版本。
    hashes = {name: hashlib.sha256((root / 'Sexslavecraft/common' / name).read_bytes()).hexdigest() for name in sorted(overrides)}
    boundaries = {name: hashlib.sha256((root / 'Tests/BondRelations' / name).read_bytes()).hexdigest()
                  for name in ('GameStubs.cs', 'OverviewWorldStubs.cs', 'BondOverviewBenchmark.cs')}
    sdk = subprocess.check_output([args.dotnet, '--version'], cwd=root, text=True).strip()
    summary = {'baselineCommit': baseline, 'currentSourceSha256': hashes, 'boundarySha256': boundaries,
               'sdkVersion': sdk, 'runsPerVersion': args.runs, 'warmups': 16, 'samplesPerScenario': 31,
               'semanticParity': True, 'boundary': 'net9 model with common instrumented engine boundaries; not in-game timing', 'results': rows}
    (out / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(summary, ensure_ascii=True))


if __name__ == '__main__':
    main()

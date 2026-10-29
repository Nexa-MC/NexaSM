#!/usr/bin/env python3
"""Fail closed on unregistered projects, references, packages and source imports."""
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
def project_key(path):
    return path.as_posix()

ROOT = Path(__file__).resolve().parents[1]
GRAPH = json.loads((ROOT / 'eng/project-graph.json').read_text())
PACKAGES = {'NexaSM.Services.Composition': {'Nexa.Xsr.Runtime'}, 'NexaSM.Persistence.Sqlite': {'Microsoft.Data.Sqlite'},
            'NexaSM.Tests': {'Microsoft.NET.Test.Sdk', 'xunit', 'xunit.runner.visualstudio'}}

def check(root, graph):
    errors = []
    projects = {project_key(p.parent.relative_to(root)): p for p in root.rglob('*.csproj') if not {'bin', 'obj', 'artifacts'} & set(p.parts)}
    if set(projects) != set(graph): errors.append('Project registry mismatch')
    for folder, path in projects.items():
        xml = ET.parse(path)
        refs = set()
        for ref in xml.findall('.//ProjectReference'):
            target = (path.parent / ref.attrib['Include']).resolve()
            if not target.is_relative_to(root.resolve()): errors.append(f'{folder}: external reference'); continue
            refs.add(project_key(target.parent.relative_to(root.resolve())))
        if refs != set(graph.get(folder, [])): errors.append(f'{folder}: forbidden/missing reference')
        for package in xml.findall('.//PackageReference'):
            if package.attrib['Include'] not in PACKAGES.get(path.stem, set()): errors.append(f'{folder}: unregistered package')
        for compile_item in xml.findall('.//Compile'):
            if 'Include' in compile_item.attrib: errors.append(f'{folder}: linked compile source forbidden')
        sources = [p for p in path.parent.rglob('*.cs') if not {'bin','obj'} & set(p.parts)]
        if not sources: errors.append(f'{folder}: empty project')
        if folder in {'src/NexaSM.Domain','src/NexaSM.Contracts','src/NexaSM.Services'}:
            for source in sources:
                if re.search(r'\b(Nexa\.(Xsr|Desktop)|Microsoft\.AspNetCore|NexaSM\.Persistence)\b', source.read_text()):
                    errors.append(f'{source.name}: forbidden implementation dependency')
    return errors

if __name__ == '__main__':
    errors = check(ROOT, GRAPH)
    print('\n'.join(errors) if errors else f'Architecture passed: {len(GRAPH)} registered non-empty projects')
    sys.exit(bool(errors))

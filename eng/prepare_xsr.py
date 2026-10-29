#!/usr/bin/env python3
"""Build the pinned upstream source into an ignored local feed; never reference its projects."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / 'eng/xsr-source.json'

def run(*args):
    subprocess.run(args, cwd=ROOT, check=True)

def valid_feed(feed, spec):
    manifest = feed / 'feed-manifest.json'
    if not manifest.exists(): return False
    data = json.loads(manifest.read_text())
    if data['source_commit'] != spec['commit'] or data['version'] != spec['version']: return False
    expected = spec.get('packages')
    if not expected or data['packages'] != expected: return False
    return all((feed / p['file']).is_file() and hashlib.sha256((feed / p['file']).read_bytes()).hexdigest() == p['sha256'] for p in expected)

def prepare(source_override=None):
    spec = json.loads(LOCK.read_text())
    feed = ROOT / 'artifacts/xsr-feed'
    if valid_feed(feed, spec):
        print('XSR feed verified against pinned commit and package hashes.')
        return
    checkout = source_override.resolve() if source_override else ROOT / 'artifacts/xsr-source'
    if not (checkout / '.git').exists():
        if source_override: raise SystemExit('Explicit source checkout is not a Git repository.')
        if checkout.exists() and any(checkout.iterdir()): raise SystemExit('Refusing to overwrite nonempty source cache.')
        checkout.mkdir(parents=True, exist_ok=True)
        run('git','-C',str(checkout),'init')
        run('git','-C',str(checkout),'remote','add','origin',spec['repository'])
        run('git','-C',str(checkout),'fetch','--depth=1','origin',spec['commit'])
        run('git','-c','core.autocrlf=false','-C',str(checkout),'checkout','--detach','FETCH_HEAD')
    actual = subprocess.check_output(['git','-C',str(checkout),'rev-parse','HEAD'],text=True).strip()
    if actual != spec['commit']: raise SystemExit('Source cache commit differs; use a separate checkout of the pinned revision.')
    changes = subprocess.check_output(['git','-C',str(checkout),'status','--porcelain','--untracked-files=no'],text=True)
    if changes: raise SystemExit('Pinned source checkout contains tracked changes; refusing to build unreviewed inputs.')
    run(sys.executable,str(checkout / 'eng/packages/build.py'),'--output',str(feed))
    if not valid_feed(feed, spec): raise SystemExit('Rebuilt package hashes differ from the reviewed lock; do not refresh hashes blindly.')
    print('XSR feed rebuilt and verified.')

if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--source-checkout',type=Path)
    args=parser.parse_args();prepare(args.source_checkout)

#!/usr/bin/env python3
"""Run from any directory. Requires pinned dotnet SDK and Python 3."""
from pathlib import Path
import argparse
from collections import deque
import os
import subprocess
import sys
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(); parser.add_argument('--aot',action='store_true'); args=parser.parse_args()
def run(*command):
    # Keep the failing stage visible in Actions annotations as well as private logs.
    tail = deque(maxlen=25)
    with subprocess.Popen(command, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                          text=True, encoding='utf-8', errors='replace') as process:
        for line in process.stdout:
            print(line, end='', flush=True)
            tail.append(line)
        code = process.wait()
    if code:
        if os.environ.get('GITHUB_ACTIONS') == 'true':
            message = (' '.join(command) + '\n' + ''.join(tail))[-6000:]
            message = message.replace('%', '%25').replace('\r', '%0D').replace('\n', '%0A')
            print('::error::' + message, flush=True)
        raise subprocess.CalledProcessError(code, command)
run(sys.executable,'eng/check_architecture.py')
run(sys.executable,'eng/test_architecture.py')
run(sys.executable,'eng/prepare_xsr.py')
run('dotnet','restore','NexaSM.sln','--locked-mode')
run('dotnet','build','NexaSM.sln','-c','Release','--no-restore','-m:2')
run('dotnet','test','NexaSM.sln','-c','Release','--no-build','--logger','trx;LogFileName=m0.trx')
exe='.exe' if sys.platform=='win32' else ''
run(sys.executable,'eng/test_process.py',f'tools/TestProcess/bin/Release/net10.0/TestProcess{exe}')
run(sys.executable,'tools/FaultHarness/recovery.py',f'tools/M0Probe/bin/Release/net10.0/M0Probe{exe}')
run(sys.executable,'tools/FaultHarness/test_recovery.py',f'tools/M0Probe/bin/Release/net10.0/M0Probe{exe}')
run(sys.executable,'eng/check_response_budget.py',f'tools/M0Probe/bin/Release/net10.0/M0Probe{exe}','--report','artifacts/response-managed.json')
if args.aot:
    if sys.platform != 'linux': raise SystemExit('This AOT probe currently targets linux-x64 only.')
    run('dotnet','publish','tools/M0Probe','-c','Release','-r','linux-x64','-p:PublishAot=true','-p:RestoreLockedMode=true','-o','artifacts/aot-probe')
    run(sys.executable,'tools/FaultHarness/recovery.py','artifacts/aot-probe/M0Probe')
    run(sys.executable,'eng/check_response_budget.py','artifacts/aot-probe/M0Probe','--report','artifacts/response-aot.json')

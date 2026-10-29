#!/usr/bin/env python3
"""Run the real storage-contention response gate and retain the maximum for every scenario."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
ROOT = Path(__file__).resolve().parents[1]

def check(binary, report):
    with tempfile.TemporaryDirectory(prefix='nexasm-response-') as directory:
        result = subprocess.run([str(binary.resolve()), str(Path(directory)/'control.db'), '--latency'],
                                cwd=ROOT,text=True,capture_output=True,timeout=30)
        report.parent.mkdir(parents=True,exist_ok=True)
        report.with_suffix('.log').write_text(result.stdout + result.stderr)
        if result.returncode:
            raise SystemExit(f'Response budget probe failed ({result.returncode}); see {report.with_suffix(".log")}')
        data=json.loads(result.stdout)
        report.write_text(json.dumps(data,indent=2)+'\n')
        if not data['Passed'] or not data['Measurements']: raise SystemExit('Response budget failed or no measurements')
        for sample in data['Measurements']:
            if sample['Samples'] < 1 or sample['MaximumMilliseconds'] > sample['BudgetMilliseconds']:
                raise SystemExit('Response budget exceeded')
            print(f"{sample['Scenario']}: max {sample['MaximumMilliseconds']:.3f} ms / {sample['BudgetMilliseconds']} ms ({sample['Samples']} samples)")

if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('binary',type=Path);parser.add_argument('--report',type=Path,required=True)
    args=parser.parse_args();check(args.binary,args.report)

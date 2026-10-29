#!/usr/bin/env python3
"""Kill only our probe handle at admission checkpoints; inspect recovery in a new connection."""
import json
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import threading


def run_checkpoint(binary, point):
    committed = point in ('AfterCommit', 'AfterReceipt')
    with tempfile.TemporaryDirectory(prefix='nexasm-recovery-') as folder:
        database = str(Path(folder) / 'control.db')
        option = '--hold' if point == 'AfterReceipt' else '--checkpoint=' + point
        process = subprocess.Popen([binary, database, option], stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE, text=True)
        watchdog = threading.Timer(20, process.kill)
        watchdog.start()
        try:
            first = process.stdout.readline().strip()
            if not first:
                raise RuntimeError(f'{point}: no checkpoint: ' + process.stderr.read())
            if point == 'AfterReceipt':
                original_id = json.loads(first)['OperationId']['Value']
            else:
                marker, actual_point, original_id = first.split()
                if marker != 'CHECKPOINT' or actual_point != point:
                    raise RuntimeError(f'Unexpected checkpoint: {first}')
            process.kill()
            process.communicate(timeout=5)
        finally:
            watchdog.cancel()
            if process.poll() is None:
                process.kill()
            process.communicate(timeout=5)

        # Reopening also performs SQLite journal recovery. Verify no partial transaction survived.
        with sqlite3.connect(database) as db:
            counts = tuple(db.execute('SELECT COUNT(*) FROM ' + table).fetchone()[0]
                           for table in ('operations', 'outbox'))
            state = db.execute('SELECT desired, version, generation FROM instances').fetchone()
            expected = 1 if committed else 0
            if counts != (expected, expected) or state != (expected, expected, expected):
                raise AssertionError((point, counts, state))

        resumed = subprocess.run([binary, database, '--resume'], capture_output=True,
                                 text=True, timeout=20, check=True)
        receipt = json.loads(resumed.stdout)
        if committed and receipt['OperationId']['Value'] != original_id:
            raise AssertionError('Durable operation was replayed')
        with sqlite3.connect(database) as db:
            if db.execute('SELECT COUNT(*) FROM operations').fetchone()[0] != 1:
                raise AssertionError('Retry did not converge to one operation')
            if db.execute('SELECT COUNT(*) FROM outbox').fetchone()[0] != 1:
                raise AssertionError('Retry did not converge to one outbox item')
        print(f'PASS: {point}: atomic recovery and one durable retry result')

if __name__ == '__main__':
    binary = str(Path(sys.argv[1]).resolve())
    for point in ('AfterIntentWrite', 'AfterOperationWrite', 'BeforeCommit', 'AfterCommit', 'AfterReceipt'):
        run_checkpoint(binary, point)

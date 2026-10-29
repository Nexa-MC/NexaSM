#!/usr/bin/env python3
import subprocess
import sys
import time
import unittest
BINARY = sys.argv.pop(1)
class ProcessTests(unittest.TestCase):
    def run_fixture(self, mode, value='100', stdin=None):
        return subprocess.run([BINARY, mode, value], input=stdin, capture_output=True, text=True, timeout=10)
    def test_echo_and_graceful_stop(self):
        result = self.run_fixture('echo', stdin='hello\nstop\n')
        self.assertEqual(0, result.returncode); self.assertEqual('READY\nhello\n',result.stdout)
    def test_slow(self):
        begin=time.monotonic(); result=self.run_fixture('slow','150')
        self.assertGreaterEqual(time.monotonic()-begin, .15); self.assertEqual('READY\n',result.stdout)
    def test_silent(self): self.assertEqual('',self.run_fixture('silent').stdout)
    def test_flood(self): self.assertEqual(8193*10,len(self.run_fixture('flood','10').stdout))
    def test_exit(self): self.assertEqual(17,self.run_fixture('exit','17').returncode)
    def test_child(self): self.assertTrue(self.run_fixture('child').stdout.startswith('CHILD '))
    def test_refuses_stop(self):
        process=subprocess.Popen([BINARY,'refuse-stop'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
        try:
            process.stdin.write('stop\n'); process.stdin.flush()
            with self.assertRaises(subprocess.TimeoutExpired): process.wait(timeout=.3)
        finally:
            process.kill(); process.communicate(timeout=5)
    def test_invalid_mode(self): self.assertEqual(2,self.run_fixture('invalid').returncode)
if __name__ == '__main__': unittest.main()

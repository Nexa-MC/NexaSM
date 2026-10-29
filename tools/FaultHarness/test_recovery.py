"""Verify inspection connections release their database handles before cleanup."""
from pathlib import Path
import sqlite3
import sys
import unittest
from unittest.mock import patch
import recovery

BINARY = str(Path(sys.argv.pop(1)).resolve())


class ConnectionLifetimeTests(unittest.TestCase):
    def test_inspection_connections_are_closed(self):
        connections = []
        connect = sqlite3.connect

        def track(*args, **kwargs):
            connection = connect(*args, **kwargs)
            connections.append(connection)
            return connection

        try:
            with patch.object(recovery.sqlite3, 'connect', side_effect=track):
                recovery.run_checkpoint(BINARY, 'BeforeCommit')
            self.assertEqual(2, len(connections))
            for connection in connections:
                with self.assertRaises(sqlite3.ProgrammingError):
                    connection.execute('SELECT 1')
        finally:
            for connection in connections:
                connection.close()


if __name__ == '__main__':
    unittest.main()

import importlib.util
from pathlib import Path, PureWindowsPath
import tempfile
import unittest
spec = importlib.util.spec_from_file_location('gate', Path(__file__).with_name('check_architecture.py'))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)
class GateTests(unittest.TestCase):
    def test_windows_paths_match_manifest_keys(self):
        self.assertEqual('src/NexaSM.Services', gate.project_key(PureWindowsPath('src', 'NexaSM.Services')))

    def test_rejects_forbidden_dependency(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            project = root/'src/NexaSM.Services'
            project.mkdir(parents=True)
            (project/'Services.csproj').write_text('<Project><ItemGroup><ProjectReference Include="../Renderer/Renderer.csproj"/></ItemGroup></Project>')
            (project/'Service.cs').write_text('class Service {}')
            self.assertTrue(any('reference' in e for e in gate.check(root, {'src/NexaSM.Services': []})))
    def test_rejects_empty_project(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); (root/'Empty.csproj').write_text('<Project/>')
            self.assertTrue(any('empty project' in e for e in gate.check(root, {'.': []})))
    def test_rejects_unregistered_project(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); (root/'Unknown.csproj').write_text('<Project/>')
            self.assertIn('Project registry mismatch', gate.check(root, {}))
if __name__ == '__main__': unittest.main()

"""End-to-end checks for DeliveryPilot Markdown CLI without extra dependencies."""
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SOURCE = Path(__file__).resolve().parents[2]


class DocsCliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        shutil.copytree(SOURCE / 'docs', self.root / 'docs')
        shutil.copy2(SOURCE / 'ARCHITECTURE.md', self.root / 'ARCHITECTURE.md')
        shutil.copytree(SOURCE / 'tools', self.root / 'tools')
        shutil.copytree(SOURCE / '.agents' / 'skills', self.root / '.agents' / 'skills')
        self.feat = self.root / 'docs' / 'features' / 'FEAT-DP-003-organization'

    def run_cli(self, *args, expected=0):
        p = subprocess.run([sys.executable, str(self.root / 'tools' / 'docs.py'), *args],
                           cwd=self.root, capture_output=True, text=True)
        self.assertEqual(expected, p.returncode, p.stdout + '\n' + p.stderr)
        return p.stdout + '\n' + p.stderr

    def patch(self, rel, old, new):
        p = self.root / rel
        body = p.read_text()
        self.assertIn(old, body)
        p.write_text(body.replace(old, new, 1))

    def test_original_example_valid_and_coverage(self):
        self.assertIn('0 errors', self.run_cli('validate'))
        out=self.run_cli('coverage','--feature','FEAT-DP-003')
        self.assertIn('UR: 5',out)
        self.assertIn('FR: 7',out)
        self.assertIn('US: 4',out)
        self.assertIn('TASK: 1',out)

    def test_create_registered_feature_and_ur_without_overwriting(self):
        args=('scaffold-feature','--id','FEAT-DP-007','--title','Approval Workflow', '--slug','approval','--release','MVP','--priority','P0')
        self.run_cli(*args)
        self.run_cli(*args, expected=2)
        self.run_cli('new-artifact','--type','UR','--feature','FEAT-DP-007','--title','Approve a requirement','--actor','Client Approver')
        self.assertTrue((self.root/'docs/features/FEAT-DP-007-approval/ur/UR-DP-007-001.md').exists())
        self.assertIn('UR-DP-007-002',self.run_cli('next-id','--type','UR','--feature','FEAT-DP-007'))
        self.run_cli('validate')

    def test_duplicate_id_fails(self):
        original = self.feat/'ur/UR-DP-003-001.md'
        (self.feat/'ur/UR-DP-003-099.md').write_text(original.read_text())
        self.assertIn('duplicate ID',self.run_cli('validate',expected=1))

    def test_broken_reference_fails(self):
        self.patch('docs/features/FEAT-DP-003-organization/us/US-DP-003-001.md',
                   '"FR-DP-003-001"', '"FR-DP-003-999"')
        self.assertIn('invalid or absent fr_refs',self.run_cli('validate',expected=1))

    def test_approval_without_evidence_fails(self):
        self.patch('docs/features/FEAT-DP-003-organization/ur/UR-DP-003-001.md',
                   'status: "draft"', 'status: "approved"')
        self.assertIn('approved requires verifiable approval_ref',self.run_cli('validate',expected=1))

    def test_dependency_cycle_fails(self):
        self.run_cli('new-artifact','--type','TASK','--feature','FEAT-DP-003','--title','Second task')
        p = self.feat/'tasks/TASK-DP-003-002.md'
        body=p.read_text().replace('us_refs: []','us_refs: ["US-DP-003-002"]').replace('depends_on: []','depends_on: ["TASK-DP-003-001"]')
        p.write_text(body)
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'depends_on: []','depends_on: ["TASK-DP-003-002"]')
        self.assertIn('dependency cycle',self.run_cli('validate',expected=1))

    def test_broken_markdown_link_and_anchor_fail(self):
        self.patch('docs/roadmap.md', 'features/FEAT-DP-003-organization/feature.md', 'features/nonexistent.md')
        self.assertIn('broken markdown link',self.run_cli('validate',expected=1))

    def test_pending_task_requires_codebase_verification(self):
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'status: "blocked"', 'status: "pending"')
        self.assertIn('codebase_verified=true',self.run_cli('validate',expected=1))

    def test_task_requires_traceability_source(self):
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'us_refs: ["US-DP-003-001"]', 'us_refs: []')
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'fr_refs: ["FR-DP-003-001"]', 'fr_refs: []')
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'nfr_refs: ["NFR-001", "NFR-002", "NFR-004"]', 'nfr_refs: []')
        self.assertIn('task has no US/FR/NFR',self.run_cli('validate',expected=1))

    def test_selected_executor_skill_exists(self):
        self.run_cli('validate')
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'implementation_skill: "add-cqrs-api-feature"',
                   'implementation_skill: "nonexistent-skill"')
        self.assertIn('unknown implementation_skill',self.run_cli('validate',expected=1))

    def test_invalid_executor_skill_name_fails(self):
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'implementation_skill: "add-cqrs-api-feature"',
                   'implementation_skill: "../unsafe"')
        self.assertIn('invalid implementation_skill',self.run_cli('validate',expected=1))

    def test_blocked_task_preflight_fails(self):
        self.assertIn('task status must be pending',
                      self.run_cli('check-task', '--id', 'TASK-DP-003-001',expected=1))

    def test_pending_task_with_unready_sources_fails(self):
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'status: "blocked"', 'status: "pending"')
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'codebase_verified: false', 'codebase_verified: true')
        self.assertIn('source requirement',
                      self.run_cli('check-task','--id','TASK-DP-003-001',expected=1))

    def test_preflight_passes_ready_sources_after_review(self):
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'status: "blocked"', 'status: "pending"')
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'codebase_verified: false', 'codebase_verified: true')
        for kind,identifier in [('ur','UR-DP-003-001'),('fr','FR-DP-003-001'),('us','US-DP-003-001')]:
            rel=f'docs/features/FEAT-DP-003-organization/{kind}/{identifier}.md'
            p=self.root/rel
            src=p.read_text()
            self.assertIn('status: "draft"',src)
            p.write_text(src.replace('status: "draft"','status: "ready"',1))
        self.assertIn('structurally eligible',
                      self.run_cli('check-task','--id','TASK-DP-003-001'))

    def test_dependency_preflight_requires_done(self):
        self.run_cli('new-artifact','--type','TASK','--feature','FEAT-DP-003','--title','Wait on task')
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'depends_on: []', 'depends_on: ["TASK-DP-003-002"]')
        self.assertIn('dependency TASK-DP-003-002 must be done',
                      self.run_cli('check-task','--id','TASK-DP-003-001',expected=1))

    def test_in_progress_requires_explicit_resume(self):
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'status: "blocked"', 'status: "in_progress"')
        self.patch('docs/features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md',
                   'codebase_verified: false', 'codebase_verified: true')
        self.assertIn('with --resume',self.run_cli('check-task','--id','TASK-DP-003-001',expected=1))
        output=self.run_cli('check-task','--id','TASK-DP-003-001','--resume',expected=1)
        self.assertIn('source requirement',output)
        self.assertNotIn('task status must be pending',output)

    def test_new_artifact_does_not_reuse_id(self):
        self.run_cli('new-artifact','--type','US','--feature','FEAT-DP-003','--title','New story')
        self.run_cli('new-artifact','--type','US','--feature','FEAT-DP-003','--title','More story')
        self.assertTrue((self.feat/'us/US-DP-003-005.md').exists())
        self.assertTrue((self.feat/'us/US-DP-003-006.md').exists())
        self.run_cli('validate')


    def test_documentation_commands_leave_dotnet_tests_untouched(self):
        project = self.root / 'tests' / 'DeliveryPilot.Identity.UnitTests'
        project.mkdir(parents=True)
        csproj = project / 'DeliveryPilot.Identity.UnitTests.csproj'
        csproj.write_text('<Project Sdk="Microsoft.NET.Sdk" />')
        self.assertIn('0 errors', self.run_cli('validate'))
        self.assertEqual('<Project Sdk="Microsoft.NET.Sdk" />', csproj.read_text())

    def test_safe_legacy_cleanup_refuses_unknown_file(self):
        legacy = self.root / 'tests' / 'test_docs.py'
        legacy.parent.mkdir()
        legacy.write_text('# user-owned test, never delete')
        migration = self.root / 'tools' / 'docs_validation' / 'migrate_legacy_tests.py'
        proc = subprocess.run([sys.executable, str(migration), '--root', str(self.root), '--apply'],
                              capture_output=True, text=True)
        self.assertEqual(2, proc.returncode, proc.stdout + proc.stderr)
        self.assertIn('REFUSED', proc.stdout)
        self.assertEqual('# user-owned test, never delete', legacy.read_text())

    def test_safe_legacy_cleanup_recognized_file_requires_apply(self):
        import hashlib
        old = self.root / 'tests' / 'test_docs.py'
        old.parent.mkdir()
        old.write_text('# generated documentation workflow test fixture')
        other = self.root / 'tests' / 'UserTests.cs'
        other.write_text('// existing C# test')
        migration = self.root / 'tools' / 'docs_validation' / 'migrate_legacy_tests.py'
        digest = hashlib.sha256(old.read_bytes()).hexdigest()
        source = migration.read_text()
        migration.write_text(source.replace('KNOWN_HASHES = {', "KNOWN_HASHES = {\n    '" + digest + "',", 1))
        command = [sys.executable, str(migration), '--root', str(self.root)]
        dry = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(0, dry.returncode, dry.stdout + dry.stderr)
        self.assertTrue(old.exists())
        apply = subprocess.run(command + ['--apply'], capture_output=True, text=True)
        self.assertEqual(0, apply.returncode, apply.stdout + apply.stderr)
        self.assertFalse(old.exists())
        self.assertEqual('// existing C# test', other.read_text())


if __name__ == '__main__':
    unittest.main()

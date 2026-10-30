import tempfile
import unittest
from pathlib import Path
from check_task_graph import check


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')


def fixture(first='proposed', depends=None, accepted=False, first_approval=False):
    tmp = tempfile.TemporaryDirectory()
    root = Path(tmp.name) / 'docs/features/FEAT-DP-001-blocker-detection'
    approval = 'PR-21' if accepted else 'null'
    srcstatus = 'accepted' if accepted else 'proposed'
    write(root / 'requirements.md', f'---\nid: REQ\nfeature_id: FEAT-DP-001\nstatus: {srcstatus}\napproval_ref: {approval}\n---\n| ID | Behavior |\n|---|---|\n| FR-DP-001 | Explain risk |\n')
    write(root / 'design/solution.md', f'---\nid: SOL\nstatus: {srcstatus}\napproval_ref: {approval}\n---\n# Design\n')
    write(root / 'tasks/TASK-DP-001-normalize.md', f'''---
id: TASK-DP-001
feature_id: FEAT-DP-001
status: {first}
approval_ref: {'PR-22' if first_approval else 'null'}
delivery_mode: production
depends_on: []
spec_refs:
  - ../requirements.md
  - ../design/solution.md
acceptance_refs:
  - FR-DP-001
---
# Normalize
''')
    deps = '\n  - TASK-DP-001' if depends is None else depends
    write(root / 'tasks/TASK-DP-002-evaluate.md', f'''---
id: TASK-DP-002
feature_id: FEAT-DP-001
status: proposed
delivery_mode: production
depends_on:{deps}
spec_refs:
  - ../requirements.md
---
# Evaluate
''')
    return tmp, root


class GraphTests(unittest.TestCase):
    def test_valid_proposed_dag(self):
        tmp, root = fixture()
        with tmp:
            self.assertEqual(check(root)[0], [])

    def test_unknown_dependency(self):
        tmp, root = fixture(depends='\n  - TASK-DP-999')
        with tmp:
            self.assertTrue(any('unknown dependency' in e for e in check(root)[0]))

    def test_cycle(self):
        tmp, root = fixture(depends='\n  - TASK-DP-001')
        with tmp:
            p = root / 'tasks/TASK-DP-001-normalize.md'
            p.write_text(p.read_text(encoding='utf-8').replace('depends_on: []', 'depends_on:\n  - TASK-DP-002'), encoding='utf-8')
            self.assertTrue(any('dependency cycle' in e for e in check(root)[0]))

    def test_ready_requires_source_approval(self):
        tmp, root = fixture(first='ready', first_approval=True)
        with tmp:
            self.assertTrue(any('accepted requirements' in e for e in check(root)[0]))

    def test_ready_accepted_sources_ok(self):
        tmp, root = fixture(first='ready', accepted=True, first_approval=True)
        with tmp:
            self.assertEqual(check(root)[0], [])

    def test_completed_requires_verified_evidence(self):
        tmp, root = fixture(first='completed')
        with tmp:
            self.assertTrue(any('evidence_ref' in e for e in check(root)[0]))

    def test_nonexistent_spec_ref(self):
        tmp, root = fixture()
        with tmp:
            p = root / 'tasks/TASK-DP-001-normalize.md'
            p.write_text(p.read_text().replace('../requirements.md', '../missing.md'), encoding='utf-8')
            self.assertTrue(any('spec_ref' in e for e in check(root)[0]))


if __name__ == '__main__':
    unittest.main()

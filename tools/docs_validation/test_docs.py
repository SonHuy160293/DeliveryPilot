"""End-to-end checks for DeliveryPilot Markdown CLI without extra dependencies."""
import importlib.util
import json
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from urllib.parse import unquote

SOURCE = Path(__file__).resolve().parents[2]
LINK_RE = re.compile(r'(?<!!)\[[^\]]*\]\(([^)]+)\)')
FIX = 'docs/features/FEAT-DP-099-fixture'
TASK = f'{FIX}/tasks/TASK-DP-099-001.md'
STARTED_RE = re.compile(r'^started_at: "\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z"$', re.M)

FIXTURE_FILES = {
    'feature.md': '''---
id: "FEAT-DP-099"
title: "Fixture"
status: "draft"
priority: "P1"
release: "MVP"
depends_on: []
---
# FEAT-DP-099 — Fixture

## Problem and business outcome
Self-contained fixture for the docs CLI tests.

## Actors and scope
Test actor only.

## Dependencies and decisions
None.
''',
    'ur/UR-DP-099-001.md': '''---
id: "UR-DP-099-001"
feature_id: "FEAT-DP-099"
title: "Fixture need"
status: "draft"
priority: "P1"
actor: "Tester"
source_refs: []
approval_ref: null
---
# UR-DP-099-001 — Fixture need

## Problem
Fixture.

## User need
Fixture.

## Business value
Fixture.

## Open questions
None.
''',
    'fr/FR-DP-099-001.md': '''---
id: "FR-DP-099-001"
feature_id: "FEAT-DP-099"
title: "Fixture behavior"
status: "draft"
priority: "P1"
ur_refs: ["UR-DP-099-001"]
nfr_refs: []
approval_ref: null
---
# FR-DP-099-001 — Fixture behavior

## Requirement statement
Fixture.

## Main behavior
Fixture.

## Verification criteria
Fixture.

## Open questions
None.
''',
    'us/US-DP-099-001.md': '''---
id: "US-DP-099-001"
feature_id: "FEAT-DP-099"
title: "Fixture story"
status: "draft"
priority: "P1"
fr_refs: ["FR-DP-099-001"]
depends_on: []
approval_ref: null
---
# US-DP-099-001 — Fixture story

## Story
As a tester I want a fixture.

## Acceptance criteria
Given a fixture When tests run Then they are stable.

## Open questions
None.
''',
    'tasks/TASK-DP-099-001.md': '''---
id: "TASK-DP-099-001"
feature_id: "FEAT-DP-099"
title: "Fixture task"
status: "blocked"
priority: "P1"
type: "backend"
implementation_skill: "add-cqrs-api-feature"
us_refs: ["US-DP-099-001"]
fr_refs: ["FR-DP-099-001"]
nfr_refs: ["NFR-001", "NFR-002", "NFR-004"]
depends_on: []
codebase_verified: false
---
# TASK-DP-099-001 — Fixture task

## Objective and source contract
Fixture.

## Codebase evidence
Fixture.

## Test matrix
Fixture.

## Acceptance and definition of done
- [ ] First criterion.
- [ ] Second criterion.

## Unresolved decisions
None. Fixture has no open decision.
''',
}


class DocsCliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        shutil.copytree(SOURCE / 'docs', self.root / 'docs')
        shutil.copy2(SOURCE / 'ARCHITECTURE.md', self.root / 'ARCHITECTURE.md')
        shutil.copytree(SOURCE / 'tools', self.root / 'tools')
        shutil.copytree(SOURCE / '.agents' / 'skills', self.root / '.agents' / 'skills')
        self.mirror_linked_targets()
        self.add_fixture_feature()
        self.feat = self.root / FIX

    def mirror_linked_targets(self):
        """Docs link into src/, tests/, deploy/ and root files; mirror only those targets so link checks see the repository."""
        for md in (self.root / 'docs').rglob('*.md'):
            for link in LINK_RE.findall(md.read_text(encoding='utf-8')):
                rel = unquote(link.strip().split(' ')[0].strip('<>').split('#')[0])
                if not rel or re.match(r'^(https?://|mailto:|data:)', rel):
                    continue
                source = (SOURCE / md.parent.relative_to(self.root) / rel).resolve()
                if not source.exists() or not source.is_relative_to(SOURCE):
                    continue
                target = self.root / source.relative_to(SOURCE)
                if target.exists():
                    continue
                if source.is_dir():
                    target.mkdir(parents=True, exist_ok=True)
                else:
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(source, target)

    def add_fixture_feature(self):
        """Tests mutate this fixture instead of live feature files, so they survive real documentation changes."""
        roadmap = self.root / 'docs' / 'roadmap.md'
        roadmap.write_text(roadmap.read_text(encoding='utf-8') + '\n| FEAT-DP-099 | Fixture | Test | CLI test fixture |\n',
                           encoding='utf-8')
        for rel, text in FIXTURE_FILES.items():
            path = self.root / FIX / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding='utf-8')

    def run_cli(self, *args, expected=0):
        p = subprocess.run([sys.executable, str(self.root / 'tools' / 'docs.py'), *args],
                           cwd=self.root, capture_output=True, text=True, encoding='utf-8')
        self.assertEqual(expected, p.returncode, p.stdout + '\n' + p.stderr)
        return p.stdout + '\n' + p.stderr

    def patch(self, rel, old, new):
        p = self.root / rel
        body = p.read_text(encoding='utf-8')
        self.assertIn(old, body)
        p.write_text(body.replace(old, new, 1), encoding='utf-8')

    def read(self, rel=TASK):
        return (self.root / rel).read_text(encoding='utf-8')

    def make_startable(self):
        self.patch(TASK, 'status: "blocked"', 'status: "pending"')
        self.patch(TASK, 'codebase_verified: false', 'codebase_verified: true')
        for kind, identifier in [('ur', 'UR-DP-099-001'), ('fr', 'FR-DP-099-001'), ('us', 'US-DP-099-001')]:
            self.patch(f'{FIX}/{kind}/{identifier}.md', 'status: "draft"', 'status: "ready"')

    def summary(self, module='Projects', result='passed', finished='2999-01-01T00:00:00Z',
                skipped=0, db=0, failed=0, **extra):
        data = {'module': module, 'commit': 'abcdef0123456789abcdef0123456789abcdef01', 'startedAt': finished,
                'finishedAt': finished, 'requireDb': False, 'dbVariable': 'DELIVERYPILOT_TEST_PROJECTS_POSTGRES',
                'dbConfigured': db == 0, 'invocation': f'scripts/Verify-Module.ps1 -Module {module}',
                'steps': [{'name': 'test', 'command': 'dotnet test tests/X.csproj --no-build', 'status': 'passed',
                           'exitCode': 0, 'durationSeconds': 1.5}],
                'tests': {'total': 10 + skipped, 'passed': 10 - failed, 'failed': failed, 'skipped': skipped,
                          'dbSkipped': db, 'skipReasons': {'Set DELIVERYPILOT_TEST_PROJECTS_POSTGRES to a db': db} if db else {}},
                'trxPath': f'artifacts/verify/{module}/x/verify.trx', 'result': result}
        data.update(extra)
        path = self.root / f'summary-{module}-{result}-{finished[:4]}.json'
        path.write_text(json.dumps(data), encoding='utf-8')
        return str(path)

    def start(self):
        self.make_startable()
        self.run_cli('start-task', '--id', 'TASK-DP-099-001')

    # --- validation -------------------------------------------------------------------------------

    def test_repository_and_fixture_validate(self):
        self.assertIn(' 0 errors', self.run_cli('validate'))
        out = self.run_cli('coverage', '--feature', 'FEAT-DP-099')
        for line in ('UR: 1', 'FR: 1', 'US: 1', 'TASK: 1'):
            self.assertIn(line, out)

    def test_create_registered_feature_and_ur_without_overwriting(self):
        args=('scaffold-feature','--id','FEAT-DP-007','--title','Approval Workflow', '--slug','approval','--release','MVP','--priority','P0')
        self.run_cli(*args)
        self.run_cli(*args, expected=2)
        self.run_cli('new-artifact','--type','UR','--feature','FEAT-DP-007','--title','Approve a requirement','--actor','Client Approver')
        self.assertTrue((self.root/'docs/features/FEAT-DP-007-approval/ur/UR-DP-007-001.md').exists())
        self.assertIn('UR-DP-007-002',self.run_cli('next-id','--type','UR','--feature','FEAT-DP-007'))
        self.run_cli('validate')

    def test_duplicate_id_fails(self):
        original = self.feat/'ur/UR-DP-099-001.md'
        (self.feat/'ur/UR-DP-099-002.md').write_text(original.read_text(encoding='utf-8'), encoding='utf-8')
        self.assertIn('duplicate ID',self.run_cli('validate',expected=1))

    def test_broken_reference_fails(self):
        self.patch(f'{FIX}/us/US-DP-099-001.md', '"FR-DP-099-001"', '"FR-DP-099-999"')
        self.assertIn('invalid or absent fr_refs',self.run_cli('validate',expected=1))

    def test_approval_without_evidence_fails(self):
        self.patch(f'{FIX}/ur/UR-DP-099-001.md', 'status: "draft"', 'status: "approved"')
        self.assertIn('approved requires verifiable approval_ref',self.run_cli('validate',expected=1))

    def test_dependency_cycle_fails(self):
        self.run_cli('new-artifact','--type','TASK','--feature','FEAT-DP-099','--title','Second task')
        p = self.feat/'tasks/TASK-DP-099-002.md'
        body=p.read_text(encoding='utf-8').replace('us_refs: []','us_refs: ["US-DP-099-001"]').replace('depends_on: []','depends_on: ["TASK-DP-099-001"]')
        p.write_text(body, encoding='utf-8')
        self.patch(TASK, 'depends_on: []','depends_on: ["TASK-DP-099-002"]')
        self.assertIn('dependency cycle',self.run_cli('validate',expected=1))

    def test_broken_markdown_link_and_anchor_fail(self):
        self.patch('docs/roadmap.md', 'features/FEAT-DP-003-organization/feature.md', 'features/nonexistent.md')
        self.assertIn('broken markdown link',self.run_cli('validate',expected=1))

    def test_pending_task_requires_codebase_verification(self):
        self.patch(TASK, 'status: "blocked"', 'status: "pending"')
        self.assertIn('codebase_verified=true',self.run_cli('validate',expected=1))

    def test_task_requires_traceability_source(self):
        self.patch(TASK, 'us_refs: ["US-DP-099-001"]', 'us_refs: []')
        self.patch(TASK, 'fr_refs: ["FR-DP-099-001"]', 'fr_refs: []')
        self.patch(TASK, 'nfr_refs: ["NFR-001", "NFR-002", "NFR-004"]', 'nfr_refs: []')
        self.assertIn('task has no US/FR/NFR',self.run_cli('validate',expected=1))

    def test_selected_executor_skill_exists(self):
        self.run_cli('validate')
        self.patch(TASK, 'implementation_skill: "add-cqrs-api-feature"', 'implementation_skill: "nonexistent-skill"')
        self.assertIn('unknown implementation_skill',self.run_cli('validate',expected=1))

    def test_invalid_executor_skill_name_fails(self):
        self.patch(TASK, 'implementation_skill: "add-cqrs-api-feature"', 'implementation_skill: "../unsafe"')
        self.assertIn('invalid implementation_skill',self.run_cli('validate',expected=1))

    def test_duplicate_section_heading_fails(self):
        self.patch(TASK, '## Test matrix\n', '## Test matrix\nFixture.\n\n## Test matrix\n')
        self.assertIn("duplicate section heading 'Test matrix'", self.run_cli('validate', expected=1))

    def test_duplicate_heading_inside_code_fence_is_ignored(self):
        self.patch(TASK, '## Test matrix\nFixture.\n', '## Test matrix\nFixture.\n\n```markdown\n## Test matrix\n```\n')
        self.assertIn(' 0 errors', self.run_cli('validate'))

    def test_lifecycle_fields_are_validated(self):
        self.make_startable()
        self.patch(TASK, 'status: "pending"', 'status: "in_progress"')
        self.assertIn('requires started_at', self.run_cli('validate', expected=1))
        self.patch(TASK, 'codebase_verified: true', 'codebase_verified: true\nstarted_at: "2026-11-10 08:00"\nbase_commit: "XYZ"')
        out = self.run_cli('validate', expected=1)
        self.assertIn('invalid started_at', out)
        self.assertIn('invalid base_commit', out)

    # --- check-task (unchanged preflight) ---------------------------------------------------------

    def test_blocked_task_preflight_fails(self):
        self.assertIn('task status must be pending',
                      self.run_cli('check-task', '--id', 'TASK-DP-099-001',expected=1))

    def test_pending_task_with_unready_sources_fails(self):
        self.patch(TASK, 'status: "blocked"', 'status: "pending"')
        self.patch(TASK, 'codebase_verified: false', 'codebase_verified: true')
        self.assertIn('source requirement', self.run_cli('check-task','--id','TASK-DP-099-001',expected=1))

    def test_preflight_passes_ready_sources_after_review(self):
        self.make_startable()
        self.assertIn('structurally eligible', self.run_cli('check-task','--id','TASK-DP-099-001'))

    def test_dependency_preflight_requires_done(self):
        self.run_cli('new-artifact','--type','TASK','--feature','FEAT-DP-099','--title','Wait on task')
        self.patch(TASK, 'depends_on: []', 'depends_on: ["TASK-DP-099-002"]')
        self.assertIn('dependency TASK-DP-099-002 must be done',
                      self.run_cli('check-task','--id','TASK-DP-099-001',expected=1))

    def test_in_progress_requires_explicit_resume(self):
        self.patch(TASK, 'status: "blocked"', 'status: "in_progress"')
        self.patch(TASK, 'codebase_verified: false', 'codebase_verified: true\nstarted_at: "2026-11-10T08:00:00Z"')
        self.assertIn('with --resume',self.run_cli('check-task','--id','TASK-DP-099-001',expected=1))
        output=self.run_cli('check-task','--id','TASK-DP-099-001','--resume',expected=1)
        self.assertIn('source requirement',output)
        self.assertNotIn('task status must be pending',output)

    def test_new_artifact_does_not_reuse_id(self):
        self.run_cli('new-artifact','--type','US','--feature','FEAT-DP-099','--title','New story')
        self.run_cli('new-artifact','--type','US','--feature','FEAT-DP-099','--title','More story')
        self.assertTrue((self.feat/'us/US-DP-099-002.md').exists())
        self.assertTrue((self.feat/'us/US-DP-099-003.md').exists())
        self.run_cli('validate')

    def test_new_task_from_template_validates(self):
        self.run_cli('new-artifact','--type','TASK','--feature','FEAT-DP-099','--title','Template task')
        text = self.read(f'{FIX}/tasks/TASK-DP-099-002.md')
        self.assertIn('started_at: null\nbase_commit: null\n---', text)
        self.assertIn('## Execution evidence', text)
        self.patch(f'{FIX}/tasks/TASK-DP-099-002.md', 'nfr_refs: []', 'nfr_refs: ["NFR-011"]')
        self.assertIn(' 0 errors', self.run_cli('validate'))

    # --- start-task -------------------------------------------------------------------------------

    def test_start_task_records_start_and_changes_only_lifecycle_lines(self):
        self.make_startable()
        before = self.read()
        out = self.run_cli('start-task', '--id', 'TASK-DP-099-001')
        self.assertIn('pending -> in_progress', out)
        self.assertIn('base_commit not recorded', out)
        after = self.read()
        self.assertRegex(after, STARTED_RE)
        self.assertIn('\nbase_commit: null\n', after)
        expected = before.replace('status: "pending"', 'status: "in_progress"')
        stripped = re.sub(r'^(started_at|base_commit): .*\n', '', after, flags=re.M)
        self.assertEqual(expected, stripped)
        self.assertIn(' 0 errors', self.run_cli('validate'))

    def test_start_task_keeps_bare_status_and_crlf(self):
        self.make_startable()
        path = self.root / TASK
        text = path.read_text(encoding='utf-8').replace('status: "pending"', 'status: pending')
        path.write_bytes(text.replace('\n', '\r\n').encode('utf-8'))
        self.run_cli('start-task', '--id', 'TASK-DP-099-001')
        raw = path.read_bytes()
        self.assertNotIn(b'\n', raw.replace(b'\r\n', b''))
        self.assertIn(b'status: in_progress\r\n', raw)

    @unittest.skipUnless(shutil.which('git'), 'git not on PATH')
    def test_start_task_records_git_commit(self):
        self.make_startable()
        git = ['git', '-C', str(self.root), '-c', 'user.email=docs-test@example.invalid', '-c', 'user.name=docs-test',
               '-c', 'commit.gpgsign=false', '-c', 'core.autocrlf=false']
        subprocess.run(git + ['init', '-q'], check=True)
        subprocess.run(git + ['add', '-A'], check=True)
        subprocess.run(git + ['commit', '-qm', 'fixture'], check=True)
        head = subprocess.run(git + ['rev-parse', 'HEAD'], capture_output=True, text=True, check=True).stdout.strip()
        self.run_cli('start-task', '--id', 'TASK-DP-099-001')
        self.assertIn(f'base_commit: "{head}"', self.read())

    def assert_start_refused(self, message):
        before = self.read()
        self.assertIn(message, self.run_cli('start-task', '--id', 'TASK-DP-099-001', expected=1))
        self.assertEqual(before, self.read())

    def test_start_task_refuses_blocked_task(self):
        self.assert_start_refused('task status must be pending')

    def test_start_task_refuses_unready_sources(self):
        self.patch(TASK, 'status: "blocked"', 'status: "pending"')
        self.patch(TASK, 'codebase_verified: false', 'codebase_verified: true')
        self.assert_start_refused('not ready/approved')

    def test_start_task_refuses_unmet_dependency(self):
        self.make_startable()
        self.run_cli('new-artifact','--type','TASK','--feature','FEAT-DP-099','--title','Prerequisite')
        self.patch(TASK, 'depends_on: []', 'depends_on: ["TASK-DP-099-002"]')
        self.assert_start_refused('must be done')

    def test_start_task_refuses_open_decision(self):
        self.make_startable()
        self.patch(TASK, 'None. Fixture has no open decision.', 'List blockers. Open question on scope.')
        self.assert_start_refused('unresolved decisions must start with')

    def test_start_task_refuses_missing_decisions_section(self):
        self.make_startable()
        self.patch(TASK, '## Unresolved decisions\nNone. Fixture has no open decision.\n', '')
        self.assert_start_refused("missing section matching 'Unresolved decisions'")

    def test_start_task_refuses_task_already_in_progress(self):
        self.start()
        self.assert_start_refused('already in progress')

    # --- verify / submit / return / complete ------------------------------------------------------

    def test_full_lifecycle_with_recorded_evidence(self):
        self.start()
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects', '--from-summary', self.summary())
        text = self.read()
        self.assertIn('## Execution evidence\n\n### Verify-Module: Projects', text)
        self.assertIn('result **passed**', text)
        self.assertIn('- Tests: 10 passed, 0 failed, 0 skipped (0 database)', text)
        self.assertLess(text.index('## Execution evidence'), text.index('## Unresolved decisions'))
        self.run_cli('validate')
        self.assertIn('in_progress -> in_review', self.run_cli('submit-task', '--id', 'TASK-DP-099-001'))
        self.assertIn('unchecked items', self.run_cli('complete-task', '--id', 'TASK-DP-099-001', expected=1))
        self.patch(TASK, '- [ ] First', '- [x] First')
        self.patch(TASK, '- [ ] Second', '- [x] Second')
        self.assertIn('in_review -> done', self.run_cli('complete-task', '--id', 'TASK-DP-099-001'))
        self.assertIn('status: "done"', self.read())
        self.assertIn(' 0 errors', self.run_cli('validate'))

    def test_return_task_sends_review_back(self):
        self.start()
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects', '--from-summary', self.summary())
        self.run_cli('submit-task', '--id', 'TASK-DP-099-001')
        self.assertIn('in_review -> in_progress', self.run_cli('return-task', '--id', 'TASK-DP-099-001'))

    def test_submit_and_complete_require_evidence_and_status(self):
        self.start()
        before = self.read()
        self.assertIn('run verify-task or submit-task --no-verify', self.run_cli('submit-task', '--id', 'TASK-DP-099-001', expected=1))
        self.assertIn('needs status in_review', self.run_cli('complete-task', '--id', 'TASK-DP-099-001', expected=1))
        self.assertEqual(before, self.read())

    def test_stale_evidence_is_refused(self):
        self.start()
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects',
                     '--from-summary', self.summary(finished='2000-01-01T00:00:00Z'))
        self.assertIn('predates started_at', self.run_cli('submit-task', '--id', 'TASK-DP-099-001', expected=1))

    def test_failed_verification_is_recorded_then_replaced(self):
        self.start()
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects',
                     '--from-summary', self.summary(result='failed', failed=2), expected=1)
        self.assertIn('result **failed**', self.read())
        self.assertIn('verification result is failed', self.run_cli('submit-task', '--id', 'TASK-DP-099-001', expected=1))
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects', '--from-summary', self.summary())
        text = self.read()
        self.assertEqual(1, text.count('### Verify-Module: Projects'))
        self.assertNotIn('result **failed**', text)
        self.run_cli('submit-task', '--id', 'TASK-DP-099-001')

    def test_database_skips_need_recorded_acceptance(self):
        self.start()
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects',
                     '--from-summary', self.summary(result='passed-with-skips', skipped=3, db=3))
        self.assertIn('3 database tests skipped', self.run_cli('submit-task', '--id', 'TASK-DP-099-001', expected=1))
        self.assertIn('non-empty reason',
                      self.run_cli('submit-task', '--id', 'TASK-DP-099-001', '--accept-skips', ' ', expected=1))
        self.run_cli('submit-task', '--id', 'TASK-DP-099-001', '--accept-skips', 'no PostgreSQL in this environment')
        text = self.read()
        self.assertIn('- Accepted skips: no PostgreSQL in this environment', text)
        self.assertIn('status: "in_review"', text)
        self.run_cli('validate')

    def test_no_verify_reason_allows_submit(self):
        self.start()
        self.run_cli('submit-task', '--id', 'TASK-DP-099-001', '--no-verify', 'documentation-only task')
        text = self.read()
        self.assertIn('## Execution evidence\n\n- No verification: documentation-only task', text)
        self.assertIn('status: "in_review"', text)
        self.run_cli('validate')

    def test_multiple_modules_and_hand_written_notes_are_preserved(self):
        self.start()
        self.patch(TASK, '## Unresolved decisions', '## Execution evidence\n\nManual note: migration reviewed.\n\n## Unresolved decisions')
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects', '--from-summary', self.summary())
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Gateway', '--from-summary', self.summary(module='Gateway'))
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects',
                     '--from-summary', self.summary(finished='2999-02-02T00:00:00Z'))
        text = self.read()
        self.assertIn('Manual note: migration reviewed.', text)
        self.assertEqual(1, text.count('### Verify-Module: Projects'))
        self.assertEqual(1, text.count('### Verify-Module: Gateway'))
        self.assertIn('2999-02-02T00:00:00Z', text)
        self.run_cli('submit-task', '--id', 'TASK-DP-099-001')

    def test_unexpected_summary_fields_are_not_copied(self):
        self.start()
        self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects',
                     '--from-summary', self.summary(connectionString='Host=db;Password=hunter2'))
        self.assertNotIn('hunter2', self.read())

    def test_verify_task_requires_in_progress_and_matching_module(self):
        self.make_startable()
        before = self.read()
        self.assertIn('needs status in_progress', self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Projects',
                                                               '--from-summary', self.summary(), expected=1))
        self.assertEqual(before, self.read())
        self.run_cli('start-task', '--id', 'TASK-DP-099-001')
        self.assertIn('does not match --module', self.run_cli('verify-task', '--id', 'TASK-DP-099-001', '--module', 'Identity',
                                                               '--from-summary', self.summary(), expected=1))

    def test_section_text_accepts_legacy_dated_evidence_heading(self):
        spec = importlib.util.spec_from_file_location('docs_cli', self.root / 'tools' / 'docs.py')
        docs = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(docs)
        body = '## Execution evidence — 2026-11-10\n\nRan tests.\n\n## Unresolved decisions\nNone.\n'
        self.assertEqual('Ran tests.', docs.section_text(body, 'Execution evidence'))

    # --- test ownership ---------------------------------------------------------------------------

    def test_documentation_commands_leave_dotnet_tests_untouched(self):
        project = self.root / 'tests' / 'DeliveryPilot.Identity.UnitTests'
        project.mkdir(parents=True)
        csproj = project / 'DeliveryPilot.Identity.UnitTests.csproj'
        csproj.write_text('<Project Sdk="Microsoft.NET.Sdk" />')
        self.assertIn(' 0 errors', self.run_cli('validate'))
        self.assertEqual('<Project Sdk="Microsoft.NET.Sdk" />', csproj.read_text())

    def test_safe_legacy_cleanup_refuses_unknown_file(self):
        legacy = self.root / 'tests' / 'test_docs.py'
        legacy.parent.mkdir(exist_ok=True)
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
        old.parent.mkdir(exist_ok=True)
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

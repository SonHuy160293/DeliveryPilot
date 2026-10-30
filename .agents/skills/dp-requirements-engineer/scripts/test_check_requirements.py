import tempfile
import unittest
from pathlib import Path
from check_requirements import check


def fixture(text):
    tmp = tempfile.TemporaryDirectory()
    folder = Path(tmp.name) / 'docs' / 'features' / 'FEAT-DP-001-blockers'
    folder.mkdir(parents=True)
    path = folder / 'requirements.md'
    path.write_text(text, encoding='utf-8')
    return tmp, path


VALID = '''---
id: DOC-REQ
feature_id: FEAT-DP-001
status: proposed
approval_ref: null
---
## Functional requirements
| ID | Behavior |
|---|---|
| FR-DP-001 | Show evidence |
## Acceptance scenarios
### AC-01: FR-DP-001
Given input When read Then show source
'''


class RequirementsTests(unittest.TestCase):
    def test_valid_coverage(self):
        tmp, path = fixture(VALID)
        with tmp:
            self.assertEqual(check(path, strict=True)[0], [])

    def test_missing_coverage_strict_fails(self):
        tmp, path = fixture(VALID.replace('### AC-01: FR-DP-001', '### AC-01'))
        with tmp:
            self.assertTrue(any('without explicit acceptance' in e for e in check(path, strict=True)[0]))

    def test_duplicate_fr_fails(self):
        tmp, path = fixture(VALID.replace('## Acceptance scenarios', '| FR-DP-001 | Duplicate |\n## Acceptance scenarios'))
        with tmp:
            self.assertTrue(any('duplicate FR' in e for e in check(path)[0]))

    def test_accepted_without_approval_fails(self):
        tmp, path = fixture(VALID.replace('status: proposed', 'status: accepted'))
        with tmp:
            self.assertTrue(any('approval_ref' in e for e in check(path)[0]))


if __name__ == '__main__':
    unittest.main()

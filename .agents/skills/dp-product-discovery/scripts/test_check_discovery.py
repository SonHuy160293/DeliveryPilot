import tempfile
import unittest
from pathlib import Path
from check_discovery import check


class DiscoveryTests(unittest.TestCase):
    def test_proposed_valid_local_link(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'vision.md'
            (Path(tmp) / 'scope.md').write_text('scope', encoding='utf-8')
            p.write_text('---\nid: DOC-1\ntitle: Vision\nstatus: proposed\napproval_ref: null\n---\n[scope](scope.md)\n', encoding='utf-8')
            self.assertEqual(check(p)[0], [])

    def test_accepted_without_review_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'vision.md'
            p.write_text('---\nid: DOC-1\ntitle: Vision\nstatus: accepted\napproval_ref: null\n---\n', encoding='utf-8')
            self.assertTrue(any('approval_ref' in msg for msg in check(p)[0]))

    def test_broken_internal_link_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'vision.md'
            p.write_text('---\nid: DOC-1\ntitle: Vision\nstatus: proposed\n---\n[nope](missing.md)\n', encoding='utf-8')
            self.assertTrue(any('broken Markdown link' in msg for msg in check(p)[0]))


if __name__ == '__main__':
    unittest.main()

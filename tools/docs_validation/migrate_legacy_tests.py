#!/usr/bin/env python3
"""Safely retire only an unmodified legacy DeliveryPilot documentation test at root tests/test_docs.py.

Default mode only inspects; --apply removes a known old generated file. Do not remove
or change an unknown test or any other file in the user's root tests directory.
"""
import argparse
import hashlib
from pathlib import Path

KNOWN_HASHES = {
    'c6a9149dc6c990c864c97145286386fc8aba61baf13cbb72640afb83a77236e1',
    '7188dd1e19db15ae02c3b787295cabf77b0596e9c795fe0de7ddf2f48ab9278a',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    candidate = args.root.resolve() / 'tests' / 'test_docs.py'
    if not candidate.exists() and not candidate.is_symlink():
        print('OK: no legacy docs test file in root tests/.')
        return 0
    if candidate.is_symlink() or not candidate.is_file():
        print('REFUSED: the candidate is not a normal file. No changes made.')
        return 2
    sha = hashlib.sha256(candidate.read_bytes()).hexdigest()
    if sha not in KNOWN_HASHES:
        print('REFUSED: root tests/test_docs.py is unknown or modified. No changes made.')
        return 2
    if not args.apply:
        print('DRY RUN: known obsolete DeliveryPilot-generated root tests/test_docs.py detected.')
        print('Run with --apply to delete ONLY this verified old file; root tests/ is preserved.')
        return 0
    candidate.unlink()
    print('OK: removed the verified legacy docs test; other root tests/ files are untouched.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

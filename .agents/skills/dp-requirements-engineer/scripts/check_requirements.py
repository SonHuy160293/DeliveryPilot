#!/usr/bin/env python3
"""Validate a DeliveryPilot feature's requirements IDs and acceptance coverage.

Stdlib only. Proposed drafts warn on incomplete coverage; --strict makes it fail.
Usage: python check_requirements.py docs/features/FEAT-DP-001-blocker-detection/requirements.md --strict
"""
import argparse
import re
import sys
from pathlib import Path

HEAD = re.compile(r'\A---\s*\n(.*?)\n---\s*\n', re.S)
FIELD = re.compile(r'^([a-zA-Z][a-zA-Z0-9_]*):\s*(.*)$')
FR_ROW = re.compile(r'^\|\s*(FR-[A-Z0-9-]+)\s*\|', re.M)
FR_ANY = re.compile(r'\bFR-[A-Z0-9-]+\b')
AC_SECTION = re.compile(r'^##\s+Acceptance[^\n]*\n(.*?)(?=^##\s+|\Z)', re.M | re.S | re.I)


def read_md(path):
    text = path.read_text(encoding='utf-8')
    match = HEAD.match(text)
    if not match:
        raise ValueError(f'{path}: no YAML frontmatter')
    meta = {}
    for line in match.group(1).splitlines():
        field = FIELD.match(line)
        if field:
            key, val = field.groups()
            val = val.strip().strip('\'"')
            meta[key] = None if val.lower() in ('null', '~', '') else val
    return meta, text[match.end():]


def check(path, strict=False):
    errors, warnings = [], []
    if not path.is_file():
        return [f'missing requirements file: {path}'], warnings
    try:
        meta, body = read_md(path)
    except ValueError as e:
        return [str(e)], warnings
    feature_id = meta.get('feature_id')
    if not feature_id or not re.fullmatch(r'FEAT-[A-Z0-9-]+', feature_id):
        errors.append('requirements metadata must contain feature_id: FEAT-...')
    if not meta.get('id'):
        errors.append('requirements metadata lacks id')
    if feature_id and not path.parent.name.startswith(feature_id):
        errors.append(f'feature_id {feature_id} does not match parent directory {path.parent.name}')
    status = (meta.get('status') or '').lower()
    if status not in ('draft', 'hypothesis', 'proposed', 'accepted', 'rejected', 'superseded'):
        errors.append(f'invalid requirements status: {status!r}')
    if status == 'accepted' and not meta.get('approval_ref'):
        errors.append('accepted requirements need an independent approval_ref')
    frs = FR_ROW.findall(body)
    if not frs:
        errors.append('no FR-* declaration table rows found in requirements')
    duplicate = sorted({x for x in frs if frs.count(x) > 1})
    if duplicate:
        errors.append('duplicate FR declarations: ' + ', '.join(duplicate))
    ac = AC_SECTION.search(body)
    covered = set(FR_ANY.findall(ac.group(1))) if ac else set()
    if not ac:
        (errors if strict or status == 'accepted' else warnings).append('no ## Acceptance... section found')
    uncovered = sorted(set(frs) - covered)
    if uncovered:
        message = 'requirements without explicit acceptance-section FR references: ' + ', '.join(uncovered)
        (errors if strict or status == 'accepted' else warnings).append(message)
    for subdir, prefix in [('stories', 'US-'), ('use-cases', 'UC-')]:
        folder = path.parent / subdir
        ids = []
        if folder.is_dir():
            for doc in sorted(folder.glob('*.md')):
                if doc.name.endswith('-prompt.md') or doc.name == 'README.md':
                    continue
                try:
                    docmeta, docbody = read_md(doc)
                except ValueError as exc:
                    errors.append(str(exc))
                    continue
                ident = docmeta.get('id')
                if not ident or not ident.startswith(prefix):
                    errors.append(f'{doc}: needs {prefix} ID in frontmatter')
                else:
                    ids.append(ident)
                if docmeta.get('feature_id') != feature_id:
                    errors.append(f'{doc}: mismatched feature_id')
                if not (set(FR_ANY.findall(docbody)) & set(frs)):
                    warnings.append(f'{doc}: no recognized FR reference in content')
            if len(ids) != len(set(ids)):
                errors.append(f'duplicate {prefix} IDs in {folder}')
    return errors, warnings


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('requirements', type=Path)
    parser.add_argument('--strict', action='store_true', help='Treat missing FR acceptance references as errors')
    args = parser.parse_args()
    errors, warnings = check(args.requirements, args.strict)
    for err in errors: print('ERROR:', err)
    for warn in warnings: print('WARN: ', warn)
    print(f'Requirements check: {len(errors)} error(s), {len(warnings)} warning(s).')
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())

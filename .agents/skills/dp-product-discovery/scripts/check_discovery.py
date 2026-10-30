#!/usr/bin/env python3
"""Check DeliveryPilot discovery Markdown headers, approvals, and local links.

Stdlib only; intentional scope: does NOT attest to external source truth.
Usage: python check_discovery.py docs/product/vision.md [other-file.md ...]
"""
import argparse
import re
import sys
from pathlib import Path
from urllib.parse import unquote

HEADER = re.compile(r'\A---\s*\n(.*?)\n---\s*\n', re.S)
FIELD = re.compile(r'^([a-zA-Z][a-zA-Z0-9_]*):\s*(.*)$')
LINK = re.compile(r'(?<!!)\[[^\]]+\]\(([^)]+)\)')


def metadata(text):
    match = HEADER.match(text)
    if not match:
        return None
    info = {}
    for line in match.group(1).splitlines():
        field = FIELD.match(line)
        if field:
            value = field.group(2).split(' #', 1)[0].strip().strip('\'"')
            info[field.group(1)] = None if value.lower() in ('null', '~', '') else value
    return info


def check(path):
    errors, warnings = [], []
    if not path.is_file():
        return [f'missing document: {path}'], warnings
    text = path.read_text(encoding='utf-8')
    meta = metadata(text)
    if meta is None:
        return [f'{path}: missing YAML frontmatter'], warnings
    for required in ('id', 'title', 'status'):
        if not meta.get(required):
            errors.append(f'{path}: missing {required} metadata')
    status = (meta.get('status') or '').lower()
    if status not in ('draft', 'hypothesis', 'proposed', 'accepted', 'rejected', 'superseded'):
        errors.append(f'{path}: invalid discovery status {status!r}')
    if status == 'accepted' and not meta.get('approval_ref'):
        errors.append(f'{path}: accepted document needs a recorded approval_ref')
    if status == 'accepted' and ('_[To be completed' in text or '[Unverified]' in text):
        errors.append(f'{path}: accepted document still has unverified placeholder content')
    if re.search(r'https?://(?:example\.com|localhost)(?:/|\b)', text):
        warnings.append(f'{path}: example/local URLs are not external research evidence')
    for raw in LINK.findall(text):
        link = raw.strip().split(' "', 1)[0].strip('<>')
        if link.startswith(('http:', 'https:', 'mailto:', '#', 'data:')):
            continue
        part = unquote(link.split('#', 1)[0].split('?', 1)[0])
        if part and not (path.parent / part).exists():
            errors.append(f'{path}: broken Markdown link: {link}')
    return errors, warnings


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('files', nargs='+', type=Path)
    args = parser.parse_args()
    errors = warnings = 0
    for path in args.files:
        e, w = check(path)
        for item in e:
            print('ERROR:', item)
        for item in w:
            print('WARN: ', item)
        errors += len(e)
        warnings += len(w)
    print(f'Checked {len(args.files)} document(s): {errors} error(s), {warnings} warning(s).')
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())

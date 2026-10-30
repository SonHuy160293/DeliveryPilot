#!/usr/bin/env python3
"""Check DeliveryPilot task YAML subset, dependency DAG, links and readiness gates.

No third-party packages. This is a structural gate, not an approval verifier.
Usage: python check_task_graph.py docs/features/FEAT-DP-001-blocker-detection
"""
import argparse
import json
import re
import sys
from pathlib import Path

FR_ROW = re.compile(r'^\|\s*(FR-[A-Z0-9-]+)\s*\|', re.M)
FIELD = re.compile(r'^([A-Za-z][A-Za-z0-9_]*):\s*(.*)$')
LIST_ITEM = re.compile(r'^\s+-\s+(.+?)\s*$')
HEADER = re.compile(r'\A---\s*\n(.*?)\n---\s*\n', re.S)
VALID_STATUSES = {'proposed', 'ready', 'in_progress', 'blocked', 'completed', 'cancelled'}


def scalar(s):
    s = s.strip()
    if s.lower() in ('null', '~', ''):
        return None
    if s == '[]':
        return []
    if s.startswith('[') and s.endswith(']'):
        content = s[1:-1].strip()
        return [] if not content else [scalar(item) for item in content.split(',')]
    if s.startswith('"') and s.endswith('"'):
        try:
            return json.loads(s)
        except ValueError:
            return s[1:-1]
    if s.startswith("'") and s.endswith("'"):
        return s[1:-1].replace("''", "'")
    return s


def read_meta(path):
    text = path.read_text(encoding='utf-8')
    match = HEADER.match(text)
    if not match:
        raise ValueError(f'{path}: missing YAML frontmatter')
    meta = {}
    listkey = None
    for line in match.group(1).splitlines():
        field = FIELD.match(line)
        if field:
            key, value = field.groups()
            parsed = scalar(value)
            meta[key] = [] if parsed is None and not value.strip() else parsed
            listkey = key if not value.strip() else None
        elif listkey:
            item = LIST_ITEM.match(line)
            if item:
                meta[listkey].append(scalar(item.group(1)))
            elif line.strip() and not line.lstrip().startswith('#'):
                raise ValueError(f'{path}: unsupported nested YAML after {listkey}; use flat metadata')
    return meta, text[match.end():]


def is_accepted(path):
    if not path.is_file():
        return False
    try:
        meta, _ = read_meta(path)
        return str(meta.get('status', '')).lower() == 'accepted' and bool(meta.get('approval_ref'))
    except ValueError:
        return False


def check(feature):
    errors, warnings, tasks = [], [], {}
    if not feature.is_dir():
        return [f'feature directory not found: {feature}'], warnings, tasks
    task_dir = feature / 'tasks'
    if not task_dir.is_dir():
        return [f'missing tasks directory: {task_dir}'], warnings, tasks
    expected_feature = re.match(r'^(FEAT-[A-Z0-9]+-[0-9]+)', feature.name)
    expected_feature = expected_feature.group(1) if expected_feature else None
    req_path = feature / 'requirements.md'
    solution_path = feature / 'design' / 'solution.md'
    for path in sorted(task_dir.glob('TASK-*.md')):
        if path.name.endswith('-prompt.md'):
            continue
        try:
            meta, text = read_meta(path)
        except ValueError as ex:
            errors.append(str(ex))
            continue
        task_id = meta.get('id')
        if not isinstance(task_id, str) or not re.fullmatch(r'TASK-[A-Z0-9-]+', task_id):
            errors.append(f'{path}: missing or malformed TASK-... id')
            continue
        if task_id in tasks:
            errors.append(f'duplicate task id {task_id} in {path} and {tasks[task_id][0]}')
        tasks[task_id] = (path, meta)
        if not path.stem.startswith(task_id):
            errors.append(f'{path}: basename must start with {task_id}')
        if expected_feature and meta.get('feature_id') != expected_feature:
            errors.append(f'{path}: feature_id must be {expected_feature}')
        status = str(meta.get('status', '')).lower()
        if status not in VALID_STATUSES:
            errors.append(f'{path}: invalid task status {status!r}')
        deps = meta.get('depends_on', [])
        if not isinstance(deps, list):
            errors.append(f'{path}: depends_on must be a YAML list')
        elif len(deps) != len(set(deps)):
            errors.append(f'{path}: repeated dependency ID')
        refs = meta.get('spec_refs', [])
        if refs is None:
            refs = []
        if not isinstance(refs, list):
            errors.append(f'{path}: spec_refs must be a YAML list')
        else:
            for ref in refs:
                if not isinstance(ref, str) or not (path.parent / ref).is_file():
                    errors.append(f'{path}: missing/invalid spec_ref {ref!r}')
            if not refs:
                (errors if status in ('ready', 'in_progress', 'completed') else warnings).append(
                    f'{path}: add spec_refs for traceability to requirements and design')
        if status == 'ready':
            if not meta.get('approval_ref'):
                errors.append(f'{path}: ready task needs an authorization/approval_ref')
            if str(meta.get('delivery_mode', 'production')).lower() == 'production':
                if not is_accepted(req_path):
                    errors.append(f'{path}: production ready requires accepted requirements with approval_ref')
                if not is_accepted(solution_path):
                    errors.append(f'{path}: production ready requires accepted solution design with approval_ref')
            else:
                if not meta.get('prototype_scope_ref'):
                    errors.append(f'{path}: prototype ready needs prototype_scope_ref')
        if status == 'completed':
            ev = meta.get('evidence_ref')
            ev_path = path.parent / ev if isinstance(ev, str) and ev else None
            if not ev_path or not ev_path.is_file():
                errors.append(f'{path}: completed task needs existing evidence_ref document')
            else:
                try:
                    evmeta, _ = read_meta(ev_path)
                    if str(evmeta.get('status','')).lower() != 'verified':
                        errors.append(f'{path}: evidence status must be verified for completed task')
                except ValueError as ex:
                    errors.append(str(ex))
    if not tasks:
        errors.append(f'no TASK-*.md files found under {task_dir}')
        return errors, warnings, tasks
    for name, (path, meta) in tasks.items():
        deps = meta.get('depends_on', [])
        if not isinstance(deps, list):
            continue
        for dep in deps:
            if dep not in tasks:
                errors.append(f'{path}: unknown dependency {dep!r}')
            elif dep == name:
                errors.append(f'{path}: self-dependency')
            elif meta.get('status') == 'ready' and tasks[dep][1].get('status') != 'completed':
                errors.append(f'{path}: ready task dependency {dep} is not completed')
    visited, active = set(), set()
    def visit(node):
        if node in active:
            errors.append(f'dependency cycle includes {node}')
            return
        if node in visited:
            return
        active.add(node)
        for dep in tasks[node][1].get('depends_on', []):
            if dep in tasks and dep != node:
                visit(dep)
        active.remove(node)
        visited.add(node)
    for taskid in tasks:
        visit(taskid)
    if req_path.is_file():
        declared = set(FR_ROW.findall(req_path.read_text(encoding='utf-8')))
        for name, (path, meta) in tasks.items():
            refs = meta.get('acceptance_refs', [])
            if isinstance(refs, list):
                for ref in refs:
                    if ref not in declared:
                        errors.append(f'{path}: acceptance_ref {ref} not in requirements FR declarations')
    return errors, warnings, tasks


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('feature_directory', type=Path)
    args = parser.parse_args()
    errors, warnings, tasks = check(args.feature_directory)
    for issue in errors: print('ERROR:', issue)
    for issue in warnings: print('WARN: ', issue)
    for name, (_, m) in sorted(tasks.items()):
        print(f"TASK: {name}: {m.get('status', 'unknown')} -> depends_on={m.get('depends_on', [])}")
    print(f'Graph check: {len(tasks)} task(s), {len(errors)} error(s), {len(warnings)} warning(s).')
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())

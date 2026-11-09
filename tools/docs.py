#!/usr/bin/env python3
"""Dependency-free DeliveryPilot artifact scaffold, traceability and validation CLI.

Run at repository root: python tools/docs.py validate
"""
from __future__ import annotations
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import unquote

KINDS = ('UR', 'FR', 'US', 'TASK')
DIRS = {'UR': 'ur', 'FR': 'fr', 'US': 'us', 'TASK': 'tasks'}
TEMPLATES = {'UR': 'ur.md', 'FR': 'fr.md', 'US': 'us.md', 'TASK': 'task.md'}
ID_RE = re.compile(r'^(UR|FR|US|TASK)-DP-(\d{3})-(\d{3})$')
FEAT_RE = re.compile(r'^FEAT-DP-(\d{3})$')
FOLDER_RE = re.compile(r'^(FEAT-DP-\d{3})-[a-z0-9]+(?:-[a-z0-9]+)*$')
MD_LINK_RE = re.compile(r'(?<!!)\[[^\]]*\]\(([^)]+)\)')
REF_KEYS = {'UR': ('source_refs',), 'FR': ('ur_refs', 'nfr_refs'),
            'US': ('fr_refs', 'depends_on'),
            'TASK': ('us_refs', 'fr_refs', 'nfr_refs', 'depends_on')}
REQUIRED = {
    'FEAT': ('id', 'title', 'status', 'priority', 'release', 'depends_on'),
    'UR': ('id', 'feature_id', 'title', 'status', 'priority', 'actor', 'source_refs', 'approval_ref'),
    'FR': ('id', 'feature_id', 'title', 'status', 'priority', 'ur_refs', 'nfr_refs', 'approval_ref'),
    'US': ('id', 'feature_id', 'title', 'status', 'priority', 'fr_refs', 'depends_on', 'approval_ref'),
    'TASK': ('id', 'feature_id', 'title', 'status', 'priority', 'type', 'us_refs', 'fr_refs',
             'nfr_refs', 'depends_on', 'codebase_verified'),
}
STATUSES = {
    'FEAT': {'discovery','draft','ready','in_progress','delivered','retired'},
    'UR': {'draft','in_review','ready','approved','superseded','retired'},
    'FR': {'draft','in_review','ready','approved','superseded','retired'},
    'US': {'draft','in_review','ready','approved','superseded','retired'},
    'TASK': {'pending','blocked','in_progress','in_review','done','cancelled'},
}
MANDATORY_SECTIONS = {
    'FEAT': ('Problem', 'Actors', 'Dependencies'),
    'UR': ('Problem', 'User need', 'Business value', 'Open questions'),
    'FR': ('Requirement statement', 'Main behavior', 'Verification criteria', 'Open questions'),
    'US': ('Story', 'Acceptance criteria', 'Open questions'),
    'TASK': ('Objective', 'Codebase evidence', 'Test matrix', 'Acceptance', 'Unresolved decisions'),
}
STARTED_AT_RE = re.compile(r'^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$')
COMMIT_RE = re.compile(r'^[0-9a-f]{7,40}$')
LIFECYCLE = {'start-task': ('pending', 'in_progress'), 'submit-task': ('in_progress', 'in_review'),
             'return-task': ('in_review', 'in_progress'), 'complete-task': ('in_review', 'done')}
VERIFY_HEADING = '### Verify-Module: '


def decode_value(raw: str):
    raw = raw.strip()
    if raw == 'null' or raw == '~': return None
    if raw == 'true': return True
    if raw == 'false': return False
    if raw.startswith('[') or raw.startswith('"'):
        try: return json.loads(raw)
        except json.JSONDecodeError: raise ValueError(f'unsupported inline YAML/JSON: {raw}')
    if raw.startswith("'") and raw.endswith("'"): return raw[1:-1].replace("''", "'")
    return raw


def parse_md(path: Path):
    text = path.read_text(encoding='utf-8')
    if not text.startswith('---\n'):
        raise ValueError('missing YAML frontmatter')
    closing = text.find('\n---\n', 4)
    if closing == -1: raise ValueError('unclosed YAML frontmatter')
    meta = {}
    for line in text[4:closing].splitlines():
        if not line.strip() or line.startswith('#'): continue
        m = re.fullmatch(r'([a-z][a-z0-9_]*)\s*:\s*(.*)', line)
        if not m: raise ValueError(f'unsupported YAML field: {line!r}; use flat keys and JSON-style arrays')
        if m.group(1) in meta: raise ValueError(f'duplicate field {m.group(1)}')
        meta[m.group(1)] = decode_value(m.group(2))
    return meta, text[closing+5:]


def feature_dirs(root: Path):
    features = root / 'docs' / 'features'
    return sorted(x for x in features.iterdir() if x.is_dir()) if features.exists() else []


def find_feature(root: Path, feature_id: str) -> Path:
    if not FEAT_RE.fullmatch(feature_id):
        raise ValueError('feature ID must be FEAT-DP-XXX')
    matches = [p for p in feature_dirs(root) if p.name.startswith(feature_id+'-')]
    if len(matches) != 1:
        raise ValueError(f'expected exactly one feature directory for {feature_id}, found {len(matches)}')
    return matches[0]


def inventory(root: Path):
    result, errors = {}, []
    for folder in feature_dirs(root):
        m = FOLDER_RE.fullmatch(folder.name)
        if not m:
            errors.append(f'{folder}: invalid feature directory name')
            continue
        candidates = [(folder/'feature.md','FEAT')]
        for kind, dirname in DIRS.items():
            if (folder/dirname).exists():
                candidates += [(p,kind) for p in sorted((folder/dirname).glob('*.md'))]
        for p, kind in candidates:
            if not p.exists():
                errors.append(f'{p}: missing feature.md')
                continue
            try:
                meta, body = parse_md(p)
            except (OSError, ValueError) as exc:
                errors.append(f'{p}: {exc}')
                continue
            ident = meta.get('id')
            if not isinstance(ident,str):
                errors.append(f'{p}: invalid id')
                continue
            if ident in result:
                errors.append(f'{p}: duplicate ID {ident} (also {result[ident][0]})')
            else: result[ident]=(p,kind,meta,body)
            if kind == 'FEAT':
                if ident != m.group(1): errors.append(f'{p}: feature ID differs from folder')
            else:
                mm = ID_RE.fullmatch(ident)
                if not mm or mm.group(1) != kind or ident != p.stem:
                    errors.append(f'{p}: invalid ID, type or filename: {ident}')
                elif 'FEAT-DP-'+mm.group(2) != m.group(1):
                    errors.append(f'{p}: ID belongs to another feature')
                if meta.get('feature_id') != m.group(1):
                    errors.append(f'{p}: incorrect feature_id')
            for key in REQUIRED[kind]:
                if key not in meta: errors.append(f'{p}: missing {key}')
            if meta.get('status') not in STATUSES[kind]:
                errors.append(f'{p}: invalid status {meta.get("status")!r}')
            if not isinstance(meta.get('title'),str) or not meta['title'].strip():
                errors.append(f'{p}: missing title')
            for key in (REF_KEYS.get(kind,()) + ('depends_on',) if kind == 'FEAT' else REF_KEYS.get(kind,())):
                if not isinstance(meta.get(key),list) or any(not isinstance(x,str) for x in meta.get(key,[]) if isinstance(meta.get(key),list)):
                    errors.append(f'{p}: {key} must be JSON-style list of strings')
            if kind in ('UR','FR','US') and meta.get('status') == 'approved':
                if not isinstance(meta.get('approval_ref'),str) or not meta['approval_ref'].strip():
                    errors.append(f'{p}: approved requires verifiable approval_ref')
            if kind == 'TASK':
                implementation_skill = meta.get('implementation_skill')
                if implementation_skill is not None:
                    if not isinstance(implementation_skill, str) or not re.fullmatch(r'[a-z][a-z0-9]*(?:-[a-z0-9]+)*', implementation_skill):
                        errors.append(f'{p}: invalid implementation_skill; expected null or lowercase skill name')
                    elif not (root / '.agents' / 'skills' / implementation_skill / 'SKILL.md').is_file():
                        errors.append(f'{p}: unknown implementation_skill {implementation_skill}; local SKILL.md missing')
                if type(meta.get('codebase_verified')) is not bool:
                    errors.append(f'{p}: codebase_verified must be boolean')
                if meta.get('status') in {'pending','in_progress','in_review','done'} and not meta.get('codebase_verified'):
                    errors.append(f'{p}: active/done task requires codebase_verified=true')
                if not any(meta.get(k) for k in ('us_refs','fr_refs','nfr_refs')):
                    errors.append(f'{p}: task has no US/FR/NFR source references')
                for key,pattern in (('started_at',STARTED_AT_RE),('base_commit',COMMIT_RE)):
                    value=meta.get(key)
                    if value is not None and (not isinstance(value,str) or not pattern.fullmatch(value)):
                        errors.append(f'{p}: invalid {key} {value!r}')
                if meta.get('status') in {'in_progress','in_review'} and not meta.get('started_at'):
                    errors.append(f'{p}: {meta.get("status")} task requires started_at; use start-task')
            headings=[h.lower() for h in re.findall(r'^## (.+)$',body,re.M)]
            # A repeated section usually means a damaged edit (duplicated text); fenced code is ignored.
            unfenced=re.sub(r'^```.*?^```','',body,flags=re.M|re.S)
            seen_headings=set()
            for heading in re.findall(r'^## (.+)$',unfenced,re.M):
                key=heading.strip().lower()
                if key in seen_headings: errors.append(f'{p}: duplicate section heading {heading.strip()!r}')
                seen_headings.add(key)
            for required in MANDATORY_SECTIONS[kind]:
                if not any(required.lower() in h for h in headings):
                    errors.append(f'{p}: missing section matching {required!r}')
            if meta.get('status') in {'ready','approved','in_progress','in_review','done'} and re.search(r'<[^<>\n]+>', body):
                errors.append(f'{p}: unresolved template placeholders in non-draft artifact')
            if kind == 'US' and not re.search(r'\bGiven\b.+\bWhen\b.+\bThen\b',body,re.I|re.S):
                errors.append(f'{p}: acceptance criteria must include Given/When/Then')
    return result,errors


def check_refs(root: Path, items):
    errors=[]
    nfrs=set(re.findall(r'\bNFR-\d{3}\b',(root/'docs/requirements.md').read_text(encoding='utf-8')))
    allowed={'ur_refs':'UR','fr_refs':'FR','us_refs':'US','nfr_refs':'NFR'}
    for ident,(p,kind,m,body) in items.items():
        for field,prefix in allowed.items():
            if field not in m: continue
            for target in m.get(field,[]) if isinstance(m.get(field),list) else []:
                if prefix=='NFR':
                    if target not in nfrs: errors.append(f'{p}: missing global {target} in nfr_refs')
                elif target not in items or items[target][1] != prefix:
                    errors.append(f'{p}: invalid or absent {field} reference {target}')
        deps=m.get('depends_on',[])
        if isinstance(deps,list):
            for d in deps:
                if d not in items or items[d][1]!=kind:
                    if not (kind=='FEAT' and d in re.findall(r'FEAT-DP-\d{3}',(root/'docs/roadmap.md').read_text(encoding='utf-8'))):
                        errors.append(f'{p}: dependency {d} must reference an existing {kind}')
        if kind in ('FR','US') and not (m.get('ur_refs') if kind=='FR' else m.get('fr_refs')):
            # Valid for a formally justified direct business-rule or NFR requirement, but still requires reviewer attention.
            pass
    for kind in ('FEAT','US','TASK'):
        graph={i:m.get('depends_on',[]) for i,(p,k,m,b) in items.items() if k==kind and isinstance(m.get('depends_on'),list)}
        seen=set(); active=set()
        def dfs(i):
            if i in active:
                errors.append(f'{kind} dependency cycle at {i}')
                return
            if i in seen: return
            seen.add(i); active.add(i)
            for dep in graph.get(i,[]):
                if dep in graph: dfs(dep)
            active.remove(i)
        for i in graph: dfs(i)
    return errors


def check_links(root: Path):
    errors=[]
    for p in sorted((root/'docs').rglob('*.md')):
        text=p.read_text(encoding='utf-8')
        for link in MD_LINK_RE.findall(text):
            link=link.strip().split(' ')[0].strip('<>')
            if re.match(r'^(https?://|mailto:|#|data:)',link): continue
            rel=unquote(link.split('#')[0])
            if not rel: continue
            dest=(p.parent/rel).resolve()
            if not dest.exists(): errors.append(f'{p}: broken markdown link {link}')
            elif not dest.is_relative_to(root.resolve()): errors.append(f'{p}: link escapes repository {link}')
            elif '#' in link and dest.is_file() and dest.suffix.lower()=='.md':
                frag=link.split('#',1)[1]
                headings=re.findall(r'^#{1,6}\s+(.+)$',dest.read_text(encoding='utf-8'),re.M)
                def slug(h): return re.sub(r'[^a-z0-9\- ]','',h.lower()).strip().replace(' ','-')
                if frag and frag not in {slug(h) for h in headings}:
                    errors.append(f'{p}: missing markdown heading anchor {link}')
    return errors


def selected(items,feature):
    return {i:v for i,v in items.items() if (i == feature or v[2].get('feature_id') == feature)}


def coverage(items, feature):
    group=selected(items,feature)
    bytype=defaultdict(list)
    for ident,(p,k,m,b) in group.items(): bytype[k].append(ident)
    consumers=defaultdict(set)
    for ident,(p,k,m,b) in group.items():
        for key in ('ur_refs','fr_refs','us_refs'):
            for ref in m.get(key,[]) if isinstance(m.get(key),list) else []:
                consumers[ref].add(ident)
    print(f'Coverage: {feature}')
    for kind in ('UR','FR','US','TASK'):
        ids=sorted(bytype[kind]); print(f'  {kind}: {len(ids)}')
        for ident in ids:
            targets=sorted(consumers[ident]); print(f'    {ident}: {", ".join(targets) if targets else "(no downstream link yet)"}')
    gaps=[i for kind in ('UR','FR','US') for i in bytype[kind] if not consumers[i]]
    print(f'  Unlinked downstream (advisory): {len(gaps)}')
    return gaps


def check_task_readiness(items, task_id, resume=False):
    """Structural executor preflight; NOT proof of approval, fresh code, or acceptance."""
    errors=[]
    entry=items.get(task_id)
    if not entry or entry[1] != 'TASK':
        return [f'unknown task {task_id}']
    path, _, meta, body = entry
    allowed = {'pending', 'in_progress'} if resume else {'pending'}
    if meta.get('status') not in allowed:
        errors.append(f'{path}: task status must be pending before starting (or in_progress with --resume); found {meta.get("status")!r}')
    if meta.get('codebase_verified') is not True:
        errors.append(f'{path}: codebase_verified=true is required for task preflight')
    for dependency in meta.get('depends_on', []):
        other=items.get(dependency)
        if not other or other[1] != 'TASK' or other[2].get('status') != 'done':
            errors.append(f'{path}: dependency {dependency} must be done before implementation')
    # Walk only actual requirement links, rather than scanning entire unrelated feature docs.
    to_visit=list(meta.get('us_refs', [])) + list(meta.get('fr_refs', []))
    visited=set()
    while to_visit:
        identifier=to_visit.pop()
        if identifier in visited:
            continue
        visited.add(identifier)
        source=items.get(identifier)
        if not source or source[1] not in {'UR','FR','US'}:
            errors.append(f'{path}: source requirement {identifier} missing or invalid')
            continue
        source_path, kind, source_meta, _ = source
        if source_meta.get('status') not in {'ready','approved'}:
            errors.append(f'{path}: source requirement {identifier} is not ready/approved ({source_meta.get("status")})')
        if kind == 'US':
            to_visit.extend(source_meta.get('fr_refs',[]))
        elif kind == 'FR':
            to_visit.extend(source_meta.get('ur_refs',[]))
    if not visited and not meta.get('nfr_refs'):
        errors.append(f'{path}: no resolved source requirements')
    return errors


# --- Task lifecycle: start -> verify -> submit -> review -> complete -------------------------------

def split_frontmatter(text: str):
    """Return (frontmatter lines, closing index) using the same bounds as parse_md."""
    if not text.startswith('---\n'): raise ValueError('missing YAML frontmatter')
    closing=text.find('\n---\n',4)
    if closing == -1: raise ValueError('unclosed YAML frontmatter')
    return text[4:closing].split('\n'), closing


def write_atomic(path: Path, text: str, newline: str = '\n'):
    fd,tmp=tempfile.mkstemp(dir=path.parent,prefix=f'.{path.name}.',suffix='.tmp')
    try:
        with os.fdopen(fd,'w',encoding='utf-8',newline=newline) as f: f.write(text)
        os.replace(tmp,path)
    except BaseException:
        if os.path.exists(tmp): os.unlink(tmp)
        raise


def rewrite_task(path: Path, updates: dict, body: str | None = None):
    """Change only the named frontmatter lines (and optionally the body); keep all other bytes."""
    newline='\r\n' if b'\r\n' in path.read_bytes() else '\n'  # keep the file's own line endings
    text=path.read_text(encoding='utf-8')
    lines,closing=split_frontmatter(text)
    anchor=next((i for i,l in enumerate(lines) if re.match(r'codebase_verified\s*:',l)),len(lines)-1)
    for key,value in updates.items():
        index=next((i for i,l in enumerate(lines) if re.match(rf'{key}\s*:',l)),None)
        if index is None:
            anchor+=1
            lines.insert(anchor,f'{key}: {json.dumps(value)}')
            continue
        raw=lines[index].split(':',1)[1].strip()
        bare=key == 'status' and isinstance(value,str) and not raw.startswith(('"',"'"))
        lines[index]=f'{key}: {value if bare else json.dumps(value)}'
    rest=text[closing:] if body is None else '\n---\n'+body
    write_atomic(path,'---\n'+'\n'.join(lines)+rest,newline)


def section_span(body: str, prefix: str):
    """(heading start, heading end, section end) of the first '## <prefix>...' heading, or None."""
    m=re.search(rf'^## {re.escape(prefix)}.*$',body,re.M)
    if not m: return None
    nxt=re.search(r'^## ',body[m.end():],re.M)
    return m.start(),m.end(),(m.end()+nxt.start() if nxt else len(body))


def section_text(body: str, prefix: str):
    span=section_span(body,prefix)
    return None if span is None else body[span[1]:span[2]].strip()


def git_output(root: Path, *args):
    try:
        p=subprocess.run(['git','-C',str(root),*args],capture_output=True,text=True)
    except OSError:
        return None
    return p.stdout.strip() if p.returncode == 0 else None


def utc_now():
    return datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')


def clean(value) -> str:
    """Copy a summary value into Markdown without placeholder-like brackets, pipes or newlines."""
    return re.sub(r'\s+',' ',str(value)).replace('<','(').replace('>',')').replace('|','/').strip()


def render_verify_section(summary: dict) -> str:
    module=clean(summary.get('module','unknown'))
    tests=summary.get('tests') or {}
    commit=summary.get('commit')
    lines=[f'{VERIFY_HEADING}{module}', '',
           f'- Run: {clean(summary.get("finishedAt","unknown"))} at commit {clean(commit[:7]) if isinstance(commit,str) and commit else "unknown"}; result **{clean(summary.get("result","unknown"))}**',
           f'- Command: `{clean(summary.get("invocation") or "scripts/Verify-Module.ps1 -Module " + module)}`',
           '', '| Step | Status | Exit | Seconds | Command |', '|---|---|---|---|---|']
    for step in summary.get('steps') or []:
        command=clean(step.get('command') or '')
        lines.append(f'| {clean(step.get("name",""))} | {clean(step.get("status",""))} | {clean(step.get("exitCode",""))} | '
                     f'{clean(step.get("durationSeconds",""))} | {"`"+command+"`" if command else ""} |')
    lines += ['', f'- Tests: {int(tests.get("passed",0))} passed, {int(tests.get("failed",0))} failed, '
                  f'{int(tests.get("skipped",0))} skipped ({int(tests.get("dbSkipped",0))} database)']
    reasons=tests.get('skipReasons') or {}
    if reasons:
        lines.append('- Skip reasons:')
        for reason,count in sorted(reasons.items(),key=lambda kv:-int(kv[1]))[:10]:
            lines.append(f'  - {int(count)} × {clean(reason)[:300]}')
    if summary.get('trxPath'): lines.append(f'- TRX: `{clean(summary["trxPath"])}`')
    return '\n'.join(lines)+'\n'


def ensure_evidence_section(body: str) -> str:
    if section_span(body,'Execution evidence'): return body
    decisions=section_span(body,'Unresolved decisions')
    block='## Execution evidence\n\n'
    if decisions: return body[:decisions[0]]+block+body[decisions[0]:]
    return body.rstrip('\n')+'\n\n'+block


def verify_subsections(body: str):
    """[(module, start, end)] for '### Verify-Module:' blocks inside the evidence section."""
    span=section_span(body,'Execution evidence')
    if not span: return []
    starts=[m for m in re.finditer(rf'^{re.escape(VERIFY_HEADING)}(.+)$',body[span[1]:span[2]],re.M)]
    result=[]
    for m in starts:
        start=span[1]+m.start()
        nxt=re.search(r'^#{2,3} ',body[span[1]+m.end():span[2]],re.M)
        end=span[1]+m.end()+nxt.start() if nxt else span[2]
        result.append((m.group(1).strip(),start,end))
    return result


def append_to_section(body: str, start: int, end: int, line: str) -> str:
    """Insert a line after the last non-blank line of body[start:end]."""
    chunk=body[start:end]
    stripped=chunk.rstrip('\n')
    trailing=chunk[len(stripped):] or '\n'
    return body[:start]+(stripped or '\n')+'\n'+line+trailing+body[end:]


def upsert_verify_section(body: str, text: str, module: str) -> str:
    body=ensure_evidence_section(body)
    for name,start,end in verify_subsections(body):
        if name.lower() == module.lower():
            return body[:start]+text+'\n'+body[end:].lstrip('\n') if end < len(body) else body[:start]+text
    span=section_span(body,'Execution evidence')
    section=body[span[1]:span[2]]
    stripped=section.rstrip('\n')
    tail='\n\n' if span[2] < len(body) else '\n'
    return body[:span[1]]+stripped+'\n\n'+text.rstrip('\n')+tail+body[span[2]:]


def evidence_errors(meta: dict, body: str):
    """Freshness rule shared by submit-task and complete-task; empty list means acceptable."""
    evidence=section_text(body,'Execution evidence')
    if evidence is None or not evidence:
        return ['no ## Execution evidence; run verify-task or submit-task --no-verify "reason"']
    if re.search(r'^- No verification: \S',evidence,re.M):
        return []
    blocks=verify_subsections(body)
    if not blocks:
        return ['no Verify-Module evidence; run verify-task or submit-task --no-verify "reason"']
    errors=[]
    started=meta.get('started_at')
    for module,start,end in blocks:
        block=body[start:end]
        run=re.search(r'^- Run: (\S+) at commit \S+; result \*\*([a-z-]+)\*\*',block,re.M)
        if not run:
            errors.append(f'{module}: unreadable Verify-Module run line'); continue
        if isinstance(started,str) and run.group(1) < started:
            errors.append(f'{module}: evidence from {run.group(1)} predates started_at {started}; run verify-task again')
        if run.group(2) not in {'passed','passed-with-skips'}:
            errors.append(f'{module}: verification result is {run.group(2)}')
        skipped=re.search(r'\((\d+) database\)',block)
        if skipped and int(skipped.group(1)) > 0 and not re.search(r'^- Accepted skips: \S',block,re.M):
            errors.append(f'{module}: {skipped.group(1)} database tests skipped; rerun with the database or submit-task --accept-skips "reason"')
    return errors


def transition_task(root: Path, items, task_id: str, command: str, accept_skips=None, no_verify=None):
    entry=items.get(task_id)
    if not entry or entry[1] != 'TASK': return [f'unknown task {task_id}']
    path,_,meta,body=entry
    source,target=LIFECYCLE[command]
    status=meta.get('status')
    errors=[]
    if command == 'start-task':
        if status == 'in_progress':
            return [f'{path}: already in progress; use check-task --resume to continue']
        errors+=check_task_readiness(items,task_id)
        decisions=section_text(body,'Unresolved decisions')
        first=next((l.strip() for l in (decisions or '').splitlines() if l.strip()),'')
        if decisions is None:
            errors.append(f'{path}: missing ## Unresolved decisions section')
        elif not (first.startswith('None') or first.startswith('No ')):
            errors.append(f'{path}: unresolved decisions must start with "None" or "No "; found {first[:200]!r}')
    elif status != source:
        errors.append(f'{path}: {command} needs status {source}; found {status!r}')
    for flag,value in (('--accept-skips',accept_skips),('--no-verify',no_verify)):
        if value is not None and not value.strip(): errors.append(f'{flag} requires a non-empty reason')
    if errors: return errors
    new_body=body
    if command == 'submit-task':
        if no_verify:
            new_body=ensure_evidence_section(new_body)
            span=section_span(new_body,'Execution evidence')
            new_body=append_to_section(new_body,span[1],span[2],f'- No verification: {clean(no_verify)}')
        if accept_skips:
            for module,start,end in reversed(verify_subsections(new_body)):
                block=new_body[start:end]
                skipped=re.search(r'\((\d+) database\)',block)
                if skipped and int(skipped.group(1)) > 0 and '- Accepted skips:' not in block:
                    new_body=append_to_section(new_body,start,end,f'- Accepted skips: {clean(accept_skips)}')
    if command in {'submit-task','complete-task'}:
        errors+=[f'{path}: {e}' for e in evidence_errors(meta,new_body)]
    if command == 'complete-task':
        acceptance=section_text(body,'Acceptance') or ''
        if not re.search(r'^\s*- \[[xX]\]',acceptance,re.M):
            errors.append(f'{path}: acceptance section has no checked checkbox')
        if re.search(r'^\s*- \[ \]',acceptance,re.M):
            errors.append(f'{path}: acceptance section still has unchecked items')
    if errors: return errors
    updates={'status':target}
    if command == 'start-task':
        updates['started_at']=utc_now()
        commit=git_output(root,'rev-parse','HEAD')
        updates['base_commit']=commit if commit and COMMIT_RE.fullmatch(commit) else None
        if updates['base_commit'] is None:
            print('WARNING: base_commit not recorded: not a git work tree',file=sys.stderr)
        else:
            dirty=git_output(root,'status','--porcelain')
            if dirty: print(f'NOTE: {len(dirty.splitlines())} uncommitted path(s) at start',file=sys.stderr)
    rewrite_task(path,updates,None if new_body == body else new_body)
    print(f'{task_id}: {status} -> {target}')
    return []


def run_verify_script(root: Path, module: str, require_db: bool, test_filter: str | None):
    script=root/'scripts'/'Verify-Module.ps1'
    if not script.is_file(): raise ValueError(f'{script} not found')
    pwsh=shutil.which('pwsh')
    if pwsh: shell=[pwsh,'-NoProfile','-NonInteractive','-File']
    elif shutil.which('powershell'):
        # Process-scoped policy for this child only; machine and user policy are never changed.
        shell=[shutil.which('powershell'),'-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File']
    else: raise ValueError('no pwsh or powershell on PATH; run scripts/Verify-Module.ps1 yourself and pass --from-summary')
    with tempfile.TemporaryDirectory() as tmp:
        out=Path(tmp)/'summary.json'
        command=shell+[str(script),'-Module',module,'-OutFile',str(out)]
        if require_db: command.append('-RequireDb')
        if test_filter: command+=['-Filter',test_filter]
        proc=subprocess.run(command,cwd=root)
        if not out.is_file(): raise ValueError(f'verification script wrote no summary (exit {proc.returncode})')
        return json.loads(out.read_text(encoding='utf-8-sig'))


def verify_task(root: Path, items, task_id: str, module: str, require_db=False, test_filter=None, from_summary=None):
    entry=items.get(task_id)
    if not entry or entry[1] != 'TASK': return [f'unknown task {task_id}'], None
    path,_,meta,body=entry
    if meta.get('status') != 'in_progress':
        return [f'{path}: verify-task needs status in_progress; found {meta.get("status")!r}'], None
    if from_summary:
        summary=json.loads(Path(from_summary).read_text(encoding='utf-8-sig'))
    else:
        summary=run_verify_script(root,module,require_db,test_filter)
    if not isinstance(summary,dict) or str(summary.get('module','')).lower() != module.lower():
        return [f'summary module {summary.get("module") if isinstance(summary,dict) else None!r} does not match --module {module}'], None
    rewrite_task(path,{},upsert_verify_section(body,render_verify_section(summary),module))
    result=summary.get('result')
    print(f'{task_id}: recorded {module} verification ({result})')
    return [], result


def make_feature(root,ident,title,slug,release,priority):
    if not FEAT_RE.fullmatch(ident): raise ValueError('invalid feature ID')
    if not re.fullmatch('[a-z0-9]+(?:-[a-z0-9]+)*',slug): raise ValueError('invalid slug')
    roadmap=(root/'docs/roadmap.md').read_text(encoding='utf-8')
    if ident not in roadmap: raise ValueError(f'{ident} is not registered in docs/roadmap.md')
    if any(p.name.startswith(ident+'-') for p in feature_dirs(root)):
        raise ValueError('feature already exists; inspect/refine it instead of overwriting')
    folder=root/'docs/features'/f'{ident}-{slug}'
    folder.mkdir(parents=True,exist_ok=False)
    s=(root/'docs/templates/feature.md').read_text(encoding='utf-8')
    s=s.replace('FEAT-DP-XXX',ident).replace('"<Feature name>"',json.dumps(title)).replace('<Feature name>',title)
    s=s.replace('"<MVP|V1|V2>"',json.dumps(release)).replace('priority: P1',f'priority: {priority}')
    with (folder/'feature.md').open('x',encoding='utf-8') as f: f.write(s)
    print(f'Created {folder.relative_to(root)}/feature.md (discovery draft).')


def next_id(root,kind,feature):
    folder=find_feature(root,feature)/DIRS[kind]
    existing=[]
    if folder.exists():
        for p in folder.glob(f'{kind}-DP-{feature[-3:]}-*.md'):
            m=ID_RE.fullmatch(p.stem)
            if m: existing.append(int(m.group(3)))
    nxt=max(existing,default=0)+1
    if nxt>999: raise ValueError('artifact ID space exhausted')
    return f'{kind}-DP-{feature[-3:]}-{nxt:03d}'


def create_artifact(root,kind,feature,title,actor):
    folder=find_feature(root,feature)/DIRS[kind]
    folder.mkdir(exist_ok=True)
    tpl=(root/'docs/templates'/TEMPLATES[kind]).read_text(encoding='utf-8')
    for _ in range(999):
        ident=next_id(root,kind,feature)
        out=folder/f'{ident}.md'
        content=tpl.replace(f'{kind}-DP-XXX-NNN',ident).replace('FEAT-DP-XXX',feature)
        title_key={'UR':'<Concise user need>','FR':'<Observable system behavior>','US':'<User-valued increment>','TASK':'<Coding-ready deliverable>'}[kind]
        content=content.replace('"'+title_key+'"',json.dumps(title,ensure_ascii=False)).replace(title_key,title)
        if actor: content=content.replace('"<Specific stakeholder or persona>"',json.dumps(actor,ensure_ascii=False))
        try:
            with out.open('x',encoding='utf-8') as f: f.write(content)
            print(f'Created draft {out.relative_to(root)}'); return
        except FileExistsError: continue
    raise ValueError('no free ID found; concurrent creations may be contending')


def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',type=Path,default=Path(__file__).resolve().parent.parent)
    commands=p.add_subparsers(dest='command',required=True)
    val=commands.add_parser('validate',help='validate metadata, references, dependencies, links')
    val.add_argument('--feature')
    cov=commands.add_parser('coverage',help='show downstream traceability')
    cov.add_argument('--feature',required=True)
    preflight=commands.add_parser('check-task',help='structural preflight before implementing a Task; not business approval')
    preflight.add_argument('--id',required=True)
    preflight.add_argument('--resume',action='store_true',help='permit authorized continuation of an in_progress task')
    lifecycle={}
    for name,text in (('start-task','pending -> in_progress after check-task and an open-decision check; records started_at/base_commit'),
                      ('submit-task','in_progress -> in_review; requires fresh, non-failing verification evidence'),
                      ('return-task','in_review -> in_progress when review sends work back'),
                      ('complete-task','in_review -> done; requires checked acceptance and fresh evidence')):
        lifecycle[name]=commands.add_parser(name,help=text)
        lifecycle[name].add_argument('--id',required=True)
    lifecycle['submit-task'].add_argument('--accept-skips',metavar='REASON',help='record why skipped database tests are acceptable')
    lifecycle['submit-task'].add_argument('--no-verify',metavar='REASON',help='record why no module verification applies (e.g. docs-only task)')
    verify=commands.add_parser('verify-task',help='run scripts/Verify-Module.ps1 for an in_progress task and record the evidence')
    verify.add_argument('--id',required=True);verify.add_argument('--module',required=True)
    verify.add_argument('--require-db',action='store_true');verify.add_argument('--filter')
    verify.add_argument('--from-summary',metavar='JSON',help='record a summary saved by an earlier Verify-Module.ps1 -OutFile run')
    nxt=commands.add_parser('next-id',help='show next available candidate ID')
    nxt.add_argument('--type',required=True,choices=KINDS)
    nxt.add_argument('--feature',required=True)
    scaff=commands.add_parser('scaffold-feature',help='create a feature from registered roadmap entry')
    scaff.add_argument('--id',required=True);scaff.add_argument('--title',required=True);scaff.add_argument('--slug',required=True)
    scaff.add_argument('--release',default='MVP');scaff.add_argument('--priority',default='P1')
    create=commands.add_parser('new-artifact',help='create an exclusive draft from canonical template')
    create.add_argument('--type',required=True,choices=KINDS)
    create.add_argument('--feature',required=True);create.add_argument('--title',required=True);create.add_argument('--actor')
    args=p.parse_args(argv)
    root=args.root.resolve()
    try:
        if args.command=='scaffold-feature': make_feature(root,args.id,args.title,args.slug,args.release,args.priority)
        elif args.command=='next-id': print(next_id(root,args.type,args.feature))
        elif args.command=='new-artifact': create_artifact(root,args.type,args.feature,args.title,args.actor)
        elif args.command in LIFECYCLE or args.command=='verify-task':
            items,errors=inventory(root)
            errors+=check_refs(root,items)+check_links(root)
            target=items.get(args.id)
            prefix=str(target[0]) if target else None
            blocking=[e for e in errors if prefix and e.startswith(prefix)]
            for warning in (e for e in errors if e not in blocking):
                print('WARNING (other artifact):',warning,file=sys.stderr)
            if not blocking:
                if args.command=='verify-task':
                    blocking,result=verify_task(root,items,args.id,args.module,args.require_db,args.filter,args.from_summary)
                    if not blocking and result not in {'passed','passed-with-skips'}: return 1
                else:
                    blocking=transition_task(root,items,args.id,args.command,
                                             getattr(args,'accept_skips',None),getattr(args,'no_verify',None))
            for error in blocking: print('ERROR:',error,file=sys.stderr)
            if blocking: return 1
        else:
            items,errors=inventory(root)
            errors+=check_refs(root,items)+check_links(root)
            if args.command=='coverage':
                if args.feature not in items: errors.append(f'unknown feature {args.feature}')
                else: coverage(items,args.feature)
            if args.command=='check-task':
                errors+=check_task_readiness(items,args.id,args.resume)
                if not errors:
                    print(f'Preflight: {args.id} structurally eligible; confirm current source, business approval policy, unresolved decisions, and acceptance before coding.')
            if args.command=='validate':
                if args.feature and args.feature not in items: errors.append(f'unknown feature {args.feature}')
                elif args.feature: coverage(items,args.feature)
            for error in errors: print('ERROR:',error,file=sys.stderr)
            print(f'Validation: {len(items)} artifacts, {len(errors)} errors')
            if errors: return 1
    except (OSError,ValueError) as exc:
        print(f'ERROR: {exc}',file=sys.stderr);return 2
    return 0

if __name__=='__main__':sys.exit(main())

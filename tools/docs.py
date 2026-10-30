#!/usr/bin/env python3
"""Dependency-free DeliveryPilot artifact scaffold, traceability and validation CLI.

Run at repository root: python tools/docs.py validate
"""
from __future__ import annotations
import argparse
import json
import re
import sys
from collections import defaultdict
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
            headings=[h.lower() for h in re.findall(r'^## (.+)$',body,re.M)]
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

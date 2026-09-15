#!/usr/bin/env python3
"""Parse the pinned MIT source (never execute MySQL SQL). Pipe output into psql -v ON_ERROR_STOP=1."""
import pathlib, re, json, hashlib, sys
root = pathlib.Path(__file__).resolve().parents[1] / 'seed-data'
raw = (root / 'wilayah.sql').read_bytes()
assert hashlib.sha256(raw).hexdigest() == json.loads((root/'manifest.json').read_text())['sha256'], 'Snapshot checksum mismatch'
rows = {}
for code, name in re.findall(r"\('([0-9.]+)'\s*,\s*'((?:[^'\\]|\\.|'')*)'\)", raw.decode()):
    code = code.replace('.', '')
    name = re.sub(r'\\(.)', r'\1', name).replace("''", "'")
    assert code not in rows, code
    rows[code] = name
assert len(rows) > 90000
print('BEGIN; SELECT pg_advisory_xact_lock(728491);')
for size, table, parent in [(2,'Provinces',None),(4,'Regencies','ProvinceId'),(6,'Districts','RegencyId'),(10,'Villages','DistrictId')]:
    subset = [(c,n) for c,n in rows.items() if len(c)==size]
    print(f'CREATE TEMP TABLE seed_{table} (LIKE "{table}" INCLUDING DEFAULTS) ON COMMIT DROP;')
    columns = ['Id','Name'] + ([parent] if parent else []) + (['Type'] if size==10 else [])
    print(f'COPY seed_{table} (' + ','.join('"'+c+'"' for c in columns) + ') FROM STDIN;')
    for code,name in subset:
        values=[code,name]
        if parent:
            prefix=code[:{4:2,6:4,10:6}[size]]; assert prefix in rows
            values.append(prefix)
        if size==10: values.append({'1':'Kelurahan','2':'Desa','3':'DesaAdat'}[code[6]])
        print('\t'.join(v.replace('\\','\\\\').replace('\t','\\t').replace('\n','\\n').replace('\r','\\r') for v in values))
    print('\\.')
    cols=','.join('"'+c+'"' for c in columns)
    updates=','.join(f'"{c}"=EXCLUDED."{c}"' for c in columns[1:])
    print(f'INSERT INTO "{table}" ({cols}) SELECT {cols} FROM seed_{table} ON CONFLICT ("Id") DO UPDATE SET {updates};')
    print(f'{table}: {len(subset)}', file=sys.stderr)
print('COMMIT; ANALYZE "Villages";')

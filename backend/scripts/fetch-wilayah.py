#!/usr/bin/env python3
"""Refresh the pinned snapshot from the latest upstream commit; requires curl."""
import subprocess, json, pathlib, hashlib, datetime
root = pathlib.Path(__file__).resolve().parents[1] / 'seed-data'
def fetch(url): return subprocess.check_output(['curl', '--fail', '--silent', '--show-error', '--location', url])
base = 'https://api.github.com/repos/cahyadsn/wilayah'
commit = json.loads(fetch(base + '/commits?per_page=1'))[0]
data_commit = json.loads(fetch(base + '/commits?path=db/wilayah.sql&per_page=1'))[0]
sha = commit['sha']
raw = fetch(f'https://raw.githubusercontent.com/cahyadsn/wilayah/{sha}/db/wilayah.sql')
license = fetch(f'https://raw.githubusercontent.com/cahyadsn/wilayah/{sha}/LICENSE')
root.mkdir(exist_ok=True)
(root/'wilayah.sql').write_bytes(raw)
(root/'LICENSE').write_bytes(license)
(root/'manifest.json').write_text(json.dumps({'repository': 'https://github.com/cahyadsn/wilayah', 'commit': sha, 'date': commit['commit']['committer']['date'], 'datasetCommit': data_commit['sha'], 'datasetDate': data_commit['commit']['committer']['date'], 'fetchedAt': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'sha256': hashlib.sha256(raw).hexdigest()}, indent=2)+'\n')
print((root/'manifest.json').read_text())

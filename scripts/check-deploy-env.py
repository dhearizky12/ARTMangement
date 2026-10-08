#!/usr/bin/env python3
"""Cross-check env/secret names between .github/workflows/deploy.yml and the scripts that consume them.

Catches two typo classes that otherwise fail silently at deploy time:
  1. a ${{ secrets.X }} / ${{ vars.Y }} reference that nothing ever reads, and
  2. an env key declared in deploy.yml that no run block or shell script reads
     (so the script silently falls back to a default).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "deploy.yml"
CONSUMERS = [ROOT / "deploy-be.sh", *sorted((ROOT / "scripts").glob("*.sh"))]
NAME = r"[A-Za-z_][A-Za-z0-9_]*"


def used(name: str, text: str) -> bool:
    return bool(re.search(r"\$" + name + r"\b", text) or re.search(r"\$\{" + name + r"\b", text))


def declared_env_keys(text: str) -> list[str]:
    keys: list[str] = []
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        m = re.match(r"^(\s*)env:\s*$", lines[i])
        if not m:
            i += 1
            continue
        base = len(m.group(1))
        i += 1
        while i < len(lines):
            line = lines[i]
            if not line.strip() or line.lstrip().startswith("#"):
                i += 1
                continue
            indent = len(line) - len(line.lstrip())
            if indent <= base:
                break
            km = re.match(r"^\s+(" + NAME + r"):", line)
            if km:
                keys.append(km.group(1))
            i += 1
    return keys


def main() -> int:
    wf = WORKFLOW.read_text()
    stripped = re.sub(r"\$\{\{[^}]*\}\}", "", wf)
    consumer_texts = [(p, p.read_text()) for p in CONSUMERS]

    def evidence(name: str) -> list[str]:
        found = []
        if used(name, stripped):
            found.append("deploy.yml run block")
        for path, text in consumer_texts:
            if used(name, text):
                found.append(path.name)
        return found

    # secrets./vars. references: directly consumed, or renamed into a KEY that is consumed.
    aliases: dict[str, list[str]] = {}
    for key, name in re.findall(r"^\s+(" + NAME + r"):\s*\$\{\{\s*(?:secrets|vars)\.(" + NAME + r")\s*\}\}", wf, re.M):
        aliases.setdefault(name, []).append(key)

    errors: list[str] = []
    print(f"Cross-checking {WORKFLOW.relative_to(ROOT)} against deploy-be.sh and scripts/*.sh\n")

    refs = sorted(set(re.findall(r"\$\{\{\s*(?:secrets|vars)\.(" + NAME + r")\s*\}\}", wf)))
    print(f"Referenced secrets/vars ({len(refs)}):")
    for name in refs:
        ev = evidence(name)
        for key in aliases.get(name, []):
            if key != name:
                ev += [f"renamed to env `{key}` ({', '.join(evidence(key))})"]
        if ev:
            print(f"  OK   {name}  <-  {'; '.join(ev)}")
        else:
            print(f"  FAIL {name}  <-  never consumed by deploy-be.sh, scripts/*.sh, or any run block")
            errors.append(f"secrets/vars '{name}' is referenced but never consumed")

    env_keys = declared_env_keys(wf)
    print(f"\nDeclared env keys ({len(env_keys)}):")
    for key in env_keys:
        ev = evidence(key)
        if ev:
            print(f"  OK   {key}  <-  {'; '.join(ev)}")
        else:
            print(f"  FAIL {key}  <-  declared but never read anywhere")
            errors.append(f"env key '{key}' is declared in deploy.yml but never consumed")

    if errors:
        print("\nMISMATCHES:")
        for e in errors:
            print(f"  - {e}")
        return 1
    print("\nAll deploy.yml env/secret names are consumed somewhere. No mismatches.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

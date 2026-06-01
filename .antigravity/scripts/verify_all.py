#!/usr/bin/env python3
"""Compatibility wrapper for the ERP-vNext Phase 1 validation gate."""

from pathlib import Path
import subprocess
import sys


DELEGATED_GATE = Path("scripts/run_phase1_gates.sh")


def resolve_repo_root(argv: list[str]) -> Path:
    """Resolve the repository root from an optional legacy root argument."""
    if len(argv) > 1:
        joined = " ".join(argv)
        print(
            f"error: unsupported arguments: {joined}\n"
            "usage: python3 .antigravity/scripts/verify_all.py [repo-root]",
            file=sys.stderr,
        )
        sys.exit(2)

    if argv and argv[0].startswith("-"):
        print(
            f"error: unsupported option: {argv[0]}\n"
            "usage: python3 .antigravity/scripts/verify_all.py [repo-root]",
            file=sys.stderr,
        )
        sys.exit(2)

    if argv:
        return Path(argv[0]).resolve()

    return Path(__file__).resolve().parents[2]


def main(argv: list[str]) -> int:
    repo_root = resolve_repo_root(argv)
    gate_path = repo_root / DELEGATED_GATE

    if not gate_path.is_file():
        print(
            f"error: delegated gate not found: {DELEGATED_GATE}",
            file=sys.stderr,
        )
        return 1

    print(f"Delegating ERP-vNext validation to {DELEGATED_GATE}")
    result = subprocess.run(["bash", str(DELEGATED_GATE)], cwd=repo_root)
    return result.returncode


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

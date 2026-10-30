#!/usr/bin/env python3
"""Prepare pinned backend sources and translate the Windows designer forms for Linux."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request

PROJECT = Path(__file__).resolve().parent.parent
LINUX = PROJECT / 'linux'


def run(arguments, directory):
    subprocess.run(arguments, cwd=directory, check=True)


def prepare_dependency(name, specification, build_root):
    vendor = build_root / 'vendor'
    vendor.mkdir(exist_ok=True)
    destination = vendor / name
    patch = LINUX / 'patches' / specification['patch']
    identity = specification['commit'] + ':' + hashlib.sha256(patch.read_bytes()).hexdigest()
    marker = destination / '.mnx-source-version'
    if marker.exists() and marker.read_text() == identity:
        return
    if destination.exists() and not marker.exists():
        raise RuntimeError(f'Refusing to replace unmarked source directory: {destination}')
    downloads = build_root / 'downloads'
    downloads.mkdir(exist_ok=True)
    archive = downloads / f"{name}-{specification['commit']}.tar.gz"
    if not archive.exists():
        url = f"https://codeload.github.com/{specification['repository']}/tar.gz/{specification['commit']}"
        pending = archive.with_suffix('.pending')
        with urllib.request.urlopen(url, timeout=60) as response, pending.open('wb') as output:
            shutil.copyfileobj(response, output)
        pending.replace(archive)
    with tempfile.TemporaryDirectory(dir=vendor, prefix='unpack-') as temporary:
        unpacked = Path(temporary)
        with tarfile.open(archive) as package:
            package.extractall(unpacked, filter='data')
        directories = list(unpacked.iterdir())
        if len(directories) != 1 or not directories[0].is_dir():
            raise RuntimeError(f'Unexpected source archive layout: {archive}')
        source = directories[0]
        run(['git', 'apply', '--check', str(patch)], source)
        run(['git', 'apply', str(patch)], source)
        (source / '.mnx-source-version').write_text(identity)
        if destination.exists():
            shutil.rmtree(destination)
        shutil.move(source, destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=PROJECT.parent / 'mnx_clutter/build/mnx-gui-port')
    arguments = parser.parse_args()
    build_root = arguments.root.resolve()
    if build_root == PROJECT or PROJECT in build_root.parents:
        raise RuntimeError('Generated sources must be outside the MNX-GUI source directory.')
    build_root.mkdir(parents=True, exist_ok=True)
    for name, specification in json.loads((LINUX / 'dependencies.json').read_text()).items():
        prepare_dependency(name, specification, build_root)
    generated = build_root / 'gui'
    marker = generated / '.mnx-generated'
    if generated.exists():
        if not marker.exists():
            raise RuntimeError(f'Refusing to replace unmarked generated sources: {generated}')
        shutil.rmtree(generated)
    shutil.copytree(PROJECT / 'windows', generated,
        ignore=shutil.ignore_patterns('MashWin', 'bin', 'obj', '.vs', '*.user'))
    marker.touch()
    migrator = build_root / 'vendor/Majorsilence.Forms/tools/Majorsilence.Forms.Migrator'
    run(['dotnet', 'run', '--project', str(migrator), '-p:EnableMobileHeads=false',
        '-p:UseSharedCompilation=false', '--', str(generated), '--backend', 'headless',
        '--no-backup', '--no-report'], PROJECT)
    run(['git', 'apply', '--check', str(LINUX / 'patches/gui.patch')], generated)
    run(['git', 'apply', str(LINUX / 'patches/gui.patch')], generated)
    program = generated / 'Program.cs'
    program.write_text(program.read_text().replace('private static int Main(string[] args)', 'internal static int Main(string[] args)'))
    for resource in generated.rglob('*.resx'):
        resource.with_suffix('.resx.raw').write_bytes(resource.read_bytes())
    print(f'Portable GUI sources: {generated}')


if __name__ == '__main__':
    main()

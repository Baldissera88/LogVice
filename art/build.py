# Renders the two icons in icons.py and writes LogVice.ico, LogFile.ico and docs/images/logo.png.
# Needs Python with Pillow, and Node.js with Playwright and Chromium.
# Run from the repository root: python3 art/build.py
import json
import os
import subprocess
import sys

from PIL import Image

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(here)
sys.path.insert(0, here)
import icons

sizes = [16, 20, 24, 32, 40, 48, 64, 256, 512]
out = os.path.join(here, 'png')
os.makedirs(out, exist_ok=True)

jobs = []
for kind in ('app', 'doc'):
    for size in sizes:
        jobs.append({'size': size, 'svg': getattr(icons, kind)(size), 'out': os.path.join(out, '%s-%d.png' % (kind, size))})
jobs_file = os.path.join(out, 'jobs.json')
with open(jobs_file, 'w') as f:
    json.dump(jobs, f)
subprocess.check_call(['node', os.path.join(here, 'render.js'), jobs_file])

ico_sizes = [256, 64, 48, 40, 32, 24, 20, 16]
for kind, name in (('app', 'LogVice.ico'), ('doc', 'LogFile.ico')):
    images = [Image.open(os.path.join(out, '%s-%d.png' % (kind, s))).convert('RGBA') for s in ico_sizes]
    images[0].save(os.path.join(root, name), format='ICO', sizes=[im.size for im in images], append_images=images[1:])
    print('Wrote', name)

Image.open(os.path.join(out, 'app-512.png')).save(os.path.join(root, 'docs', 'images', 'logo.png'))
print('Wrote docs/images/logo.png')

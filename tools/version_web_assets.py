"""Publish matching versioned web assets; use --check in CI."""
from pathlib import Path
import hashlib
import re
import sys

root=Path(__file__).resolve().parents[1]/'docs'
page_path=root/'index.html'
page=page_path.read_text(encoding='utf-8-sig')
check='--check' in sys.argv
for name in ('style.css','app.js','favicon.svg'):
    data=(root/name).read_bytes().replace(b'\r\n',b'\n')
    stem,ext=name.rsplit('.',1)
    version=hashlib.sha256(data).hexdigest()[:12]
    target=f'assets/{stem}.{version}.{ext}'
    if check:
        assert target in page, f'Run python tools/version_web_assets.py after editing {name}'
        assert (root/target).read_bytes().replace(b'\r\n',b'\n')==data, f'Published {name} does not match source'
    else:
        (root/'assets').mkdir(exist_ok=True)
        (root/target).write_bytes(data)
        pattern=r'(?<=["\'])(?:assets/)?'+re.escape(stem)+r'(?:\.[0-9a-f]{12})?\.'+re.escape(ext)+r'(?:\?[^"\']*)?(?=["\'])'
        page=re.sub(pattern,target,page)
if not check:
    page_path.write_text(page,encoding='utf-8')
print('PASS versioned stylesheet, script and favicon match their sources')

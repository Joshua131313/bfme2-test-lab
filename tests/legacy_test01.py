"""Reproduce the reported exact Test01 hashes and verify migration/restore."""
from pathlib import Path
import json,hashlib,shutil,subprocess,uuid
r=Path(__file__).resolve().parents[1];s=Path(r"C:\Program Files (x86)\BFME2 Ecth's Patch Switcher\100")
pkg=r/('build/BFME2-Test-Lab-'+(r/'version.txt').read_text().strip())
selected=r.parent/'dist/BFME2-1.00-Hybrid-Test04'
m=json.loads((selected/'patch.json').read_text());legacy=json.loads((r.parent/'dist/BFME2-1.00-Test01/patch.json').read_text())
work=r/'test-output'/str(uuid.uuid4());game=work/'game';game.mkdir(parents=True)
backup=game/'_BFME2_100_Test01_Backup';backup.mkdir()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for f in m['files']+m['baseline']:
 p=game/f['name'];p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(s/f['name'],p)
for f in legacy['files']:
 original=(s/f['name']).read_bytes();b=bytearray(original)
 for edit in f['edits']:
  before=bytes.fromhex(edit['before']);after=bytes.fromhex(edit['after']);offset=edit['offset'];assert b[offset:offset+len(before)]==before;b[offset:offset+len(before)]=after
 b+=bytes.fromhex(f['append_hex']);assert hashlib.sha256(b).hexdigest()==f['patched_sha256']
 (game/f['name']).write_bytes(b);(backup/(f['name']+'.original')).write_bytes(original)
initial={f['name']:sha(game/f['name']) for f in m['files']};results=[]
def run(action,expected=0):
 p=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(pkg/'Apply-Package.ps1'),'-Action',action,'-GameDirectory',str(game),'-PackageDirectory',str(selected),'-CacheDirectory',str(work/'empty-cache')],capture_output=True,text=True)
 results.append({'action':action,'expected':expected,'exit':p.returncode,'output':p.stdout+p.stderr});assert p.returncode==expected,p.stdout+p.stderr
run('Validate');assert 'recognized previous test build' in results[-1]['output']
(backup/'INI.big.original').rename(backup/'INI.saved')
run('Install',1);assert all(sha(game/n)==h for n,h in initial.items())
(backup/'INI.saved').rename(backup/'INI.big.original')
run('Install')
assert all(sha(game/f['name'])==f['patched_sha256'] for f in m['files'])
run('Install');run('Restore')
assert all(sha(game/f['name'])==f['original_sha256'] for f in m['files'])
assert all(sha(backup/(f['name']+'.original'))==f['original_sha256'] for f in legacy['files'])
# After restoring, an unrecognized edited INI is still refused.
with (game/'INI.big').open('ab') as f:f.write(b'unknown-mod')
run('Install',1);assert sha(game/'game.dat')==m['files'][0]['original_sha256']
(r/'test-output/legacy-test01-results.json').write_text(json.dumps(results,indent=2))
print('PASS: exact Test01 identification, missing-backup refusal without changes, migration, reinstall, original restore, unknown INI refusal.')

from pathlib import Path
import json,shutil,subprocess,hashlib,uuid
root=Path(__file__).resolve().parents[1]
pkg=root/('build/BFME2-Test-Lab-'+(root/'version.txt').read_text().strip())
source=root.parent/'dist/BFME2-1.00-Hybrid-Test04'
old=root.parent/'dist/BFME2-1.00-Hybrid-Test03'
switcher=Path(r"C:\Program Files (x86)\BFME2 Ecth's Patch Switcher\100")
work=root/'test-output'/str(uuid.uuid4());game=work/'game';cache=work/'cache';game.mkdir(parents=True);cache.mkdir()
shutil.copytree(source,cache/'Test04');shutil.copytree(old,cache/'Test03')
m=json.loads((source/'patch.json').read_text());results=[]
for f in m['files']+m['baseline']:
 p=game/f['name'];p.parent.mkdir(exist_ok=True,parents=True);shutil.copyfile(switcher/f['name'],p)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def state(which):
 for f in m['files']:assert sha(game/f['name'])==f[which+'_sha256']
def run(action,expected=0,selected='Test04'):
 r=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(pkg/'Apply-Package.ps1'),'-Action',action,'-GameDirectory',str(game),'-PackageDirectory',str(cache/selected),'-CacheDirectory',str(cache)],capture_output=True,text=True)
 results.append({'action':action,'selected':selected,'expected':expected,'exit':r.returncode,'output':r.stdout+r.stderr});assert r.returncode==expected,r.stdout+r.stderr
run('Validate');state('original')
# A readable different map archive is preserved, not a reason to reject core patching.
with (game/'Maps.big').open('ab') as f:f.write(b'map-variant-fixture')
map_hash=sha(game/'Maps.big')
run('Validate');assert 'Maps.big differs' in results[-1]['output']
run('Install');state('patched');assert sha(game/'Maps.big')==map_hash
run('Restore');state('original');assert sha(game/'Maps.big')==map_hash
(game/'Maps.big').rename(game/'Maps.saved')
run('Install',1);state('original')
(game/'Maps.big').write_bytes(b'not a BIG archive')
run('Install',1);state('original')
shutil.copyfile(switcher/'Maps.big',game/'Maps.big')
with (game/'Data1.big').open('ab') as f:f.write(b'changed-core')
run('Install',1);state('original')
shutil.copyfile(switcher/'Data1.big',game/'Data1.big')
run('Install');state('patched');run('Install');state('patched');run('Restore');state('original')
run('Install',selected='Test03');run('Install');state('patched');run('Restore');state('original')
# Existing standalone Test04 backup migration.
backup=game/'_BFME2_100_Hybrid04_Backup';backup.mkdir()
for f in m['files']:
 shutil.copyfile(switcher/f['name'],backup/(f['name']+'.original'));shutil.copyfile(source/'payload'/f['name'],game/f['name'])
existing=game/'_BFME2_TestLab_Backup'
existing.rename(game/'saved-testlab-backup')
run('Install');state('patched');run('Restore');state('original')
# Changed current file and corrupted payload refuse before mutation.
with (game/'INI.big').open('ab') as f:f.write(b'changed')
run('Install',1);assert sha(game/'game.dat')==m['files'][0]['original_sha256']
shutil.copyfile(switcher/'INI.big',game/'INI.big')
with (cache/'Test04/payload/INI.big').open('ab') as f:f.write(b'corrupt')
run('Install',1);state('original');shutil.copyfile(source/'payload/INI.big',cache/'Test04/payload/INI.big')
# Locked second destination forces rollback after engine commit.
lock=work/'lock.ps1'
lock.write_text("$f=[IO.File]::Open($args[0],[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read);try { & $args[1] -Action Install -GameDirectory $args[2] -PackageDirectory $args[3] -CacheDirectory $args[4] } finally {$f.Dispose()}; exit $LASTEXITCODE")
r=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(lock),str(game/'INI.big'),str(pkg/'Apply-Package.ps1'),str(game),str(cache/'Test04'),str(cache)],capture_output=True,text=True)
assert r.returncode==1 and 'Rolled back game.dat' in r.stdout,r.stdout+r.stderr
state('original');results.append({'action':'locked file rollback','output':r.stdout})
# Alternate 1.00-labelled executable: preserve its exact original bytes.
variant=bytearray((switcher/'game.dat').read_bytes());variant[0x40]^=1
(game/'game.dat').write_bytes(variant);variant_hash=sha(game/'game.dat')
run('Validate');assert '1.00 with a different hash' in results[-1]['output']
run('Install');state('patched')
variant_backup=game/'_BFME2_TestLab_Variant_Backup/game.dat.original'
assert sha(variant_backup)==variant_hash
run('Install');state('patched')
run('Restore');assert sha(game/'game.dat')==variant_hash
run('Restore');assert sha(game/'game.dat')==variant_hash
run('Install');state('patched')
with variant_backup.open('ab') as f:f.write(b'tamper')
run('Restore',1);state('patched')
variant_backup.write_bytes(variant)
run('Restore');assert sha(game/'game.dat')==variant_hash
# A different official patch's executable must remain unsupported.
shutil.copyfile(switcher.parent/'106/game.dat',game/'game.dat')
foreign_hash=sha(game/'game.dat');run('Install',1);assert sha(game/'game.dat')==foreign_hash
(game/'game.dat').write_bytes(b'MZ truncated')
run('Install',1);assert (game/'game.dat').read_bytes()==b'MZ truncated'
(root/'test-output/installer-results.json').write_text(json.dumps(results,indent=2));print('PASS:',len(results),'installer scenarios')

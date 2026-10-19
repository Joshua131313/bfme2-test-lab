"""Create/publish the requested project using existing Git Credential Manager.
Credentials are only held in memory and never printed or written to disk.
Usage: python scripts/github-release.py create|publish
"""
import hashlib,json,pathlib,subprocess,sys,urllib.request,urllib.error
ROOT=pathlib.Path(__file__).resolve().parents[1]
VERSION=(ROOT/'version.txt').read_text().strip();TAG='v'+VERSION
OWNER='Joshua131313';NAME='bfme2-test-lab';BASE='https://api.github.com/repos/'+OWNER+'/'+NAME
r=subprocess.run(['git','credential','fill'],input='protocol=https\nhost=github.com\n\n',capture_output=True,text=True,check=True)
fields=dict(line.split('=',1) for line in r.stdout.splitlines() if '=' in line)
headers={'Authorization':'Bearer '+fields['password'],'User-Agent':'BFME2-Test-Lab-release','Accept':'application/vnd.github+json'}
def request(url,data=None,method=None,kind='application/json'):
    if isinstance(data,dict):data=json.dumps(data).encode()
    req=urllib.request.Request(url,data=data,method=method,headers={**headers,'Content-Type':kind})
    with urllib.request.urlopen(req,timeout=180) as response:return json.load(response)
assert request('https://api.github.com/user')['login']==OWNER
if sys.argv[1]=='create':
    try:
        repo=request(BASE)
        if repo['description']!='Small Windows launcher for BFME2 1.00 hybrid test builds.':raise RuntimeError('Repository name already in use.')
    except urllib.error.HTTPError as e:
        if e.code!=404:raise
        repo=request('https://api.github.com/user/repos',{'name':NAME,'description':'Small Windows launcher for BFME2 1.00 hybrid test builds.','private':False,'auto_init':False},'POST')
    print(repo['html_url'])
elif sys.argv[1]=='publish':
    commit=subprocess.check_output(['git','-c','safe.directory='+str(ROOT),'rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    body=(ROOT/'release-notes.txt').read_text()
    try:release=request(BASE+'/releases/tags/'+TAG)
    except urllib.error.HTTPError as e:
        if e.code!=404:raise
        release=request(BASE+'/releases',{'tag_name':TAG,'target_commitish':commit,'name':'BFME2 Test Lab '+VERSION+' - Hybrid Test04','body':body,'draft':True},'POST')
    for name in ['BFME2-Test-Lab-'+VERSION+'.zip','BFME2-TestPackage.zip']:
        p=ROOT/'build'/name;data=p.read_bytes()
        assets=request(BASE+'/releases/'+str(release['id'])+'/assets');asset=next((a for a in assets if a['name']==name),None)
        if asset is None:asset=request(release['upload_url'].split('{')[0]+'?name='+name,data,'POST','application/zip')
        if asset.get('digest')!='sha256:'+hashlib.sha256(data).hexdigest():raise RuntimeError('Published checksum differs; do not overwrite releases.')
    release=request(BASE+'/releases/'+str(release['id']),{'draft':False,'prerelease':False,'make_latest':'true'},'PATCH')
    assert request(BASE+'/releases/latest')['tag_name']==TAG
    print(release['html_url'])
else:raise ValueError('Expected create or publish')

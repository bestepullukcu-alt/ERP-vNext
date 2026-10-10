# mints a short-lived platform_admin HS256 token for the isolated stack; writes "Authorization: Bearer ..." to a 600 file, prints nothing secret
import base64,hmac,hashlib,json,time,sys,os,subprocess
E=sys.argv[1]; port=sys.argv[2]
out=subprocess.run(["mongosh","--quiet","--port",port,"--eval",'const d=db.getSiblingDB("q358_platform").platform_administrators.findOne({IsDeleted:false,Status:1}); print(JSON.stringify({e:d.NormalizedEmail||d.Email,id:String(d._id)}))'],capture_output=True,text=True).stdout.strip().splitlines()[-1]
adm=json.loads(out)
def b64(b): return base64.urlsafe_b64encode(b).rstrip(b"=")
now=int(time.time())
hdr={"alg":"HS256","typ":"JWT"}
pl={"sub":adm["id"],"email":adm["e"],"actor_type":"platform_admin","iss":"diten-auth-service","aud":"diten-erp","iat":now,"nbf":now,"exp":now+1800}
si=b64(json.dumps(hdr,separators=(",",":")).encode())+b"."+b64(json.dumps(pl,separators=(",",":")).encode())
sig=b64(hmac.new(open(E+"/jwt.secret","rb").read(),si,hashlib.sha256).digest())
os.umask(0o077); open(E+"/run/admin.hdr","w").write("Authorization: Bearer "+(si+b"."+sig).decode()+"\n")
print("token minted for platform admin (email len %d), claims: %s"%(len(adm["e"]),sorted(k for k in pl if k!="email")))

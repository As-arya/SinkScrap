using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SinkScrapt
{
    public class SinkApp : MonoBehaviour
    {
        public static SinkApp Instance;
        public BalanceConfig balance;
        public NetworkManager network;
        public string notice="Siapkan alat. Bawa pulang yang berharga.";
        public string joinCode="";
        public string playerName="Penyelam";
        public string profile;
        public bool busy;
        public bool newCampaign;
        // Host option for testing: expeditions without monsters (remembered; `--no-monsters` on the command line).
        public bool noMonsters;
        public bool localTransport;
        public string SavePath;
        public ushort localPort=7777;
        ISession onlineSession;
        bool shuttingDown;
        public bool Connected=>network!=null&&network.IsListening;
        public bool OnlineConfigured=>!string.IsNullOrWhiteSpace(Application.cloudProjectId);
        public double TimeNow=>Connected?network.ServerTime.Time:Time.unscaledTimeAsDouble;

        void Awake()
        {
            if(Instance!=null) {Destroy(gameObject);return;} Instance=this;
            Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            balance=Resources.Load<BalanceConfig>("SinkScrapt/Balance");
            if(balance==null) {balance=ScriptableObject.CreateInstance<BalanceConfig>();Debug.LogWarning("Balance asset not found; defaults used.");}
            profile=PlayerPrefs.GetString("SinkScraptProfile","");
            if(profile.Length==0) {profile=Guid.NewGuid().ToString("N");PlayerPrefs.SetString("SinkScraptProfile",profile);}
            var args=Environment.GetCommandLineArgs();
            SavePath=CampaignSave.DefaultPath;
            noMonsters=PlayerPrefs.GetInt("SinkNoMonsters",0)==1||Array.IndexOf(args,"--no-monsters")>=0;
            for(int i=0;i<args.Length-1;i++) if(args[i]=="--profile") {profile=args[i+1];playerName=profile;}
            for(int i=0;i<args.Length-1;i++) if(args[i]=="--save-path")SavePath=args[i+1];
            for(int i=0;i<args.Length-1;i++) if(args[i]=="--local-port"&&ushort.TryParse(args[i+1],out var port)&&port>0)localPort=port;
            network=gameObject.AddComponent<NetworkManager>();var transport=gameObject.AddComponent<UnityTransport>();
            // The campaign snapshot (deck cargo with positions) can outgrow the 6 KB default reliable payload.
            transport.MaxPayloadSize=64*1024;
            network.NetworkConfig=new NetworkConfig();
            network.NetworkConfig.NetworkTransport=transport;
            network.NetworkConfig.TickRate=30;network.NetworkConfig.EnableSceneManagement=true;
            network.NetworkConfig.ConnectionApproval=true;network.NetworkConfig.ConnectionData=System.Text.Encoding.UTF8.GetBytes("sink-alpha-2");
            network.ConnectionApprovalCallback=Approve;
            foreach(var name in new[]{"Player","Session","Item","Enemy"}) network.AddNetworkPrefab(Resources.Load<GameObject>("SinkScrapt/"+name));
            network.NetworkConfig.PlayerPrefab=Resources.Load<GameObject>("SinkScrapt/Player");
            network.OnServerStarted+=ServerStarted;network.OnClientDisconnectCallback+=Disconnected;
            gameObject.AddComponent<RetroLook>();gameObject.AddComponent<GameHUD>();gameObject.AddComponent<VoiceComms>();gameObject.AddComponent<AudioDirector>();
            WorldFactory.ConfigureEnvironment();
            MenuBackdrop();
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();yield return null;
            newCampaign=newCampaign||args.Contains("--new-campaign");
            if(Array.IndexOf(args,"--local-host")>=0) {newCampaign=Array.IndexOf(args,"--new-campaign")>=0;StartLocal(true);}
            if(Array.IndexOf(args,"--local-client")>=0) StartCoroutine(AutoJoinLocal());
            if(Array.IndexOf(args,"--online-host")>=0)_=StartOnline(true,"");
            int onlineIndex=Array.IndexOf(args,"--online-client");if(onlineIndex>=0&&onlineIndex+1<args.Length)_=StartOnline(false,args[onlineIndex+1]);
            if(Array.IndexOf(args,"--qa-host")>=0) {
                noMonsters=false;var runner=gameObject.AddComponent<QaRunner>();runner.expectedPlayers=4;runner.iterations=10;
                int players=Array.IndexOf(args,"--qa-players");if(players>=0&&players+1<args.Length&&int.TryParse(args[players+1],out int count))runner.expectedPlayers=Mathf.Clamp(count,1,4);
            }
        }
        // `--local-client`: keep trying for a while, because the host refuses joins while it is loading scenes.
        IEnumerator AutoJoinLocal()
        {
            for(int attempt=0;attempt<25;attempt++) {
                if(Connected&&NetPlayer.Local!=null)yield break;
                if(!Connected&&!busy&&!shuttingDown)StartLocal(false);
                yield return new WaitForSecondsRealtime(4);
            }
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            bool version=System.Text.Encoding.UTF8.GetString(request.Payload)=="sink-alpha-2",room=network.ConnectedClients.Count<4;
            var session=NetSession.Instance;bool host=request.ClientNetworkId==NetworkManager.ServerClientId;
            bool busy=!host&&(session==null||session.Busy);
            bool valid=version&&room&&!busy;
            response.Approved=valid;response.CreatePlayerObject=valid;response.Pending=false;
            response.Reason=valid?"":!version?"Versi build berbeda dengan host.":!room?"Permainan sudah penuh (4 pemain).":"Host sedang memuat lokasi. Coba lagi beberapa detik lagi.";
            response.Position=WorldZone.BoatSpawn+new Vector3((request.ClientNetworkId%3)*.8f,0,0);
        }
        void ServerStarted()
        {
            var go=Instantiate(Resources.Load<GameObject>("SinkScrapt/Session"));go.GetComponent<NetworkObject>().Spawn(false);
        }
        public void StartLocal(bool host,string address="127.0.0.1")
        {
            if(Connected||busy)return;
            localTransport=true;notice=host?"Memulai simulasi...":"Menghubungkan ke host lokal...";
            network.GetComponent<UnityTransport>().SetConnectionData(address,localPort,"0.0.0.0");
            bool ok=host?network.StartHost():network.StartClient();if(!ok)notice="Koneksi lokal gagal dimulai.";
            if(host)joinCode="LOKAL :7777";
        }
        public async Task StartOnline(bool host,string code)
        {
            if(Connected||busy)return;busy=true;localTransport=false;
            try {
                if(!OnlineConfigured) throw new InvalidOperationException("Permainan online belum tersedia pada konfigurasi build ini. Gunakan solo atau jaringan lokal.");
                string authProfile="sink"+new string(profile.Where(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-').Take(24).ToArray());
                if(UnityServices.State!=ServicesInitializationState.Initialized) await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(authProfile));
                if(!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                notice=host?"Membuat permainan privat...":"Menghubungkan permainan...";
                if(host) onlineSession=await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers=4,IsPrivate=true,Name="SinkScrapt" }.WithRelayNetwork());
                else onlineSession=await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                joinCode=onlineSession.Code;notice="Terhubung. Kode: "+joinCode;
                await GetComponent<VoiceComms>().Connect(onlineSession.Id);
            } catch(Exception e) {notice=e.Message;Debug.LogWarning("Online: "+e.Message);if(network.IsListening)network.Shutdown();}
            finally {busy=false;}
        }
        void Disconnected(ulong client)
        {
            if(network.IsServer&&NetSession.Instance!=null) NetSession.Instance.OnCrewDisconnected(client);
            if(client==network.LocalClientId&&!network.IsServer&&!shuttingDown) {var reason=network.DisconnectReason;notice=string.IsNullOrEmpty(reason)?"Host terputus. Lanjutkan dari checkpoint terakhir pada host.":reason;Leave();}
        }
        public async void Leave()
        {
            if(busy)return;busy=true;shuttingDown=true;
            try {if(onlineSession!=null) await onlineSession.LeaveAsync();}
            catch(Exception e) {Debug.LogWarning(e.Message);}
            finally {onlineSession=null;if(network.IsListening)network.Shutdown();await GetComponent<VoiceComms>().Disconnect();busy=false;StartCoroutine(ReturnToMenu());}
        }
        IEnumerator ReturnToMenu()
        {
            if(network.IsListening)network.Shutdown();yield return null;
            foreach(var name in new[]{"Interior","Exterior","Island","SmallBoat"}) {
                var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);
                if(scene.isLoaded) {var op=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);if(op!=null)yield return op;}
            }
            foreach(var player in NetPlayer.All.ToArray()) if(player!=null)Destroy(player.gameObject);
            var session=NetSession.Instance;if(session!=null)Destroy(session.gameObject);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;joinCode="";shuttingDown=false;
            var camera=GameObject.Find("Menu Camera");if(camera!=null){camera.GetComponent<Camera>().enabled=true;camera.GetComponent<AudioListener>().enabled=true;}
            if(GameHUD.Instance!=null)GameHUD.Instance.Close();
            SetMenuBackdrop(true);
        }
        public void SetNoMonsters(bool on) {noMonsters=on;PlayerPrefs.SetInt("SinkNoMonsters",on?1:0);}
        public void SetMenuBackdrop(bool active) {var root=transform.Find("MenuBackdrop");if(root!=null)root.gameObject.SetActive(active);}
        void MenuBackdrop()
        {
            var root=new GameObject("MenuBackdrop").transform;root.SetParent(transform,false);root.localPosition=new Vector3(-300,0,0);
            var sea=WorldFactory.Shape("Sea",PrimitiveType.Plane,root,Vector3.zero,new Vector3(40,1,40),new Color(.06f,.21f,.24f),false);
            var water=Resources.Load<Material>("SinkScrapt/Water");if(water!=null)sea.GetComponent<Renderer>().sharedMaterial=water;
            WorldFactory.Box("Cargo silhouette",root,new Vector3(0,4,0),new Vector3(28,8,65),WorldFactory.Hull,false);
            WorldFactory.Box("Deck",root,new Vector3(0,8.1f,0),new Vector3(29,.2f,66),WorldFactory.Steel,false);
            for(int i=0;i<8;i++)WorldFactory.Box("Freight",root,new Vector3(i%2==0?-7:7,9.7f,-22+i/2*14),new Vector3(6,3,10),i%3==0?WorldFactory.Rust:WorldFactory.Teal,false);
            WorldFactory.Box("Bridge",root,new Vector3(0,10.8f,-28),new Vector3(10,5,9),WorldFactory.Steel,false);
            var light=new GameObject("Preview light");light.transform.SetParent(root,false);light.transform.rotation=Quaternion.Euler(14,-30,0);var sun=light.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.color=new Color(1,.68f,.48f);RenderSettings.sun=sun;
            // Slow red beacon and deck floodlights so the wreck reads as a living silhouette against the dusk.
            var mast=WorldFactory.Shape("Mast light",PrimitiveType.Sphere,root,new Vector3(0,22,-28),Vector3.one*.6f,new Color(1,.15f,.1f),false,true);
            Fx.Glow(mast.transform,Vector3.zero,new Color(1,.15f,.1f),45,6).gameObject.AddComponent<LightBlink>().period=2.2f;
            foreach(float x in new[]{-12f,12f})foreach(float z in new[]{-22f,0f,22f}) {var lamp=WorldFactory.Shape("Floodlight",PrimitiveType.Sphere,root,new Vector3(x,11.4f,z),Vector3.one*.4f,new Color(1,.85f,.65f),false,true);Fx.Glow(lamp.transform,Vector3.down*.5f,new Color(1,.8f,.6f),20,3.5f).gameObject.AddComponent<LightFlicker>();}
            var cameraObject=GameObject.Find("Menu Camera");if(cameraObject!=null) {cameraObject.GetComponent<Camera>().clearFlags=CameraClearFlags.Skybox;cameraObject.AddComponent<MenuDrift>();}
        }
        void OnDestroy() {if(Instance==this)Instance=null;}
    }

    // Lets the main-menu camera creep around the sinking wreck.
    public class MenuDrift : MonoBehaviour
    {
        Vector3 home;
        readonly Vector3 focus=new(-300,9,0);
        void Awake() {home=transform.position;}
        void Update()
        {
            float t=Time.unscaledTime;transform.position=home+new Vector3(Mathf.Sin(t*.11f)*9,Mathf.Sin(t*.07f)*1.8f,Mathf.Sin(t*.05f)*5);transform.LookAt(focus);
        }
    }
}

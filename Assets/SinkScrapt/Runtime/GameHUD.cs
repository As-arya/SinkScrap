using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SinkScrapt
{
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance;
        public static float Sensitivity=1,Fov=75;
        public bool Modal=>menu.gameObject.activeSelf||dialog.gameObject.activeSelf;
        // The campaign line under the wallet (cycle, days, package countdown), as shown; read by the QA client view.
        public string StatusLine=>cycleText!=null?cycleText.text:"";
        public string LastMenuMode=>menuMode;
        static readonly Color LcdBack=new(.10f,.14f,.09f,.97f),LcdInk=new(.64f,.88f,.46f),LcdDim=new(.22f,.32f,.18f);
        static readonly (string title,string body)[] Pages={
            ("SELAMAT DATANG DI KAPAL KECIL","Kamu dan kru menyelam ke kapal kargo yang sedang tenggelam dan membawa pulang scrap berharga.\n\nSatu kuota = 3 hari (3 ekspedisi). Setelah hari ketiga, berlayar ke pulau untuk menjual scrap ke pedagang dan menutup kuota. Target pertama: $600 penjualan. Modal awal tim: $120."),
            ("PESAN ALAT","Tidak ada alat gratis. Pesan di KOMPUTER KAPAL (tab TOKO), kapan saja: di laut, di pulau, atau saat ekspedisi.\n\n  Senter $15  •  Baterai $10  •  Radio $25  •  Tabung oksigen $80  •  Flare $20\n\nPaket diterjunkan dengan parasut ke kapal kecil sekitar 30 detik kemudian. Tekan E pada paket untuk membukanya."),
            ("KAPAL KECIL DAN TALI","Di kabin ada KEMUDI untuk berangkat, KOMPUTER KAPAL, dan RAK ALAT.\n\nDi kapal kargo, panjat tali dengan W / S lalu masuk lewat PALKA A (dek atas), B atau C. Letak pintu di dalam berubah setiap ekspedisi. Ekspedisi 10 menit: kapal mulai tenggelam pada menit 4 dan hilang di menit 10. Lihat JAM di kiri atas."),
            ("MUATAN DI KAPAL","Barang yang kamu letakkan di kapal kecil (G) tetap di tempatnya dan disimpan bersama kampanye.\n\n<color=#E64033>Tetapi jika seluruh kru mati atau tidak ada yang kembali ke kapal kecil saat berangkat, SEMUA barang di kapal (dek dan rak alat) ikut hilang.</color> Minimal satu kru harus pulang."),
            ("SENTER DAN SABUK","Kamu punya 4 slot tangan dan 1 SLOT SABUK untuk senter atau radio. [5] memindahkan senter/radio yang dipegang ke sabuk.\n\n[F] menyalakan senter kapan saja: senter tetap menyala saat kamu ganti barang atau membawa barang dua tangan. Satu baterai sekitar 2,5 menit; pegang baterai lalu klik kiri untuk memasangnya."),
            ("BAHAYA DI DALAM","Di dalam kapal hampir gelap gulita. PENGUNTIT LAMBUNG memburu di lorong kering: ia bisa memukul atau MENCENGKERAM lalu MELEMPARMU. PEMBURU TENGGELAM menguasai dek yang terendam.\n\nBeberapa pintu terkunci: cari KUNCI di dalam kapal. Di balik pintu terkunci selalu ada barang berharga."),
            ("KONTROL","WASD gerak • Shift lari • Ctrl jongkok/turun • Space lompat/naik/panjat tepian • E interaksi • 1–4 slot • 5 sabuk • F senter • G jatuhkan • Klik kiri flare/baterai • V suara • R radio • Esc menu\n\nKOMPUTER KAPAL: toko, peta dek kru yang dipantau, level air, kontrol bahaya.")
        };
        RectTransform canvas,menu,hud,dialog,card;
        TMP_Text watchTime,watchCaption,departLabel,objective,crew,focusTitle,focusDetail,toast,alert,lowAir,hints,voice,money,quotaText,cycleText,spectator,airText,beltName,beltKey;
        UiBar health,stamina,quotaBar;
        Image ring,dot,fade,airFill,beltIcon,healthIcon;
        Image[] beltCells;
        RectTransform focusGroup,toastBox,beltBattery;
        CanvasGroup toastGroup,airGroup;
        float airShown=-1,airVisibleUntil;
        Ui.KeyCap focusKey;
        readonly List<Slot> slots=new();
        readonly HashSet<string> saleSelection=new();
        class Slot {public RectTransform root;public Image edge,back,well,icon;public TMP_Text key,name,tag;}
        TMP_FontAsset font;
        float nextRefresh,fadeAlpha,toastUntil,alertUntil,voiceVolume=.8f,toastAlpha;
        int salePage,guidePage;
        string shownResult="",menuMode="",lastPhase="",lastNotice="";
        ActionKind mode;
        Color alertColor=Theme.Amber;

        void Awake()
        {
            Instance=this;font=Theme.Font;Cinematic.Ensure(transform);
            Sensitivity=PlayerPrefs.GetFloat("SinkLook",1);Fov=PlayerPrefs.GetFloat("SinkFov",75);AudioListener.volume=PlayerPrefs.GetFloat("SinkVolume",.8f);voiceVolume=PlayerPrefs.GetFloat("SinkVoiceVolume",.8f);
            QualitySettings.SetQualityLevel(Mathf.Clamp(1,0,QualitySettings.names.Length-1),true);
            if(!Application.isEditor&&!Application.isBatchMode&&PlayerPrefs.GetInt("SinkFull",0)==1)Screen.fullScreenMode=FullScreenMode.FullScreenWindow;
            var go=new GameObject("SinkScrapt UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);canvas=(RectTransform)go.transform;go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=0;
            if(EventSystem.current==null){var ev=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));ev.transform.SetParent(transform,false);ev.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            hud=Ui.Stretch("HUD",canvas);BuildHUD();
            menu=Ui.Panel(canvas,"Menu",new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(96,0),new Vector2(700,990),Theme.Ink,Theme.Line,true);
            BuildToasts();
            var overlay=Ui.Stretch("Modal",canvas);Ui.Fill(overlay,new Color(0,0,0,.66f),true);dialog=overlay;
            card=Ui.Panel(dialog,"Dialog card",Ui.Center,Ui.Center,Vector2.zero,new Vector2(940,1000),Theme.Ink,Theme.Line,true);BuildMonitorFrame();dialog.gameObject.SetActive(false);
            var fadeRect=Ui.Stretch("Transition",canvas);fade=Ui.Fill(fadeRect,Theme.Clear);
            BuildMenu();
        }
        // ---- HUD construction ----------------------------------------------------------------------------------------------------
        void BuildHUD()
        {
            // A wristwatch with the 24-hour time of day (top left), the objective under it. The sinking itself is no longer on the
            // HUD: the ship's state lives on the boat computer, the alarms and the visible water.
            var watch=Ui.MetalPanel(hud,"Watch",Ui.TopLeft,Ui.TopLeft,new Vector2(40,-34),new Vector2(236,104),LcdBack);
            watchCaption=Ui.Label(watch,"JAM",new Vector2(16,-12),new Vector2(204,18),15,LcdDim);
            watchTime=Ui.Label(watch,"--:--",new Vector2(0,-22),new Vector2(236,72),64,LcdInk,TextAlignmentOptions.Center);watchTime.fontStyle=FontStyles.Bold;watchTime.characterSpacing=6;
            departLabel=Ui.Label(hud,"",new Vector2(44,-142),new Vector2(300,30),22,Theme.Red);
            objective=Ui.Label(hud,"",new Vector2(44,-176),new Vector2(560,70),22,Theme.Paper);
            crew=Ui.Label(hud,"",new Vector2(44,-246),new Vector2(420,120),18,Theme.Muted);
            // Team money and quota (top right).
            var camp=Ui.MetalPanel(hud,"Campaign",Ui.TopRight,Ui.TopRight,new Vector2(-40,-34),new Vector2(330,142));
            money=Ui.Label(camp,"",new Vector2(20,-12),new Vector2(290,56),50,Theme.Amber);money.fontStyle=FontStyles.Bold;
            quotaText=Ui.Label(camp,"",new Vector2(20,-66),new Vector2(290,24),18,Theme.Paper);
            quotaBar=Ui.Bar(camp,"Quota",new Vector2(20,-92),new Vector2(290,14),Theme.Amber,1);
            cycleText=Ui.Label(camp,"",new Vector2(20,-112),new Vector2(290,22),15,Theme.Muted);
            // With a package countdown the line is longer than the panel: keep it on one line and shrink it to fit.
            cycleText.textWrappingMode=TextWrappingModes.NoWrap;cycleText.enableAutoSizing=true;cycleText.fontSizeMin=10;cycleText.fontSizeMax=15;
            // Oxygen: a vertical gauge on the right edge, shown only while the air is going down or refilling.
            var air=Ui.MetalPanel(hud,"Oxygen",new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-36,0),new Vector2(64,380));
            airGroup=air.gameObject.AddComponent<CanvasGroup>();airGroup.alpha=0;airGroup.blocksRaycasts=false;
            Ui.Icon(air,"air",new Vector2(0,-14),30,Theme.Teal,Ui.TopCenter).rectTransform.pivot=new Vector2(.5f,1);
            var tube=Ui.Rect("Tube",air,Ui.TopCenter,new Vector2(.5f,1),new Vector2(0,-52),new Vector2(26,270));Ui.Fill(tube,new Color(.02f,.05f,.055f,1));
            var level=Ui.Stretch("Level",tube,3);airFill=level.gameObject.AddComponent<Image>();airFill.sprite=Theme.Fill;airFill.type=Image.Type.Filled;airFill.fillMethod=Image.FillMethod.Vertical;airFill.fillOrigin=0;airFill.color=Theme.Teal;airFill.raycastTarget=false;
            for(int k=1;k<4;k++) {var tick=Ui.Rect("Tick",tube,new Vector2(0,k/4f),new Vector2(0,.5f),Vector2.zero,new Vector2(8,2));Ui.Fill(tick,new Color(.6f,.7f,.68f,.6f));}
            airText=Ui.Label(air,"",new Vector2(0,14),new Vector2(64,40),17,Theme.Paper,TextAlignmentOptions.Bottom,Ui.BottomCenter,Ui.BottomCenter);
            // Vitals and the belt slot (bottom left), after the reference HUD: two bars with icons in a riveted frame and the belt
            // item in its own box with a battery meter on top.
            var vitals=Ui.MetalPanel(hud,"Vitals",Ui.BottomLeft,Ui.BottomLeft,new Vector2(40,52),new Vector2(400,104));
            healthIcon=Ui.Icon(vitals,"heart",new Vector2(18,-18),30,new Color(.95f,.3f,.26f));
            health=Ui.Bar(vitals,"Health",new Vector2(58,-16),new Vector2(322,34),new Color(.86f,.2f,.18f),17);
            Ui.Icon(vitals,"run",new Vector2(18,-58),30,Theme.Teal);
            stamina=Ui.Bar(vitals,"Stamina",new Vector2(58,-56),new Vector2(322,30),new Color(.25f,.78f,.66f),15);
            var belt=Ui.MetalPanel(hud,"Belt slot",Ui.BottomLeft,Ui.BottomLeft,new Vector2(452,40),new Vector2(128,128));
            beltIcon=Ui.Icon(belt,"torch",new Vector2(0,6),98,new Color(.85f,.86f,.84f),Ui.Center);beltIcon.rectTransform.pivot=new Vector2(.5f,.5f);
            beltName=Ui.Label(belt,"",new Vector2(0,10),new Vector2(120,22),14,Theme.Muted,TextAlignmentOptions.Bottom,Ui.BottomCenter,Ui.BottomCenter);
            beltKey=Ui.Label(belt,"5",new Vector2(12,-8),new Vector2(30,22),16,Theme.Amber);
            beltBattery=Ui.MetalPanel(hud,"Battery",Ui.BottomLeft,Ui.BottomLeft,new Vector2(478,166),new Vector2(76,30),new Color(.02f,.03f,.02f,1));
            beltCells=new Image[5];
            for(int k=0;k<5;k++) {var cell=Ui.Rect("Cell",beltBattery,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(9+k*12,0),new Vector2(10,14));beltCells[k]=Ui.Fill(cell,Theme.Good);}
            var nub=Ui.Rect("Nub",beltBattery,new Vector2(1,.5f),new Vector2(0,.5f),new Vector2(-2,0),new Vector2(6,10));Ui.Fill(nub,new Color(.4f,.42f,.4f));
            // Inventory slots (bottom centre).
            for(int i=0;i<4;i++) {
                var root=Ui.MetalPanel(hud,"Slot "+(i+1),Ui.BottomCenter,Ui.Center,new Vector2(-230+i*196,52+56),new Vector2(186,112));
                var slot=new Slot {root=root,back=root.Find("Inset").GetComponent<Image>()};
                slot.edge=root.Find("Frame").GetComponent<Image>();
                // The item's rendered icon in a dark well on the left, its name and state on the right.
                slot.icon=ItemIcon(root,0,new Vector2(12,-24),74);slot.well=slot.icon.transform.parent.GetComponent<Image>();slot.icon.enabled=false;
                slot.key=Ui.Label(root,(i+1).ToString(),new Vector2(14,-6),new Vector2(40,22),15,Theme.Amber);
                slot.name=Ui.Label(root,"",new Vector2(92,-20),new Vector2(88,56),16,Theme.Paper,TextAlignmentOptions.TopLeft);
                slot.tag=Ui.Label(root,"",new Vector2(92,-74),new Vector2(90,32),11,Theme.Muted);
                slots.Add(slot);
            }
            hints=Ui.Label(hud,"",new Vector2(0,22),new Vector2(900,24),16,Theme.Muted,TextAlignmentOptions.Bottom,Ui.BottomCenter,Ui.BottomCenter);
            // Crosshair, interaction prompt, status text (centre).
            var dotRect=Ui.Rect("Dot",hud,Ui.Center,Ui.Center,Vector2.zero,new Vector2(6,6));dot=Ui.Fill(dotRect,new Color(.9f,.92f,.84f,.7f));
            var ringRect=Ui.Rect("Ring",hud,Ui.Center,Ui.Center,Vector2.zero,new Vector2(26,26));ring=ringRect.gameObject.AddComponent<Image>();ring.sprite=Theme.Frame;ring.type=Image.Type.Sliced;ring.color=new Color(.9f,.92f,.84f,.35f);ring.raycastTarget=false;
            focusGroup=Ui.Rect("Focus",hud,Ui.Center,new Vector2(.5f,1),new Vector2(0,-48),new Vector2(700,110));
            focusKey=Ui.Key(focusGroup,"E",new Vector2(8,-4),48);
            focusTitle=Ui.Label(focusGroup,"",new Vector2(86,-2),new Vector2(600,40),30,Theme.Paper);
            focusDetail=Ui.Label(focusGroup,"",new Vector2(86,-44),new Vector2(600,60),21,Theme.Muted);
            lowAir=Ui.Label(hud,"OKSIGEN MENIPIS! NAIK KE PERMUKAAN",Vector2.zero,new Vector2(1000,60),36,Theme.Red,TextAlignmentOptions.Center,Ui.Center,Ui.Center);lowAir.rectTransform.anchoredPosition=new Vector2(0,-230);
            spectator=Ui.Label(hud,"",new Vector2(0,-120),new Vector2(1100,80),30,Theme.Amber,TextAlignmentOptions.Top,Ui.TopCenter,Ui.TopCenter);
            voice=Ui.Label(hud,"",new Vector2(-48,28),new Vector2(520,36),24,Theme.Amber,TextAlignmentOptions.BottomRight,Ui.BottomRight,Ui.BottomRight);
        }
        void BuildToasts()
        {
            toastBox=Ui.Panel(canvas,"Notice",Ui.TopCenter,Ui.TopCenter,new Vector2(0,-36),new Vector2(860,64),Theme.Ink,Theme.Amber);
            toast=Ui.Label(toastBox,"",new Vector2(20,0),new Vector2(820,64),24,Theme.Amber,TextAlignmentOptions.MidlineLeft,new Vector2(0,.5f),new Vector2(0,.5f));
            toastGroup=toastBox.gameObject.AddComponent<CanvasGroup>();toastGroup.interactable=false;toastGroup.blocksRaycasts=false;toastBox.gameObject.SetActive(false);
            alert=Ui.Label(canvas,"",new Vector2(0,120),new Vector2(1500,100),64,Theme.Red,TextAlignmentOptions.Center,Ui.Center,Ui.Center);
        }
        public void Fade() {fadeAlpha=.85f;}
        // Big centred warning, used for the five-minute, two-minute and thirty-second calls.
        public void Alert(string text,Color color,float seconds=4) {alert.text=text;alertColor=color;alertUntil=Time.unscaledTime+seconds;}
        public void AlertLevel(int level) {Alert(level==1?"KAPAL MULAI TENGGELAM":level==2?"2 MENIT: KEMBALI KE KAPAL KECIL":"30 DETIK!",level==1?Theme.Amber:Theme.Red,level==3?5f:4f);}
        // ---- Frame update --------------------------------------------------------------------------------------------------------
        void Update()
        {
            float unscaled=Time.unscaledDeltaTime;fadeAlpha=Mathf.MoveTowards(fadeAlpha,0,unscaled*3);fade.color=new Color(0,0,0,fadeAlpha);
            var app=SinkApp.Instance;var s=NetSession.Instance;var p=NetPlayer.Local;
            menu.gameObject.SetActive(!app.Connected);hud.gameObject.SetActive(app.Connected);
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true) {if(dialog.gameObject.activeSelf)Close();else OpenSettings();}
            // Notice toasts and alerts animate every frame.
            if(app.notice!=lastNotice) {lastNotice=app.notice;toast.text=app.notice;toastUntil=Time.unscaledTime+5f;if(app.notice.Length>0)Sfx.Ui(Snd.UiOpen,.35f);}
            bool showToast=app.notice.Length>0&&Time.unscaledTime<toastUntil;toastAlpha=Mathf.MoveTowards(toastAlpha,showToast?1:0,unscaled*4);
            toastBox.gameObject.SetActive(toastAlpha>.01f);toastGroup.alpha=toastAlpha;
            alert.color=new Color(alertColor.r,alertColor.g,alertColor.b,Time.unscaledTime<alertUntil?Mathf.Clamp01((alertUntil-Time.unscaledTime)*2)*(.65f+.35f*Mathf.Sin(Time.unscaledTime*12)):0);
            if(!app.Connected||s==null||p==null||!p.IsSpawned)return;
            // Clients count their copy of the order timers down between campaign syncs (the host's copy is the real one).
            if(!s.IsServer&&s.Phase.Value!=GamePhase.Loading)foreach(var order in s.Campaign.orders)order.remaining-=unscaled;
            UpdateBars(s,p,unscaled);
            if(terminalOpen&&dialog.gameObject.activeSelf) {
                var keys=Keyboard.current;
                if(keys!=null&&(keys.leftArrowKey.wasPressedThisFrame||keys.aKey.wasPressedThisFrame))CycleFocus(-1);
                if(keys!=null&&(keys.rightArrowKey.wasPressedThisFrame||keys.dKey.wasPressedThisFrame))CycleFocus(1);
                UpdateTerminalMap(s);
                // Power-on: a bright line opens up, then the picture fades in with a slight phosphor flicker.
                float on=Time.unscaledTime-termOpenedAt;
                screenGroup.alpha=on<.22f?0:Mathf.Clamp01((on-.22f)/.25f)*(.95f+.05f*Mathf.PerlinNoise(Time.unscaledTime*9,0));
                if(termBoot!=null) {termBoot.gameObject.SetActive(on<.5f);termBoot.localScale=new Vector3(Mathf.Clamp01(on/.18f),1+Mathf.Clamp01((on-.18f)/.2f)*40,1);termBoot.GetComponent<Image>().color=new Color(.8f,1,.9f,1-Mathf.Clamp01((on-.2f)/.3f));}
                if(Time.unscaledTime>=nextTerminal) {nextTerminal=Time.unscaledTime+.25f;UpdateTerminal(s);}
            }
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;
            RefreshTexts(app,s,p);
        }
        void UpdateBars(NetSession s,NetPlayer p,float dt)
        {
            float tank=p.Inventory.All.Where(x=>x.definitionId==14).Sum(x=>x.oxygen);float air=p.Oxygen.Value+tank;float maxAir=SinkApp.Instance.balance.breath+(tank>0?90:0);
            health.Set(p.Health.Value/100f,Mathf.CeilToInt(p.Health.Value).ToString(),p.Health.Value<30);
            healthIcon.rectTransform.localScale=Vector3.one*(p.Health.Value<30?1+.12f*Mathf.Abs(Mathf.Sin(Time.unscaledTime*5)):1);
            stamina.Set(p.stamina/100f,"",false);
            // The oxygen gauge fades in while the air changes (falling underwater or refilling at the surface) and away once full.
            if(airShown<0)airShown=air;
            if(Mathf.Abs(air-airShown)>.05f||p.Underwater)airVisibleUntil=Time.unscaledTime+2.5f;airShown=air;
            bool showAir=p.Alive.Value&&Time.unscaledTime<airVisibleUntil;airGroup.alpha=Mathf.MoveTowards(airGroup.alpha,showAir?1:0,dt*3);
            float airFraction=air/Mathf.Max(1,maxAir);airFill.fillAmount=Mathf.MoveTowards(airFill.fillAmount,airFraction,dt*1.5f);
            airFill.color=air<10&&p.Underwater?Color.Lerp(Theme.Teal,Theme.Red,.5f+.5f*Mathf.Sin(Time.unscaledTime*9)):Theme.Teal;
            airText.text=Mathf.CeilToInt(air)+"\n<size=12>dtk</size>";
            lowAir.enabled=p.Alive.Value&&p.Underwater&&air<10&&Mathf.Sin(Time.unscaledTime*8)>-.3f;
            // Belt slot: its item and, for a torch, the battery meter (five cells, lit cells blink when nearly empty).
            var beltItem=p.Inventory.HasUtility?p.Inventory.utility:null;
            beltIcon.enabled=beltItem!=null;
            if(beltItem!=null) {
                // The rendered item icon (pixel-art fallback); a lit torch glows warm, everything else sits a little dimmer.
                var sprite=ModelLibrary.Icon(beltItem.definitionId);
                beltIcon.sprite=sprite!=null?sprite:Theme.Icon(beltItem.Definition.kind==ItemKind.Radio?"radio":"torch");
                beltIcon.color=p.Inventory.lightOn&&beltItem==p.Inventory.Torch?new Color(1,.96f,.86f):new Color(.72f,.74f,.72f);
            }
            beltName.text=beltItem!=null?beltItem.Definition.title.ToUpperInvariant():"SABUK KOSONG";
            var torch=p.Inventory.Torch;beltBattery.gameObject.SetActive(torch!=null);
            if(torch!=null) {
                int lit=Mathf.CeilToInt(torch.charge*5-.001f);bool blink=torch.charge<.2f&&Mathf.Sin(Time.unscaledTime*8)<0;
                for(int k=0;k<5;k++)beltCells[k].color=k<lit&&!blink?(torch.charge<.2f?Theme.Red:torch.charge<.45f?Theme.Amber:Theme.Good):new Color(.08f,.1f,.08f);
            }
            // Crosshair reacts to what is in reach.
            bool focused=p.Focus!=null&&p.Alive.Value&&!Modal;
            float ringSize=Mathf.Lerp(ring.rectTransform.sizeDelta.x,focused?40:26,dt*14);ring.rectTransform.sizeDelta=new Vector2(ringSize,ringSize);
            ring.color=Color.Lerp(ring.color,focused?new Color(Theme.Amber.r,Theme.Amber.g,Theme.Amber.b,.9f):new Color(.9f,.92f,.84f,.3f),dt*14);
            dot.enabled=p.Alive.Value&&!Modal;ring.enabled=dot.enabled;
            // The watch: 24-hour time of day from the shared clock; the colon blinks every second.
            float hour=s.Hour;int hh=Mathf.FloorToInt(hour)%24,mm=Mathf.FloorToInt((hour-Mathf.Floor(hour))*60);
            watchTime.text=hh.ToString("00")+(Mathf.Repeat(Time.unscaledTime,1)<.5f?":":" ")+mm.ToString("00");
            watchTime.color=s.InExpedition&&s.Remaining<=30?Color.Lerp(LcdInk,Theme.Red,.5f+.5f*Mathf.Sin(Time.unscaledTime*9)):LcdInk;
            hud.gameObject.SetActive(!Cinematic.Playing);
        }
        void RefreshTexts(SinkApp app,NetSession s,NetPlayer p)
        {
            string phase=s.Phase.Value.ToString();if(phase!=lastPhase) {lastPhase=phase;if(s.Phase.Value==GamePhase.Loading)Close();if(s.Phase.Value==GamePhase.Harbor||s.Phase.Value==GamePhase.Ready)MaybeShowGuide();}
            if(s.LastResult.Length>0&&s.LastResult!=shownResult) {shownResult=s.LastResult;OpenResults();}
            var c=s.Campaign;
            watchCaption.text=s.Phase.Value==GamePhase.Loading?"JAM  •  BERLAYAR":s.InExpedition?"JAM  •  KAPAL KARGO":s.Phase.Value==GamePhase.Harbor?"JAM  •  PULAU":"JAM  •  LAUT";
            departLabel.text=s.Phase.Value==GamePhase.Departing?"BERANGKAT  "+Mathf.CeilToInt((float)(s.DepartureAt.Value-app.TimeNow))+" DTK":"";
            objective.text="<color=#F7AA3F>▸</color> "+Objective(s,p);
            money.text=ItemCatalog.Money(c.credits);
            quotaText.text="KUOTA  "+ItemCatalog.Money(c.sold)+" / "+ItemCatalog.Money(c.quota);
            quotaBar.Set((float)c.sold/Mathf.Max(1,c.quota),"",false);
            cycleText.text="SIKLUS "+c.cycle+"  •  HARI "+new string('●',Mathf.Clamp(c.expeditionsUsed,0,3))+new string('○',3-Mathf.Clamp(c.expeditionsUsed,0,3))+(c.orders.Count>0?"  •  <color=#F7AA3F>PAKET "+Mathf.CeilToInt(c.orders.Min(o=>o.remaining))+" DTK</color>":"")+(s.NoMonsters.Value?"  <color=#E64033>TES</color>":"");
            crew.text=NetPlayer.All.Count<2?"":string.Join("\n",NetPlayer.All.Select(x=>(x.Alive.Value?"<color=#84D378>●</color> ":"<color=#E64033>×</color> ")+x.Name.Value.ToString()+(x.IsOwner?"  <color=#8AA79F>(kamu)</color>":"")));
            for(int i=0;i<slots.Count;i++)RefreshSlot(slots[i],i<p.Inventory.items.Count?p.Inventory.items[i]:null,i==p.Inventory.selected&&p.Inventory.items.Count>0,p);
            var selected=p.Selected;
            string light=p.Inventory.Torch!=null?"   [F] senter "+(p.Inventory.lightOn?"mati":"nyala"):"";
            hints.text=!p.Alive.Value?"":(selected==null?"[1–4] slot   [5] sabuk":selected.Definition.kind switch {
                ItemKind.Flare=>"[KLIK KIRI] flare: alihkan monster",
                ItemKind.Scrap=>p.OnBoat?"[G] taruh di kapal":"[G] jatuhkan",
                ItemKind.Radio=>"[R] radio   [5] ke sabuk",
                ItemKind.Flashlight=>"[5] ke sabuk",
                ItemKind.Battery=>"[KLIK KIRI] pasang baterai",
                ItemKind.Key=>"[E] buka pintu terkunci",
                _=>"[G] jatuhkan"})+light;
            // Interaction prompt.
            bool show=p.Alive.Value&&!Modal&&(p.climbing||p.Focus!=null);focusGroup.gameObject.SetActive(show);
            if(show) {
                if(p.climbing) {focusKey.label.text="W/S";focusTitle.text="Memanjat tali";focusDetail.text="Space untuk melepas";}
                else {
                    var f=p.Focus;focusKey.label.text="E";focusTitle.text=f.title;
                    focusDetail.text=f.item!=null&&f.item.Item.Definition.kind==ItemKind.Package?(f.item.Falling?"Sedang turun dengan parasut...":"<color=#F7AA3F>Buka paket</color>: "+string.Join(", ",f.item.Item.contents.Select(i=>ItemCatalog.Get(i)?.title)))
                        :f.item!=null?(f.item.Item.Definition.kind==ItemKind.Scrap?ItemCatalog.Money(f.item.Item.Definition.value)+"   •   ":"")+f.item.Item.Definition.weight+" kg"+(f.item.Item.Definition.twoHands?"   •   DUA TANGAN":"")+(s.Campaign.cargo.Any(x=>x.uid==f.item.Uid)?"   •   <color=#84D378>DI KAPAL</color>":"")
                        :f.action==ActionKind.Rack?(selected!=null&&selected.Definition.kind!=ItemKind.Scrap?"Taruh "+selected.Definition.title+" di rak":"Pegang alat untuk menaruhnya di rak")
                        :f.action==ActionKind.Terminal?"Toko • peta kapal • level air • kontrol bahaya"
                        :f.action==ActionKind.Sell?"Jual scrap dari kapal kecil"
                        :f.action==ActionKind.Door?(f.title=="Pintu terkunci"?(p.Inventory.items.Any(i=>i.Definition.kind==ItemKind.Key)?"<color=#F7AA3F>Pakai kunci</color> untuk membuka":"<color=#E64033>Butuh kunci</color> • cari di dalam kapal"):"Pintu kapal")
                        :f.action==ActionKind.Portal?(p.Zone.Value==PlayerZone.Interior?"Kembali ke geladak kapal kargo":"Masuk ke dalam kapal kargo"):"";
                }
            }
            spectator.text=p.Alive.Value?"":"KAMU JADI SPECTATOR\n<size=22><color=#8AA79F>← / → pilih rekan   •   V bicara dengan sesama spectator</color></size>";
            var comms=app.GetComponent<VoiceComms>();voice.text=comms.transmitting.Length>0?"● "+comms.transmitting:comms.ready?"V SUARA   R RADIO":"";
        }
        void RefreshSlot(Slot slot,ItemInstance item,bool selected,NetPlayer p)
        {
            slot.edge.color=selected?Theme.Amber:Color.white;slot.back.color=selected?new Color(.12f,.13f,.09f,.95f):new Color(.035f,.04f,.04f,.92f);slot.key.color=selected?Theme.Amber:Theme.Muted;
            slot.well.color=selected?new Color(.95f,.66f,.25f,.16f):new Color(0,0,0,.35f);
            if(item==null) {slot.icon.enabled=false;slot.name.text="<color=#547A70>KOSONG</color>";slot.tag.text="";return;}
            var d=item.Definition;var sprite=ModelLibrary.Icon(d.id);
            slot.icon.enabled=true;slot.icon.sprite=sprite!=null?sprite:Theme.Fill;slot.icon.color=sprite!=null?Color.white:d.color*1.25f;
            slot.name.text=d.title.ToUpperInvariant();slot.name.color=selected?Theme.Amber:Theme.Paper;
            slot.tag.text=d.twoHands?"DUA TANGAN • "+d.weight+" kg":d.kind switch {
                ItemKind.Flashlight=>(p.Inventory.lightOn&&p.Inventory.Torch==item?"NYALA":"MATI")+" • "+(item.charge<=0?"<color=#E64033>HABIS</color>":item.charge<.2f?"<color=#E64033>"+Mathf.CeilToInt(item.charge*100)+"%</color>":Mathf.CeilToInt(item.charge*100)+"%"),
                ItemKind.Oxygen=>Mathf.CeilToInt(item.oxygen)+" dtk tersisa",
                ItemKind.Scrap=>ItemCatalog.Money(d.value)+" • "+d.weight+" kg",
                ItemKind.Flare=>"SEKALI PAKAI",
                ItemKind.Battery=>"ISI SENTER",
                ItemKind.Key=>"BUKA 1 PINTU",
                _=>"RADIO"};
        }
        // An item's rendered icon in a dark well (hand slots, shop rows, sale list). Falls back to the item's colour.
        static Image ItemIcon(Transform parent,int id,Vector2 pos,float size,Vector2? anchor=null,Color? well=null)
        {
            var a=anchor??Ui.TopLeft;var back=Ui.Rect("Icon well",parent,a,a,pos,new Vector2(size,size));Ui.Fill(back,well??new Color(0,0,0,.4f));
            var r=Ui.Stretch("Icon",back,2);var image=r.gameObject.AddComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;
            var sprite=ModelLibrary.Icon(id);if(sprite!=null)image.sprite=sprite;else {image.sprite=Theme.Fill;image.color=ItemCatalog.Get(id)?.color??Color.grey;}
            return image;
        }
        string Objective(NetSession s,NetPlayer p)
        {
            var c=s.Campaign;bool carryingScrap=p.Inventory.items.Any(x=>x.Definition.kind==ItemKind.Scrap);
            if(!p.Alive.Value)return "Tunggu hingga kapal kecil berangkat.";
            bool package=c.cargo.Any(x=>x.Definition.kind==ItemKind.Package);
            switch(s.Phase.Value) {
                case GamePhase.Loading: return "Mesin menyala. Kapal sedang berlayar...";
                case GamePhase.Departing: return "<color=#E64033>Segera naik ke kapal kecil!</color>";
                case GamePhase.Expedition:
                    if(s.Remaining<120)return "<color=#E64033>KEMBALI KE KAPAL KECIL SEKARANG!</color>";
                    return p.Zone.Value==PlayerZone.Boat?"Panjat tali menuju kapal kargo (W / S).":p.Zone.Value==PlayerZone.Exterior?"Masuk lewat PALKA A, B atau C di dek.":carryingScrap?"Bawa scrap ke kapal kecil dan taruh (G) di dek.":"Cari scrap berharga. Awasi air dan monster.";
                case GamePhase.Harbor:
                    if(c.expeditionsUsed>=3&&c.cargo.Any(x=>x.Definition.kind==ItemKind.Scrap))return "Jual scrap ke PEDAGANG SCRAP di pulau.";
                    if(c.expeditionsUsed>=3)return "Selesaikan KUOTA di terminal pulau.";
                    return "Pakai KEMUDI untuk kembali ke kapal kargo.";
                default:
                    if(package)return "Paket pasokan sudah tiba: tekan E pada paket.";
                    if(c.expeditionsUsed>=3)return "Pakai KEMUDI untuk berlayar ke pulau.";
                    if(!p.Inventory.All.Any()&&c.orders.Count==0&&c.credits>=15)return "Pesan alat di KOMPUTER KAPAL.";
                    return "Pakai KEMUDI untuk berangkat ke kapal kargo.";
            }
        }
        // ---- Menus ---------------------------------------------------------------------------------------------------------------
        void BuildMenu()
        {
            Ui.Clear(menu);menuMode="main";var app=SinkApp.Instance;
            Ui.Label(menu,"DERMAGA 04   /   CO-OP SALVAGE",new Vector2(40,-34),new Vector2(620,30),20,Theme.Amber);
            var logo=Ui.Label(menu,"SINK\nSCRAPT",new Vector2(34,-74),new Vector2(640,260),104,Theme.Paper);logo.lineSpacing=-22;logo.gameObject.AddComponent<UiGlitch>();
            Ui.Label(menu,"Bawa pulang yang berharga.\nSebelum laut mengambil semuanya.",new Vector2(40,-336),new Vector2(620,70),24,Theme.Muted);
            Ui.Label(menu,"NAMA KRU",new Vector2(40,-404),new Vector2(300,22),16,Theme.Muted);
            var name=Ui.Input(menu,app.playerName,"Nama kru",new Vector2(40,-428),new Vector2(620,54));name.onValueChanged.AddListener(v=>app.playerName=v);
            Ui.Button(menu,"MAIN SOLO",new Vector2(40,-496),new Vector2(620,60),()=>app.StartLocal(true),true,true,"1 pemain");
            Ui.Button(menu,"BUAT PERMAINAN ONLINE",new Vector2(40,-562),new Vector2(620,56),async()=>await app.StartOnline(true,""),true,false,"privat");
            Ui.Button(menu,"GABUNG DENGAN KODE",new Vector2(40,-624),new Vector2(620,56),()=>ConnectionMenu(false));
            Ui.Button(menu,"JARINGAN LOKAL",new Vector2(40,-686),new Vector2(620,56),()=>ConnectionMenu(true),true,false,"UDP 7777");
            Ui.Button(menu,"PENGATURAN",new Vector2(40,-750),new Vector2(300,52),OpenSettings,true,false,"",22);
            Ui.Button(menu,"PANDUAN",new Vector2(360,-750),new Vector2(300,52),()=>OpenGuide(),true,false,"",22);
            Ui.Button(menu,"KAMPANYE: "+(app.newCampaign?"BARU":"LANJUT"),new Vector2(40,-810),new Vector2(300,52),()=>{app.newCampaign=!app.newCampaign;BuildMenu();},true,false,"",21);
            // Test mode: the host starts expeditions without any monster.
            Ui.Button(menu,"MONSTER: "+(app.noMonsters?"MATI (TES)":"AKTIF"),new Vector2(360,-810),new Vector2(300,52),()=>{app.SetNoMonsters(!app.noMonsters);BuildMenu();},true,app.noMonsters,"",21);
            Ui.Button(menu,"KELUAR",new Vector2(40,-870),new Vector2(620,48),()=>{if(Application.isEditor)app.notice="Hentikan Play Mode untuk keluar dari Editor.";else Application.Quit();},true,false,"",21);
            Ui.Label(menu,"1–4 KRU  •  "+Mathf.RoundToInt(app.balance.duration/60)+" MENIT  •  3 EKSPEDISI PER KUOTA\nAlpha 0.3  •  Windows  •  Bahasa Indonesia",new Vector2(40,-930),new Vector2(620,50),16,Theme.Muted);
        }
        void ConnectionMenu(bool local)
        {
            Ui.Clear(menu);menuMode="connect";var app=SinkApp.Instance;
            Ui.Label(menu,local?"JARINGAN LOKAL":"GABUNG PERMAINAN",new Vector2(40,-44),new Vector2(620,70),46,Theme.Amber);
            Ui.Label(menu,local?"Masukkan alamat komputer host.\nSemua pemain harus memakai build yang sama.":"Minta kode permainan dari temanmu\n(kode privat dibuat saat host mulai online).",new Vector2(40,-150),new Vector2(620,100),24,Theme.Muted);
            Ui.Label(menu,local?"ALAMAT HOST":"KODE PERMAINAN",new Vector2(40,-306),new Vector2(300,22),16,Theme.Muted);
            var field=Ui.Input(menu,local?"127.0.0.1":"",local?"Alamat host":"Kode permainan",new Vector2(40,-332),new Vector2(620,62));
            Ui.Button(menu,"GABUNG",new Vector2(40,-420),new Vector2(620,62),async()=>{if(local)app.StartLocal(false,field.text);else await app.StartOnline(false,field.text);},true,true);
            if(local)Ui.Button(menu,"BUAT HOST LOKAL",new Vector2(40,-496),new Vector2(620,62),()=>app.StartLocal(true));
            Ui.Button(menu,"KEMBALI",new Vector2(40,-840),new Vector2(620,58),BuildMenu);
        }
        // ---- Dialogs -------------------------------------------------------------------------------------------------------------
        public void Close() {dialog.gameObject.SetActive(false);terminalOpen=false;}
        public void Open(ActionKind action) {mode=action;salePage=0;saleSelection.Clear();if(action==ActionKind.Terminal&&!terminalOpen)termTab=-1;dialog.gameObject.SetActive(true);Sfx.Ui(Snd.UiOpen,.6f);BuildDialog();}
        void Header(string title,string detail,string closeText="TUTUP  /  ESC")
        {
            Ui.Clear(card);terminalOpen=false;monitor.gameObject.SetActive(false);card.gameObject.SetActive(true);
            Ui.Label(card,title,new Vector2(44,-34),new Vector2(850,56),40,Theme.Amber);
            Ui.Label(card,detail,new Vector2(44,-92),new Vector2(850,70),22,Theme.Muted);
            var line=Ui.Rect("Rule",card,Ui.TopLeft,Ui.TopLeft,new Vector2(44,-170),new Vector2(852,3));Ui.Fill(line,Theme.Line);
            Ui.Button(card,closeText,new Vector2(44,-928),new Vector2(852,52),Close,true,false,"",22);
        }
        TMP_Text Row(string text,Vector2 pos,Vector2 size,float fontSize=24,Color? color=null) => Ui.Label(card,text,pos,size,fontSize,color??Theme.Paper);
        void BuildDialog()
        {
            var s=NetSession.Instance;var p=NetPlayer.Local;if(s==null||p==null)return;
            if(mode==ActionKind.Depart) BuildHelm(s,p);
            else if(mode==ActionKind.Sell) BuildSale(s,p);
            else if(mode==ActionKind.Terminal) BuildTerminal(s,p);
            else if(mode==ActionKind.ResolveQuota) BuildQuota(s,p);
        }
        void BuildHelm(NetSession s,NetPlayer p)
        {
            Header("KEMUDI",s.InExpedition?"Kru yang tidak kembali ke kapal kecil akan hilang. Jika tidak ada kru di kapal, semua muatan kapal ikut hilang.":"Pilih tujuan. Perjalanan berlangsung otomatis.");
            if(s.InExpedition) {
                bool departing=s.Phase.Value==GamePhase.Departing;
                Ui.Button(card,departing?"BERANGKAT SEDANG BERJALAN":"BERANGKAT DALAM 5 DETIK",new Vector2(44,-210),new Vector2(852,76),()=>{p.Act(ActionKind.Depart);Close();},!departing,true,"tinggalkan kapal kargo");
                Ui.Button(card,"BATALKAN KEBERANGKATAN",new Vector2(44,-300),new Vector2(852,70),()=>{p.Act(ActionKind.CancelDeparture);Close();},departing);
                Row("Pastikan semua kru sudah naik ke kapal kecil.\nBarang yang masih kamu bawa ikut pulang; barang di bangkai kapal hilang.",new Vector2(44,-400),new Vector2(852,100),22,Theme.Muted);
            } else {
                int used=s.Campaign.expeditionsUsed;bool island=WorldZone.Island!=null;
                Ui.Button(card,"KAPAL KARGO S-04",new Vector2(44,-210),new Vector2(852,96),()=>{p.Act(ActionKind.TravelShip);Close();},used<3,true,"hari "+Mathf.Min(3,used+1)+" / 3");
                Row(used<3?Mathf.RoundToInt(SinkApp.Instance.balance.duration/60)+" menit. Kapal mulai tenggelam pada menit "+Mathf.RoundToInt(ShipTimeline.At(ShipTimeline.SinkStart,SinkApp.Instance.balance.duration)/60)+". Interior tiga lantai dan letak pintunya berubah setiap ekspedisi."+(s.NoMonsters.Value?" Mode tanpa monster.":""):"Tiga hari sudah terpakai. Jual scrap dan selesaikan kuota di pulau.",new Vector2(64,-312),new Vector2(812,60),20,Theme.Muted);
                Ui.Button(card,"PULAU DERMAGA 04",new Vector2(44,-400),new Vector2(852,86),()=>{p.Act(ActionKind.TravelIsland);Close();},used>=3&&!island,used>=3&&!island,island?"sudah di pulau":used>=3?"jual • kuota":"setelah hari 3");
                Row("Pulau hanya dikunjungi untuk menutup siklus: jual scrap ke pedagang dan evaluasi kuota. Alat dipesan lewat KOMPUTER KAPAL.",new Vector2(64,-496),new Vector2(812,60),20,Theme.Muted);
            }
        }
        static string ToolInfo(int id) => id switch {
            12=>"Menerangi lorong gelap. [F] nyala / mati. Datang dengan baterai penuh.",
            16=>"Isi ulang senter: sekitar "+Mathf.RoundToInt(SinkApp.Instance.balance.flashlightBattery)+" detik cahaya.",
            13=>"Bicara lintas lokasi dengan tombol R.",
            14=>"Tambah 90 detik napas di bawah air.",
            _=>"Sekali pakai. Mengalihkan monster 20 detik."};
        void BuildSale(NetSession s,NetPlayer p)
        {
            Header("PEDAGANG SCRAP","Pilih scrap dari kapal kecil yang akan dijual. Penjualan menambah dompet tim dan progres kuota.");
            var c=s.Campaign;var cargo=c.cargo.Where(x=>x.Definition.kind==ItemKind.Scrap).ToArray();const int perPage=6;
            salePage=Mathf.Clamp(salePage,0,Mathf.Max(0,(cargo.Length-1)/perPage));
            if(cargo.Length==0)Row("Belum ada scrap di kapal kecil.\nBawa scrap dari bangkai kapal dan taruh di dek kapal kecil.",new Vector2(44,-210),new Vector2(852,120),24,Theme.Muted);
            for(int n=0;n<perPage;n++) {
                int index=salePage*perPage+n;if(index>=cargo.Length)break;var item=cargo[index];bool on=saleSelection.Contains(item.uid);
                var line=Ui.Button(card,(on?"[X]  ":"[  ]  ")+item.Definition.title,new Vector2(44,-192-n*66),new Vector2(852,60),()=>{if(!saleSelection.Add(item.uid))saleSelection.Remove(item.uid);BuildDialog();},true,on,ItemCatalog.Money(item.Definition.value));
                // The scrap's picture before its name.
                ItemIcon(line.transform,item.definitionId,new Vector2(16,-5),50);
                var text=(RectTransform)line.transform.Find("Text");text.anchoredPosition+=new Vector2(58,0);text.sizeDelta-=new Vector2(58,0);
            }
            Ui.Button(card,"PILIH SEMUA",new Vector2(44,-596),new Vector2(250,50),()=>{foreach(var item in cargo)saleSelection.Add(item.uid);BuildDialog();},cargo.Length>0,false,"",20);
            Ui.Button(card,"←",new Vector2(310,-596),new Vector2(90,50),()=>{salePage--;BuildDialog();},salePage>0);
            Ui.Button(card,"→",new Vector2(416,-596),new Vector2(90,50),()=>{salePage++;BuildDialog();},(salePage+1)*perPage<cargo.Length);
            Row("Halaman "+(salePage+1)+" / "+Mathf.Max(1,(cargo.Length+perPage-1)/perPage),new Vector2(530,-606),new Vector2(300,34),20,Theme.Muted);
            int total=cargo.Where(x=>saleSelection.Contains(x.uid)).Sum(x=>x.Definition.value);int count=cargo.Count(x=>saleSelection.Contains(x.uid));
            int counted=Mathf.Clamp(c.quota-c.sold,0,total);
            var summary=Ui.Panel(card,"Summary",Ui.TopLeft,Ui.TopLeft,new Vector2(44,-664),new Vector2(852,156),Theme.Panel);
            Ui.Label(summary,"DIPILIH  "+count+" barang    NILAI  <color=#F7AA3F>+"+ItemCatalog.Money(total)+"</color>",new Vector2(20,-14),new Vector2(812,32),24,Theme.Paper);
            Ui.Label(summary,"DOMPET TIM   "+ItemCatalog.Money(c.credits)+"  →  <color=#84D378>"+ItemCatalog.Money(c.credits+total)+"</color>",new Vector2(20,-50),new Vector2(812,32),24,Theme.Paper);
            Ui.Label(summary,"KUOTA   "+ItemCatalog.Money(c.sold)+"  →  <color=#84D378>"+ItemCatalog.Money(c.sold+counted)+"</color> / "+ItemCatalog.Money(c.quota)+(total>counted?"   <size=18><color=#8AA79F>(kelebihan "+ItemCatalog.Money(total-counted)+" tidak dihitung)</color></size>":""),new Vector2(20,-86),new Vector2(812,32),24,Theme.Paper);
            var bar=Ui.Bar(summary,"Projected",new Vector2(20,-120),new Vector2(812,24),Theme.Teal,16);bar.Set((float)(c.sold+counted)/Mathf.Max(1,c.quota),"",false,true);
            Ui.Button(card,"KONFIRMASI PENJUALAN  /  "+ItemCatalog.Money(total),new Vector2(44,-836),new Vector2(852,70),()=>{p.Act(ActionKind.Sell,0,string.Join(",",saleSelection));Close();},total>0,true);
        }
        // ---- Boat computer ---------------------------------------------------------------------------------------------------
        // An old CRT on the cabin desk. The left of the screen is a live map that follows one crew member through the wreck:
        // it shows the deck they are on, centred on them, with walls, doors (locked ones in red), stairs, hazards and entries.
        // ← / → (or the arrows on screen) switch who is followed; solo, it follows you. The right side shows that diver's state,
        // the water level from the shared clock, and hazard control. Markers move every frame; the deck drawing is rebuilt only
        // when the followed diver changes deck, and the panel when a hazard, door or the crew changes.
        static readonly Color[] CrewColors={new(.95f,.6f,.25f),new(.3f,.8f,.8f),new(.85f,.5f,.45f),new(.75f,.8f,.45f)};
        static readonly Color Phos=new(.46f,1f,.70f),PhosDim=new(.24f,.58f,.42f),PhosFaint=new(.09f,.26f,.19f),PhosFloor=new(.05f,.15f,.11f),ScreenBlack=new(.012f,.035f,.028f,1);
        const float MapCell=54,MapMargin=90,ViewW=860,ViewH=690;
        RectTransform monitor,screen,mapView,mapContent,termFocusMarker,termFocusHeading,termBoot;
        CanvasGroup screenGroup;
        readonly List<(RectTransform dot,NetPlayer player)> termCrew=new();
        readonly List<TMP_Text> termHazards=new();
        TMP_Text termInfo,termFocusName,termFocusState,termHeader,termMapNote,termDeckTabs;
        Image termWater;
        bool terminalOpen;
        // Boat computer tab (0 map, 1 shop; -1 picks one on opening) and the package being filled in the shop.
        int termTab=-1;
        readonly List<int> cart=new();
        TMP_Text termOrders;
        public bool TerminalMapShowing=>terminalOpen&&dialog.gameObject.activeSelf&&mapContent!=null&&mapContent.childCount>0;
        string termSignature="";
        float nextTerminal,termOpenedAt;
        int termDeck=-1;
        ulong termFocus=ulong.MaxValue;
        void BuildMonitorFrame()
        {
            monitor=Ui.Rect("Monitor",dialog,Ui.Center,Ui.Center,Vector2.zero,new Vector2(1580,960));
            // Plastic bezel: dark casing, a raised inner lip, a brand plate and a power lamp.
            Ui.Fill(monitor,new Color(.085f,.095f,.088f,1),true);
            var lip=Ui.Stretch("Lip",monitor,18);Ui.Fill(lip,new Color(.14f,.155f,.145f,1));
            var lipFrame=Ui.Stretch("Lip frame",monitor,18).gameObject.AddComponent<Image>();lipFrame.sprite=Theme.Frame;lipFrame.type=Image.Type.Sliced;lipFrame.color=new Color(.24f,.27f,.25f);lipFrame.raycastTarget=false;
            Ui.Label(monitor,"SEAWARD  S-04",new Vector2(0,10),new Vector2(400,26),18,new Color(.42f,.46f,.43f),TextAlignmentOptions.Center,Ui.BottomCenter,Ui.BottomCenter);
            var led=Ui.Rect("Power",monitor,Ui.BottomRight,Ui.BottomRight,new Vector2(-60,14),new Vector2(14,14));Ui.Fill(led,new Color(.4f,1,.45f));
            Ui.Label(monitor,"DAYA",new Vector2(-82,10),new Vector2(80,22),15,new Color(.42f,.46f,.43f),TextAlignmentOptions.Right,Ui.BottomRight,Ui.BottomRight);
            screen=Ui.Stretch("Screen",monitor,46);screen.offsetMin=new Vector2(46,50);Ui.Fill(screen,ScreenBlack);
            screen.gameObject.AddComponent<RectMask2D>();screenGroup=screen.gameObject.AddComponent<CanvasGroup>();
            monitor.gameObject.SetActive(false);
        }
        // Overlays drawn above the screen content: scanlines, edge falloff and the power-on line.
        void ScreenOverlays()
        {
            var lines=Ui.Stretch("Scanlines",screen).gameObject.AddComponent<Image>();lines.sprite=Theme.Scanlines;lines.type=Image.Type.Tiled;lines.color=new Color(0,0,0,.55f);lines.raycastTarget=false;
            var shade=Ui.Stretch("Vignette",screen).gameObject.AddComponent<Image>();shade.sprite=Theme.Vignette;shade.color=new Color(0,0,0,.85f);shade.raycastTarget=false;
            termBoot=Ui.Rect("Boot line",screen,Ui.Center,Ui.Center,Vector2.zero,new Vector2(1480,6));Ui.Fill(termBoot,new Color(.8f,1,.9f,1));
        }
        TMP_Text Glow(Transform parent,string text,Vector2 pos,Vector2 size,float fontSize,Color color,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
        {
            var label=Ui.Label(parent,text,pos,size,fontSize,color,align);label.fontStyle=FontStyles.Bold;return label;
        }
        Button ScreenButton(Transform parent,string text,Vector2 pos,Vector2 size,Action click,bool enabled=true,float fontSize=20)
        {
            var r=Ui.Rect("Key "+text,parent,Ui.TopLeft,Ui.TopLeft,pos,size);var back=Ui.Fill(r,new Color(.05f,.16f,.11f,1),true);
            var edge=Ui.Stretch("Frame",r).gameObject.AddComponent<Image>();edge.sprite=Theme.Frame;edge.type=Image.Type.Sliced;edge.color=enabled?Phos:PhosFaint;edge.raycastTarget=false;
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=back;button.transition=Selectable.Transition.None;button.interactable=enabled;button.onClick.AddListener(()=>click());
            var fx=r.gameObject.AddComponent<UiFeedback>();fx.fill=back;fx.normal=new Color(.05f,.16f,.11f,1);fx.hover=new Color(.16f,.42f,.28f,1);fx.selectable=button;
            Glow(r,text,Vector2.zero,size,fontSize,enabled?Phos:PhosDim,TextAlignmentOptions.Center);return button;
        }
        // Crew in a stable order (by client id), and the one the map follows.
        static List<NetPlayer> Crew() => NetPlayer.All.Where(x=>x!=null&&x.IsSpawned).OrderBy(x=>x.OwnerClientId).ToList();
        NetPlayer FocusedCrew()
        {
            var crew=Crew();if(crew.Count==0)return null;
            var current=crew.Find(x=>x.OwnerClientId==termFocus);if(current!=null)return current;
            // First open: follow someone inside the wreck if anyone is, otherwise yourself.
            var pick=crew.Find(x=>x.Alive.Value&&x.Zone.Value==PlayerZone.Interior&&!x.IsOwner)??crew.Find(x=>x.IsOwner)??crew[0];
            termFocus=pick.OwnerClientId;return pick;
        }
        void CycleFocus(int step)
        {
            var crew=Crew();if(crew.Count<2)return;var current=FocusedCrew();int i=Mathf.Max(0,crew.IndexOf(current));
            termFocus=crew[((i+step)%crew.Count+crew.Count)%crew.Count].OwnerClientId;Sfx.Ui(Snd.UiClick,.6f);BuildDialog();
        }
        static int DeckOf(Vector3 world) => Mathf.Clamp(Mathf.RoundToInt((world.y-WorldZone.InteriorOffset.y-ShipTimeline.FloorHeight(0))/3f),0,ShipGrid.Decks-1);
        // Interior position -> pixel position on the map content (+z, the bow, at the top).
        static Vector2 MapPoint(Vector3 world)
        {
            var local=world-WorldZone.InteriorOffset;
            float col=local.x/ShipGrid.Cell+(ShipGrid.Columns-1)*.5f,row=local.z/ShipGrid.Cell+(ShipGrid.Rows-1)*.5f;
            return new Vector2(MapMargin+(col+.5f)*MapCell,-MapMargin-(ShipGrid.Rows-1-row+.5f)*MapCell);
        }
        static Vector2 CellPoint(int x,int z) => new(MapMargin+(x+.5f)*MapCell,-MapMargin-(ShipGrid.Rows-1-z+.5f)*MapCell);
        string TerminalSignature(NetSession s)
        {
            if(termTab==1) {var p=NetPlayer.Local;return "shop|"+s.Campaign.credits+"|"+s.Campaign.orders.Count+"|"+(p!=null&&p.Inventory.All.Any(i=>i.definitionId==14&&i.oxygen<90));}
            var wreck=WorldZone.Interior;if(!s.InExpedition||wreck==null||wreck.layout==null)return "offline";
            var now=SinkApp.Instance.TimeNow;var sig=new System.Text.StringBuilder("on");
            for(int i=0;i<wreck.hazards.Count;i++)sig.Append(s.HazardSuppressed(i)?'m':now<s.HazardReadyAt(i)?'j':'a');
            sig.Append('|');for(int i=0;i<wreck.layout.doors.Count;i++)if(wreck.layout.doors[i].locked)sig.Append(s.DoorLockedNow(i)?'L':'u');
            sig.Append('|').Append(string.Join(",",Crew().Select(x=>x.OwnerClientId))).Append('|').Append(termFocus);
            return sig.ToString();
        }
        void BuildTerminal(NetSession s,NetPlayer p)
        {
            card.gameObject.SetActive(false);monitor.gameObject.SetActive(true);Ui.Clear(screen);
            termCrew.Clear();termHazards.Clear();termInfo=null;termWater=null;termFocusMarker=null;termDeck=-1;mapContent=null;termOrders=null;
            if(!terminalOpen)termOpenedAt=Time.unscaledTime;terminalOpen=true;
            var wreck=WorldZone.Interior;bool online=s.InExpedition&&wreck!=null&&wreck.layout!=null;
            if(termTab<0)termTab=s.InExpedition?0:1;
            var focus=FocusedCrew();termSignature=TerminalSignature(s);
            termHeader=Glow(screen,"",new Vector2(26,-16),new Vector2(1040,30),22,Phos);
            var rule=Ui.Rect("Rule",screen,Ui.TopLeft,Ui.TopLeft,new Vector2(26,-52),new Vector2(1430,2));Ui.Fill(rule,PhosDim);
            ScreenButton(screen,termTab==0?"[ PETA ]":"PETA",new Vector2(1080,-8),new Vector2(180,38),()=>{termTab=0;BuildDialog();},true,19);
            ScreenButton(screen,termTab==1?"[ TOKO ]":"TOKO",new Vector2(1274,-8),new Vector2(180,38),()=>{termTab=1;BuildDialog();},true,19);
            ScreenButton(screen,"[ESC]  KELUAR",new Vector2(1240,-806),new Vector2(214,40),Close,true,19);
            if(termTab==1) {BuildTerminalShop(s,p);ScreenOverlays();UpdateTerminalTexts(s);return;}
            Glow(screen,"[← / →] GANTI KRU YANG DIPANTAU",new Vector2(26,-812),new Vector2(700,30),18,PhosDim);
            if(!online) {
                Glow(screen,"> TIDAK ADA SINYAL\n\n> Komputer terhubung ke kapal kargo selama ekspedisi.\n> Berangkat dengan KEMUDI. Setelah tiba, layar ini menunjukkan\n  peta dek tempat kru berada, level air, dan kontrol bahaya.\n\n_",new Vector2(40,-100),new Vector2(1380,600),28,Phos);
                ScreenOverlays();UpdateTerminalTexts(s);return;
            }
            // Map viewport.
            mapView=Ui.Rect("Map",screen,Ui.TopLeft,Ui.TopLeft,new Vector2(26,-70),new Vector2(ViewW,ViewH));Ui.Fill(mapView,new Color(.008f,.022f,.018f,1));mapView.gameObject.AddComponent<RectMask2D>();
            var viewFrame=Ui.Stretch("Frame",mapView).gameObject.AddComponent<Image>();viewFrame.sprite=Theme.Frame;viewFrame.type=Image.Type.Sliced;viewFrame.color=PhosDim;viewFrame.raycastTarget=false;
            termDeckTabs=Glow(screen,"",new Vector2(26,-764),new Vector2(860,32),18,Phos);
            termMapNote=Glow(mapView,"",new Vector2(0,-ViewH*.5f+30),new Vector2(ViewW,60),24,new Color(1,.7f,.3f),TextAlignmentOptions.Center);
            // Right column: who is followed, their state, the water, hazard control.
            float x0=910;
            Glow(screen,"PANTAU KRU",new Vector2(x0,-70),new Vector2(300,28),20,PhosDim);
            var crew=Crew();
            ScreenButton(screen,"◄",new Vector2(x0,-102),new Vector2(56,48),()=>CycleFocus(-1),crew.Count>1,24);
            ScreenButton(screen,"►",new Vector2(x0+488,-102),new Vector2(56,48),()=>CycleFocus(1),crew.Count>1,24);
            termFocusName=Glow(screen,"",new Vector2(x0+64,-104),new Vector2(416,48),28,Phos,TextAlignmentOptions.Center);
            termFocusState=Glow(screen,"",new Vector2(x0,-162),new Vector2(546,150),20,Phos);
            var waterBox=Ui.Rect("Water",screen,Ui.TopLeft,Ui.TopLeft,new Vector2(x0,-318),new Vector2(546,150));Ui.Fill(waterBox,new Color(.02f,.07f,.05f,1));
            var waterFrame=Ui.Stretch("Frame",waterBox).gameObject.AddComponent<Image>();waterFrame.sprite=Theme.Frame;waterFrame.type=Image.Type.Sliced;waterFrame.color=PhosFaint;waterFrame.raycastTarget=false;
            var gauge=Ui.Rect("Gauge",waterBox,Ui.TopLeft,Ui.TopLeft,new Vector2(500,-12),new Vector2(32,126));Ui.Fill(gauge,PhosFloor);
            var level=Ui.Rect("Level",gauge,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);level.anchorMin=Vector2.zero;level.anchorMax=new Vector2(1,0);level.offsetMin=level.offsetMax=Vector2.zero;termWater=Ui.Fill(level,new Color(.2f,.6f,.9f,.85f));
            termInfo=Glow(waterBox,"",new Vector2(14,-10),new Vector2(480,136),20,Phos);
            var balance=SinkApp.Instance.balance;
            Glow(screen,"KONTROL BAHAYA  <color=#3E9468>mati "+Mathf.RoundToInt(balance.hazardOffSeconds)+" dtk / jeda "+Mathf.RoundToInt(balance.hazardCooldown)+" dtk</color>",new Vector2(x0,-484),new Vector2(546,28),19,Phos);
            if(wreck.hazards.Count==0)Glow(screen,"> tidak ada bahaya terdeteksi",new Vector2(x0,-518),new Vector2(546,30),19,PhosDim);
            for(int i=0;i<Mathf.Min(5,wreck.hazards.Count);i++) {
                var hazard=wreck.hazards[i];int index=i;bool ready=SinkApp.Instance.TimeNow>=s.HazardReadyAt(i);
                float y=-516-i*56;
                Glow(screen,"#"+(i+1)+" "+(hazard.electric?"LISTRIK":"UAP")+" D"+(hazard.deck+1)+" "+hazard.label,new Vector2(x0,y-4),new Vector2(300,26),17,Phos);
                termHazards.Add(Glow(screen,"",new Vector2(x0,y-28),new Vector2(300,24),16,PhosDim));
                ScreenButton(screen,"MATIKAN",new Vector2(x0+376,y),new Vector2(170,48),()=>p.Act(ActionKind.HazardOff,index),ready,18);
            }
            // Crew markers live on the map content, which is rebuilt per deck; create the content now for the focused deck.
            BuildDeckMap(s,focus!=null&&focus.Zone.Value==PlayerZone.Interior?DeckOf(focus.transform.position):ShipGrid.Decks-1);
            ScreenOverlays();
            UpdateTerminalTexts(s);UpdateTerminalMap(s,true);
        }
        // TOKO tab: fill a package (up to ItemCatalog.PackageCapacity tools), pay from the team wallet, and it is dropped on the
        // small boat by parachute after BalanceConfig.deliverySeconds. Orders in flight count down on the right.
        void BuildTerminalShop(NetSession s,NetPlayer p)
        {
            var c=s.Campaign;int cartCost=cart.Sum(id=>ItemCatalog.Get(id).value);
            Glow(screen,"PESAN ALAT  <color=#3E9468>// paket diterjunkan dengan parasut ke kapal kecil</color>",new Vector2(26,-72),new Vector2(860,30),21,Phos);
            for(int n=0;n<ItemCatalog.Shop.Length;n++) {
                int id=ItemCatalog.Shop[n];var def=ItemCatalog.Get(id);float y=-116-n*112;int inCart=cart.Count(x=>x==id);
                var row=Ui.Rect("Tool "+def.title,screen,Ui.TopLeft,Ui.TopLeft,new Vector2(26,y),new Vector2(860,100));Ui.Fill(row,new Color(.02f,.07f,.05f,1));
                var frame=Ui.Stretch("Frame",row).gameObject.AddComponent<Image>();frame.sprite=Theme.Frame;frame.type=Image.Type.Sliced;frame.color=PhosFaint;frame.raycastTarget=false;
                // The tool's picture in a dark well; the CRT's scanlines and glow (ScreenOverlays) run over it.
                var picture=ItemIcon(row,id,new Vector2(10,-8),84,null,new Color(.01f,.035f,.025f,1));
                var well=picture.transform.parent.gameObject.AddComponent<Outline>();well.effectColor=PhosFaint;well.effectDistance=new Vector2(1,-1);
                Glow(row,def.title.ToUpperInvariant(),new Vector2(108,-12),new Vector2(380,34),26,Phos);
                Glow(row,ToolInfo(id),new Vector2(108,-52),new Vector2(380,44),17,PhosDim);
                Glow(row,ItemCatalog.Money(def.value),new Vector2(486,-28),new Vector2(120,40),28,new Color(1,.7f,.3f),TextAlignmentOptions.TopRight);
                ScreenButton(row,"-",new Vector2(632,-26),new Vector2(52,48),()=>{cart.Remove(id);BuildDialog();},inCart>0,26);
                Glow(row,inCart.ToString(),new Vector2(690,-28),new Vector2(48,44),28,Phos,TextAlignmentOptions.Top);
                ScreenButton(row,"+",new Vector2(744,-26),new Vector2(52,48),()=>{cart.Add(id);BuildDialog();},cart.Count<ItemCatalog.PackageCapacity&&c.credits>=cartCost+def.value,26);
            }
            float x0=910;
            Glow(screen,"DOMPET TIM  <size=40><color=#F7AA3F>"+ItemCatalog.Money(c.credits)+"</color></size>",new Vector2(x0,-72),new Vector2(546,56),22,Phos);
            Glow(screen,"PAKET  "+cart.Count+" / "+ItemCatalog.PackageCapacity+"\n"+(cart.Count==0?"<color=#2A6A4C>> kosong</color>":string.Join("\n",cart.GroupBy(i=>i).Select(g=>"> "+ItemCatalog.Get(g.Key).title+(g.Count()>1?" x"+g.Count():""))))+"\n\nTOTAL  <color=#F7AA3F>"+ItemCatalog.Money(cartCost)+"</color>",new Vector2(x0,-140),new Vector2(546,220),21,Phos);
            ScreenButton(screen,"BAYAR & TERJUNKAN",new Vector2(x0,-372),new Vector2(546,58),()=>{p.Act(ActionKind.Buy,0,string.Join(",",cart));cart.Clear();Sfx.Ui(Snd.UiConfirm,.7f);BuildDialog();},cart.Count>0&&c.credits>=cartCost,22);
            Glow(screen,"PESANAN DALAM PERJALANAN",new Vector2(x0,-450),new Vector2(546,28),19,PhosDim);
            termOrders=Glow(screen,"",new Vector2(x0,-482),new Vector2(546,200),19,Phos);
            bool tank=p.Inventory.All.Any(i=>i.definitionId==14&&i.oxygen<90);
            ScreenButton(screen,"ISI ULANG TABUNG OKSIGEN  $20",new Vector2(x0,-700),new Vector2(546,50),()=>p.Act(ActionKind.Refill),tank&&c.credits>=20,18);
        }
        void BuildDeckMap(NetSession s,int deck)
        {
            if(mapContent!=null)Destroy(mapContent.gameObject);termCrew.Clear();termFocusMarker=null;termFocusHeading=null;
            var wreck=WorldZone.Interior;var layout=wreck.layout;termDeck=deck;
            mapContent=Ui.Rect("Deck "+(deck+1),mapView,Ui.TopLeft,Ui.TopLeft,Vector2.zero,new Vector2(MapMargin*2+ShipGrid.Columns*MapCell,MapMargin*2+ShipGrid.Rows*MapCell));
            mapContent.SetAsFirstSibling();
            // Floor fills, then walls on every closed edge, then doors and openings.
            var titles=WorldFactory.RoomTitles;
            for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {
                var kind=layout.Kind(deck,x,z,out _);if(kind==CellKind.Void)continue;
                var cell=Ui.Rect("Cell",mapContent,Ui.TopLeft,Ui.Center,CellPoint(x,z),new Vector2(MapCell,MapCell));
                Ui.Fill(cell,kind==CellKind.Corridor?PhosFloor:kind==CellKind.Room?new Color(.07f,.2f,.14f):new Color(.25f,.18f,.06f));
                if(kind==CellKind.Stair)Glow(cell,"▲",Vector2.zero,new Vector2(MapCell,MapCell),26,new Color(1,.68f,.28f,.8f),TextAlignmentOptions.Center);
            }
            foreach(var room in layout.rooms.Where(r=>r.floor==deck)) {
                var centre=(CellPoint(room.x,room.z)+CellPoint(room.x+room.w-1,room.z+room.d-1))*.5f;
                Glow(Ui.Rect("Room",mapContent,Ui.TopLeft,Ui.Center,centre,new Vector2(room.w*MapCell-6,40)),titles[room.type],Vector2.zero,new Vector2(room.w*MapCell-6,40),room.w>1?16:12,PhosDim,TextAlignmentOptions.Center);
            }
            for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {
                var kind=layout.Kind(deck,x,z,out int module);if(kind==CellKind.Void)continue;
                for(int dir=0;dir<4;dir++) {
                    var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];var other=layout.Kind(deck,n.x,n.y,out int otherModule);
                    bool open=layout.Open(deck,x,z,dir);if(open&&other==kind&&otherModule==module)continue;
                    var d=new Vector2(ShipGrid.Dirs[dir].x,ShipGrid.Dirs[dir].y);var mid=CellPoint(x,z)+new Vector2(d.x,d.y)*MapCell*.5f;
                    bool horizontal=dir%2==1;var size=horizontal?new Vector2(MapCell+3,3):new Vector2(3,MapCell+3);
                    if(!open) {Ui.Fill(Ui.Rect("Wall",mapContent,Ui.TopLeft,Ui.Center,mid,size),Phos);continue;}
                    int door=layout.DoorAt(deck,x,z,dir);if(door<0)continue;
                    // A door: a gap in the wall with a bar across it, red while it is still locked.
                    bool locked=s.DoorLockedNow(door);var gap=horizontal?new Vector2(MapCell*.3f,3):new Vector2(3,MapCell*.3f);
                    foreach(float side in new[]{-1f,1f}) {var along=horizontal?new Vector2(side*MapCell*.35f,0):new Vector2(0,side*MapCell*.35f);Ui.Fill(Ui.Rect("Wall",mapContent,Ui.TopLeft,Ui.Center,mid+along,gap),Phos);}
                    Ui.Fill(Ui.Rect("Door",mapContent,Ui.TopLeft,Ui.Center,mid,horizontal?new Vector2(MapCell*.38f,7):new Vector2(7,MapCell*.38f)),locked?Theme.Red:new Color(.46f,1,.7f,.45f));
                }
            }
            for(int i=0;i<layout.entries.Count;i++) {
                var e=layout.entries[i];if(e.floor!=deck)continue;
                var at=CellPoint(e.x,e.z)+new Vector2(ShipGrid.Dirs[e.dir].x,ShipGrid.Dirs[e.dir].y)*MapCell*.95f;
                var mark=Ui.Rect("Entry",mapContent,Ui.TopLeft,Ui.Center,at,new Vector2(44,44));Ui.Fill(mark,new Color(1,.68f,.28f,.25f));
                Glow(mark,InteriorLayout.EntryNames[i],Vector2.zero,new Vector2(44,44),26,new Color(1,.68f,.28f),TextAlignmentOptions.Center);
            }
            for(int i=0;i<wreck.hazards.Count;i++) {
                var hazard=wreck.hazards[i];if(hazard.deck!=deck)continue;
                var mark=Ui.Rect("Hazard",mapContent,Ui.TopLeft,Ui.Center,MapPoint(hazard.transform.position),new Vector2(26,26));Ui.Fill(mark,s.HazardSuppressed(i)?Theme.Good:Theme.Red);
                Glow(mark,(i+1).ToString(),Vector2.zero,new Vector2(26,26),17,Theme.Ink,TextAlignmentOptions.Center);
            }
            foreach(var player in Crew()) {
                bool focused=player.OwnerClientId==termFocus;var color=CrewColors[player.OwnerClientId%4];
                var dot=Ui.Rect("Crew",mapContent,Ui.TopLeft,Ui.Center,Vector2.zero,focused?new Vector2(30,30):new Vector2(18,18));
                if(focused) {
                    // The followed diver: a ring with a heading wedge, so the operator can say "turn left".
                    var ring=dot.gameObject.AddComponent<Image>();ring.sprite=Theme.Frame;ring.type=Image.Type.Sliced;ring.color=color;ring.raycastTarget=false;
                    Ui.Fill(Ui.Rect("Core",dot,Ui.Center,Ui.Center,Vector2.zero,new Vector2(14,14)),color);
                    var heading=Ui.Rect("Heading",dot,Ui.Center,Ui.Center,Vector2.zero,new Vector2(30,30));
                    var nose=Ui.Rect("Nose",heading,Ui.Center,new Vector2(.5f,0),new Vector2(0,16),new Vector2(6,18));Ui.Fill(nose,color);
                    termFocusMarker=dot;termFocusHeading=heading;
                } else Ui.Fill(dot,color);
                var name=player.Name.Value.ToString().Trim();
                Glow(dot,name.Length>0?name:"?",new Vector2(focused?22:14,10),new Vector2(200,22),focused?18:14,color);
                termCrew.Add((dot,player));
            }
        }
        void UpdateTerminalMap(NetSession s,bool snap=false)
        {
            if(mapContent==null)return;var focus=FocusedCrew();
            bool inside=focus!=null&&focus.Alive.Value&&focus.Zone.Value==PlayerZone.Interior;
            if(inside) {int deck=DeckOf(focus.transform.position);if(deck!=termDeck) {BuildDeckMap(s,deck);snap=true;}}
            foreach(var (dot,player) in termCrew) {
                bool show=player!=null&&player.Alive.Value&&player.Zone.Value==PlayerZone.Interior&&DeckOf(player.transform.position)==termDeck;dot.gameObject.SetActive(show);
                if(!show)continue;dot.anchoredPosition=MapPoint(player.transform.position);
                if(dot==termFocusMarker&&termFocusHeading!=null)termFocusHeading.localRotation=Quaternion.Euler(0,0,-player.transform.eulerAngles.y);
            }
            // Keep the followed diver in the middle of the view; with nobody to follow, show the whole deck from its centre.
            var centre=inside?MapPoint(focus.transform.position):new Vector2(mapContent.sizeDelta.x*.5f,-mapContent.sizeDelta.y*.5f);
            var goal=new Vector2(ViewW*.5f-centre.x,-ViewH*.5f-centre.y);
            mapContent.anchoredPosition=snap?goal:Vector2.Lerp(mapContent.anchoredPosition,goal,1-Mathf.Exp(-Time.unscaledDeltaTime*8));
            if(termFocusMarker!=null) {float pulse=1+.12f*Mathf.Sin(Time.unscaledTime*6);termFocusMarker.localScale=Vector3.one*pulse;}
        }
        void UpdateTerminalTexts(NetSession s)
        {
            var app=SinkApp.Instance;var focus=FocusedCrew();var crew=Crew();
            string clock=TimeSpan.FromHours(s.Hour).ToString(@"hh\:mm");
            if(termHeader!=null)termHeader.text="S-04 // KOMPUTER KAPAL   "+(s.InExpedition?"<color=#77FFB2>■ TERHUBUNG</color>":"<color=#E64033>■ OFFLINE</color>")+"   JAM "+clock+(s.InExpedition?"   SISA "+TimeSpan.FromSeconds(s.Remaining).ToString(@"mm\:ss"):"");
            if(termOrders!=null)termOrders.text=s.Campaign.orders.Count==0?"<color=#2A6A4C>> tidak ada</color>":string.Join("\n",s.Campaign.orders.Select(o=>"> "+string.Join(", ",o.items.Select(i=>ItemCatalog.Get(i).title))+"  <color=#F7AA3F>"+Mathf.CeilToInt(Mathf.Max(0,o.remaining))+" dtk</color>"));
            if(termInfo==null)return;
            var wreck=WorldZone.Interior;float depth=s.Depth,flood=s.FloodLevel;
            int floodDeck=flood>=ShipTimeline.FloorHeight(2)?3:flood>=ShipTimeline.FloorHeight(1)?2:1;float gap=ShipTimeline.CargoDeck-depth-ShipTimeline.BoatDeck;
            termInfo.text="LEVEL AIR   <size=34><color=#F7AA3F>"+depth.ToString("0.0")+" m</color></size>\nair sampai dek "+floodDeck+"\ngeladak kargo "+(gap>=0?"+":"")+gap.ToString("0.0")+" m dari kapal kecil\nSISA WAKTU  "+TimeSpan.FromSeconds(s.Remaining).ToString(@"mm\:ss");
            if(termWater!=null)termWater.rectTransform.anchorMax=new Vector2(1,Mathf.Clamp01(flood/(ShipTimeline.FloorHeight(2)+3)));
            if(focus!=null) {
                int index=crew.IndexOf(focus)+1;var color=ColorUtility.ToHtmlStringRGB(CrewColors[focus.OwnerClientId%4]);
                termFocusName.text="<color=#"+color+">"+focus.Name.Value.ToString()+"</color>"+(focus.IsOwner?" <size=18>(kamu)</size>":"")+"  <size=18>"+index+"/"+crew.Count+"</size>";
                bool inside=focus.Zone.Value==PlayerZone.Interior;
                string where=!focus.Alive.Value?"<color=#E64033>SINYAL HILANG</color>":inside?"DEK "+(DeckOf(focus.transform.position)+1)+(focus.Underwater?"  <color=#5AB4FF>DI BAWAH AIR</color>":""):focus.Zone.Value==PlayerZone.Exterior?"GELADAK KAPAL KARGO":focus.Zone.Value==PlayerZone.Boat?"KAPAL KECIL":"PULAU";
                var torch=focus.Inventory.Torch?.charge??-1;
                float hp=focus.Health.Value;string Bar(float v)=>new string('█',Mathf.Clamp(Mathf.RoundToInt(v*10),0,10))+new string('░',10-Mathf.Clamp(Mathf.RoundToInt(v*10),0,10));
                termFocusState.text="LOKASI    "+where+"\nKESEHATAN "+Bar(hp/100)+" "+Mathf.CeilToInt(hp)+"\nNAPAS     "+Bar(focus.Oxygen.Value/Mathf.Max(1,app.balance.breath))+" "+Mathf.CeilToInt(focus.Oxygen.Value)+" dtk\nSENTER    "+(torch<0?"tidak ada":Mathf.CeilToInt(torch*100)+"%"+(focus.Inventory.lightOn?" • nyala":""))+"\nBAWAAN    "+focus.Inventory.items.Count+"/4"+(focus.Inventory.items.Any(i=>i.Definition.kind==ItemKind.Key)?" • ada kunci":"");
                if(termMapNote!=null)termMapNote.text=inside&&focus.Alive.Value?"":!focus.Alive.Value?"> SINYAL "+focus.Name.Value.ToString().ToUpperInvariant()+" HILANG":"> "+focus.Name.Value.ToString().ToUpperInvariant()+" TIDAK DI DALAM KAPAL KARGO";
            }
            if(termDeckTabs!=null) {var tabs=new System.Text.StringBuilder();for(int d=0;d<ShipGrid.Decks;d++)tabs.Append(d==termDeck?"<color=#77FFB2>[ DEK "+(d+1)+" ]</color>   ":"<color=#2A6A4C>  DEK "+(d+1)+"    </color>");tabs.Append("<color=#2A6A4C>▲ tangga  A/B/C masuk  <color=#E64033>■</color> kunci</color>");termDeckTabs.text=tabs.ToString();}
            var now=app.TimeNow;
            for(int i=0;i<termHazards.Count;i++) {
                bool off=s.HazardSuppressed(i);double wait=s.HazardReadyAt(i)-now;
                termHazards[i].text=off?"<color=#84D378>MATI "+Mathf.CeilToInt((float)(s.HazardOffUntil(i)-now))+" dtk</color>":wait>0?"JEDA "+Mathf.CeilToInt((float)wait)+" dtk":"<color=#E64033>AKTIF</color>";
            }
        }
        void UpdateTerminal(NetSession s)
        {
            if(TerminalSignature(s)!=termSignature) {BuildDialog();return;}
            UpdateTerminalTexts(s);
        }
        void BuildQuota(NetSession s,NetPlayer p)
        {
            var c=s.Campaign;bool eligible=c.expeditionsUsed>=3,success=c.sold>=c.quota;
            Header("EVALUASI KUOTA","Kuota dinilai setelah tiga ekspedisi dan kesempatan menjual terakhir.");
            var bar=Ui.Bar(card,"Quota",new Vector2(44,-210),new Vector2(852,56),success?Theme.Good:Theme.Amber,26);bar.Set((float)c.sold/Mathf.Max(1,c.quota),"PENJUALAN  "+ItemCatalog.Money(c.sold)+" / "+ItemCatalog.Money(c.quota),false,true);
            Row("EKSPEDISI  "+Mathf.Min(3,c.expeditionsUsed)+" / 3      SIKLUS  "+c.cycle,new Vector2(44,-284),new Vector2(852,34),24,Theme.Muted);
            Row(!eligible?"Habiskan jatah tiga ekspedisi sebelum menutup siklus. Mencapai angka lebih awal tidak mempercepat evaluasi.":success?"<color=#84D378>Target terpenuhi.</color> Uang, alat, dan muatan yang belum dijual dipertahankan. Target berikutnya "+ItemCatalog.Money((int)Math.Ceiling(c.quota*1.35/10)*10)+".":"<color=#E64033>Target belum terpenuhi.</color> Kamu masih bisa menjual muatan. Menutup siklus sekarang mereset uang, alat, dan muatan ke kondisi awal.",new Vector2(44,-340),new Vector2(852,200),26,Theme.Paper);
            Ui.Button(card,success?"MULAI SIKLUS BERIKUTNYA":"KONFIRMASI RESET KAMPANYE",new Vector2(44,-600),new Vector2(852,76),()=>{p.Act(ActionKind.ResolveQuota);Close();},eligible,true);
        }
        public void OpenResults()
        {
            dialog.gameObject.SetActive(true);Header("HASIL EKSPEDISI",NetSession.Instance.LastResult);
            var c=NetSession.Instance.Campaign;int value=c.cargo.Where(x=>x.Definition.kind==ItemKind.Scrap).Sum(x=>x.Definition.value);
            var summary=Ui.Panel(card,"Result",Ui.TopLeft,Ui.TopLeft,new Vector2(44,-300),new Vector2(852,170),Theme.Panel);
            Ui.Label(summary,"SCRAP DI KAPAL KECIL\n<size=44><color=#F7AA3F>"+ItemCatalog.Money(value)+"</color></size>  •  "+c.cargo.Count+" barang",new Vector2(24,-16),new Vector2(800,130),26,Theme.Paper);
            Row("Muatan kapal hanya selamat jika minimal satu kru kembali ke kapal kecil. Alat yang hilang tidak diganti otomatis.\n\n"+(c.expeditionsUsed>=3?"Hari ketiga selesai: berlayar ke pulau lewat KEMUDI.":"Gunakan KEMUDI untuk ekspedisi berikutnya."),new Vector2(44,-500),new Vector2(852,240),25,Theme.Muted);
            Sfx.Ui(Snd.UiConfirm,.7f);
        }
        public void OpenSettings()
        {
            dialog.gameObject.SetActive(true);Header("PENGATURAN","Waktu ekspedisi tetap berjalan saat menu terbuka.");
            var app=SinkApp.Instance;var comms=app.GetComponent<VoiceComms>();var retro=RetroLook.Instance;
            bool full=Screen.fullScreenMode!=FullScreenMode.Windowed;
            Ui.Button(card,"RETRO: "+(retro!=null?RetroLook.Names[retro.Level]:"-"),new Vector2(44,-192),new Vector2(274,50),()=>{retro?.SetLevel((retro.Level+1)%RetroLook.Names.Length);OpenSettings();},true,false,"",19);
            Ui.Button(card,"BAYANGAN: "+(retro!=null?RetroLook.ShadowNames[retro.Shadows]:"-"),new Vector2(333,-192),new Vector2(274,50),()=>{retro?.SetShadows((retro.Shadows+1)%RetroLook.ShadowNames.Length);OpenSettings();},true,false,"",19);
            Ui.Button(card,"LAYAR: "+(full?"PENUH":"JENDELA"),new Vector2(622,-192),new Vector2(274,50),()=>{SetFullscreen(!full);OpenSettings();},true,false,"",19);
            SliderRow("Bidang pandang",Fov,60,100,254,v=>{Fov=v;PlayerPrefs.SetFloat("SinkFov",v);},v=>Mathf.RoundToInt(v)+"°");
            SliderRow("Sensitivitas",Sensitivity,.2f,3,306,v=>{Sensitivity=v;PlayerPrefs.SetFloat("SinkLook",v);},v=>v.ToString("0.0"));
            SliderRow("Kecerahan",RetroLook.Brightness,.6f,1.6f,358,v=>RetroLook.SetBrightness(v),v=>Mathf.RoundToInt(v*100)+"%");
            SliderRow("Volume utama",AudioListener.volume,0,1,422,v=>{AudioListener.volume=v;PlayerPrefs.SetFloat("SinkVolume",v);});
            SliderRow("Efek suara",AudioDirector.BusVolume(Bus.Sfx),0,1,474,v=>AudioDirector.SetBus(Bus.Sfx,v));
            SliderRow("Suasana",AudioDirector.BusVolume(Bus.Ambience),0,1,526,v=>AudioDirector.SetBus(Bus.Ambience,v));
            SliderRow("Musik",AudioDirector.BusVolume(Bus.Music),0,1,578,v=>AudioDirector.SetBus(Bus.Music,v));
            SliderRow("Suara kru",voiceVolume,0,1,630,v=>{voiceVolume=v;comms.SetVoiceVolume(v);PlayerPrefs.SetFloat("SinkVoiceVolume",v);});
            Ui.Button(card,"MIKROFON: "+comms.MicrophoneName,new Vector2(44,-692),new Vector2(852,48),()=>{comms.NextMicrophone();OpenSettings();},true,false,"",20);
            Ui.Button(card,comms.muted?"MIKROFON: DIBISUKAN (klik untuk aktifkan)":"MIKROFON: AKTIF (klik untuk bisukan)",new Vector2(44,-746),new Vector2(852,48),()=>{comms.muted=!comms.muted;OpenSettings();},true,false,"",20);
            int index=0;foreach(var participant in comms.Participants.Where(x=>!x.IsSelf).Take(3)) {var entry=participant;Ui.Button(card,(comms.IsMuted(entry.PlayerId)?"Aktifkan suara ":"Bisukan ")+entry.DisplayName,new Vector2(44+index*290,-800),new Vector2(280,44),()=>{comms.ToggleMute(entry.PlayerId);OpenSettings();},true,false,"",17);index++;}
            if(app.Connected)Ui.Button(card,"KEMBALI KE MENU",new Vector2(44,-860),new Vector2(414,56),()=>{Close();app.Leave();},true,false,"",21);
            Ui.Button(card,"PANDUAN",new Vector2(app.Connected?478:44,-860),new Vector2(app.Connected?418:852,56),()=>OpenGuide(),true,false,"",21);
        }
        // Windowed <-> borderless fullscreen. The Editor keeps its own Game view, so this only matters in a player.
        static void SetFullscreen(bool on)
        {
            PlayerPrefs.SetInt("SinkFull",on?1:0);if(Application.isEditor)return;
            Screen.fullScreenMode=on?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
        }
        void SliderRow(string label,float value,float min,float max,float y,Action<float> changed,Func<float,string> format=null)
        {
            Ui.Label(card,label,new Vector2(44,-y-8),new Vector2(300,32),22,Theme.Paper);
            var valueLabel=Ui.Label(card,"",new Vector2(800,-y-8),new Vector2(96,32),22,Theme.Amber,TextAlignmentOptions.TopRight);
            Ui.Slider(card,new Vector2(350,-y),new Vector2(430,34),min,max,value,changed,valueLabel,format);
        }
        public void OpenGuide(int page=0)
        {
            guidePage=Mathf.Clamp(page,0,Pages.Length-1);dialog.gameObject.SetActive(true);var entry=Pages[guidePage];
            Header(entry.title,"PANDUAN KRU   "+(guidePage+1)+" / "+Pages.Length,"TUTUP");
            Ui.Label(card,entry.body,new Vector2(44,-210),new Vector2(852,560),27,Theme.Paper);
            for(int i=0;i<Pages.Length;i++){var dotRect=Ui.Rect("Page dot",card,Ui.TopLeft,Ui.TopLeft,new Vector2(44+i*30,-790),new Vector2(20,20));Ui.Fill(dotRect,i==guidePage?Theme.Amber:Theme.Line);}
            Ui.Button(card,"←  SEBELUMNYA",new Vector2(44,-840),new Vector2(300,64),()=>OpenGuide(guidePage-1),guidePage>0,false,"",21);
            bool last=guidePage==Pages.Length-1;
            Ui.Button(card,last?"MENGERTI":"SELANJUTNYA  →",new Vector2(596,-840),new Vector2(300,64),()=>{if(last){PlayerPrefs.SetInt("SinkGuide",1);Close();}else OpenGuide(guidePage+1);},true,true,"",21);
        }
        // First visit: teach the safe-cargo rule and the loop before the first expedition.
        void MaybeShowGuide()
        {
            if(PlayerPrefs.GetInt("SinkGuide",0)==1||Application.isBatchMode||FindAnyObjectByType<QaRunner>()!=null||dialog.gameObject.activeSelf)return;
            OpenGuide();
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SinkScrapt
{
    public class VoiceComms : MonoBehaviour
    {
        public bool ready,muted;
        public string transmitting="";
        string proximity,radio,spectators,current;
        bool initialized,changing;
        float positionAt;
        readonly HashSet<string> joined=new();
        readonly HashSet<string> mutedPlayers=new();
        public IEnumerable<VivoxParticipant> Participants=>ready?VivoxService.Instance.ActiveChannels.Values.SelectMany(x=>x).GroupBy(x=>x.PlayerId).Select(x=>x.First()):Array.Empty<VivoxParticipant>();
        public async Task Connect(string sessionId)
        {
            try {
                var service=VivoxService.Instance;
                if(!initialized) {await service.InitializeAsync();service.ChannelJoined+=ChannelJoined;service.ChannelLeft+=ChannelLeft;initialized=true;}
                await service.LoginAsync(new LoginOptions {DisplayName=SinkApp.Instance.playerName});
                service.MuteInputDevice();
                proximity="sink-"+sessionId+"-near";radio="sink-"+sessionId+"-radio";spectators="sink-"+sessionId+"-spectate";
                var properties=new Channel3DProperties(15,1,1,AudioFadeModel.LinearByDistance);
                await service.JoinPositionalChannelAsync(proximity,ChatCapability.AudioOnly,properties);
                await service.JoinGroupChannelAsync(radio,ChatCapability.AudioOnly);
                await service.JoinGroupChannelAsync(spectators,ChatCapability.AudioOnly);
                await service.SetChannelTransmissionModeAsync(TransmissionMode.None);
                var deadline=Time.realtimeSinceStartup+10;while(joined.Count<3&&Time.realtimeSinceStartup<deadline)await Task.Delay(50);
                if(joined.Count<3)throw new TimeoutException("Voice channels were not ready.");
                if(!muted)service.UnmuteInputDevice();current="";ready=true;SetVoiceVolume(PlayerPrefs.GetFloat("SinkVoiceVolume",.8f));
            } catch(Exception e) {ready=false;SinkApp.Instance.notice="Suara belum tersambung. Gameplay tetap berjalan.";Debug.LogWarning("Vivox: "+e.Message);}
        }
        void ChannelJoined(string channel) {joined.Add(channel);}
        void ChannelLeft(string channel) {joined.Remove(channel);}
        void Update()
        {
            if(!ready||NetPlayer.Local==null||changing)return;
            var player=NetPlayer.Local;var keyboard=Keyboard.current;
            bool modal=GameHUD.Instance!=null&&GameHUD.Instance.Modal;
            string requested="";
            if(!muted&&!modal&&keyboard!=null) {
                if(!player.Alive.Value&&keyboard.vKey.isPressed)requested=spectators;
                else if(player.Alive.Value&&keyboard.rKey.isPressed&&player.HasRadio&&(!player.HasHeavy||player.RadioOnBelt))requested=radio;
                else if(player.Alive.Value&&keyboard.vKey.isPressed)requested=proximity;
            }
            transmitting=requested==radio?"RADIO":requested==proximity?"SUARA DEKAT":requested==spectators?"SPECTATOR":"";
            if(requested!=current)_=Transmit(requested);
            if(Time.unscaledTime>=positionAt&&joined.Contains(proximity)) {
                positionAt=Time.unscaledTime+.1f;
                VivoxService.Instance.Set3DPosition(player.eyes!=null?player.eyes.gameObject:player.gameObject,proximity);
                foreach(var kv in VivoxService.Instance.ActiveChannels)foreach(var participant in kv.Value) {
                    if(participant.IsSelf)continue;
                    bool blocked=mutedPlayers.Contains(participant.PlayerId)||(kv.Key==radio&&!player.HasRadio)||(kv.Key==spectators&&player.Alive.Value);
                    if(blocked)participant.MutePlayerLocally();else participant.UnmutePlayerLocally();
                }
            }
        }
        async Task Transmit(string channel)
        {
            changing=true;
            try {await VivoxService.Instance.SetChannelTransmissionModeAsync(channel.Length==0?TransmissionMode.None:TransmissionMode.Single,channel.Length==0?null:channel);current=channel;}
            catch(Exception e) {Debug.LogWarning("Voice transmit: "+e.Message);}
            finally {changing=false;}
        }
        public void ToggleMute(string id) {if(!mutedPlayers.Add(id))mutedPlayers.Remove(id);}
        public bool IsMuted(string id)=>mutedPlayers.Contains(id);
        public string MicrophoneName=>ready?VivoxService.Instance.ActiveInputDevice?.DeviceName??"Default":"Default";
        public async void NextMicrophone()
        {
            if(!ready)return;var service=VivoxService.Instance;var devices=service.AvailableInputDevices;if(devices.Count==0)return;
            int index=devices.ToList().FindIndex(x=>x.DeviceName==service.ActiveInputDevice?.DeviceName);
            try {await service.SetActiveInputDeviceAsync(devices[(index+1)%devices.Count]);}catch(Exception e){Debug.LogWarning(e.Message);}
        }
        public void SetVoiceVolume(float value) {if(ready)VivoxService.Instance.SetOutputDeviceVolume(Mathf.RoundToInt(Mathf.Lerp(-50,10,value)));}
        public async Task Disconnect()
        {
            ready=false;current="";transmitting="";joined.Clear();
            if(!initialized)return;
            try {await VivoxService.Instance.LeaveAllChannelsAsync();await VivoxService.Instance.LogoutAsync();}catch(Exception e){Debug.LogWarning(e.Message);}
        }
        void OnDestroy()
        {
            if(!initialized)return;VivoxService.Instance.ChannelJoined-=ChannelJoined;VivoxService.Instance.ChannelLeft-=ChannelLeft;
        }
    }
}

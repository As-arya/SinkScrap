var app=SinkScrapt.SinkApp.Instance;
var voice=app?.GetComponent<SinkScrapt.VoiceComms>();
return new { cloudConfigured=app?.OnlineConfigured, connected=app?.Connected, busy=app?.busy, notice=app?.notice, code=app?.joinCode, voiceReady=voice!=null&&voice.ready, players=SinkScrapt.NetPlayer.All.Count };

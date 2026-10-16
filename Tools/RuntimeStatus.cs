var app=SinkScrapt.SinkApp.Instance;
var qa=UnityEngine.Object.FindFirstObjectByType<SinkScrapt.QaRunner>();
var session=SinkScrapt.NetSession.Instance;
return new {connected=app!=null&&app.Connected,notice=app?.notice,phase=session?.Phase.Value.ToString(),players=SinkScrapt.NetPlayer.All.Count,qaDone=qa!=null&&qa.done,qaPassed=qa!=null&&qa.passed,qaFailure=qa?.failure,checks=qa?.checks.Count,position=SinkScrapt.NetPlayer.Local?.transform.position.ToString(),campaign=session?.Campaign};

var app=SinkScrapt.SinkApp.Instance;
app.SavePath=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../Artifacts/QA-Editor/campaign.json"));
app.newCampaign=true;
var qa=app.gameObject.AddComponent<SinkScrapt.QaRunner>();
qa.expectedPlayers=1;
qa.iterations=10;
return "QA started with isolated checkpoint";

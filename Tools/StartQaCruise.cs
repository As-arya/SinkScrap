var app=SinkScrapt.SinkApp.Instance;
app.SavePath=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../Artifacts/QA-Editor-Cruise/campaign.json"));
app.newCampaign=true;
var qa=app.gameObject.AddComponent<SinkScrapt.QaRunner>();
qa.expectedPlayers=1;
qa.ship=SinkScrapt.ShipKind.Cruise;
qa.iterations=3;
return "Cruise-ship QA started with isolated checkpoint";

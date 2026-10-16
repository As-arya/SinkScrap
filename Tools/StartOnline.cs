var app=SinkScrapt.SinkApp.Instance;
app.SavePath=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../Artifacts/QA-Online/campaign.json"));
app.newCampaign=true;
_ = app.StartOnline(true,"");
return "Online host requested; checkpoint isolated";

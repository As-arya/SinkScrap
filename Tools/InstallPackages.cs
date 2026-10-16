var request = UnityEditor.PackageManager.Client.AddAndRemove(new[] { "com.unity.netcode.gameobjects", "com.unity.services.multiplayer", "com.unity.services.authentication", "com.unity.services.vivox" });
UnityEditor.EditorApplication.CallbackFunction poll = null;
poll = () => {
    if (!request.IsCompleted) return;
    UnityEditor.EditorApplication.update -= poll;
    if (request.Status == UnityEditor.PackageManager.StatusCode.Success) UnityEngine.Debug.Log("SinkScrapt packages installed");
    else UnityEngine.Debug.LogError("SinkScrapt package install: " + request.Error.message);
};
UnityEditor.EditorApplication.update += poll;
return "Package installation queued in the live Editor";

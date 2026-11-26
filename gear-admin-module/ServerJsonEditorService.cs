using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

public static class ServerJsonEditorService
{
    static readonly Lazy<ServerJsonStore> Store = new(() =>
    {
        var docs = new ServerJsonModelDocs(typeof(HttpResponseUtil).Assembly, Path.Combine(AppContext.BaseDirectory, "SPTarkov.Server.Core.xml"));
        return new ServerJsonStore(AppContext.BaseDirectory, docs.Describe);
    });
    public static string Handle(CharacterDataRequest r, string owner, HttpResponseUtil http, CancellationToken ct) => http.GetBody(Store.Value.Execute(owner,
        new(r.Action, r.Id, r.JsonToken, r.JsonPath, r.JsonOffset, r.JsonQuery, r.Preview, r.DeleteNode, r.JsonAddField ? r.ChildKey : null, r.JsonAppend), ct));
}

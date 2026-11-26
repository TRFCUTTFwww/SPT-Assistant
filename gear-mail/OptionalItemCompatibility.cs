using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
[Injectable(TypePriority=OnLoadOrder.Preload+20)]
public sealed class OptionalItemCompatibility(SaveServer saves,JsonUtil json,TemplateTable templates,MailSendService mail):IOnLoad {
 public async Task OnLoadAsync(CancellationToken ct){FunItemQuarantine.Install(saves,json,templates,mail);await FunItemQuarantine.ProcessAll();}
}

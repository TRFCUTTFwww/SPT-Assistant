using LocalSpt.Shared;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Dialogue;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace LocalGearMail;

[Injectable]
public sealed class Nw11ChatBot : IDialogueChatBot
{
    static readonly MongoId ContactId = new(Nw11Contact.Id);
    readonly SaveServer saves;

    public Nw11ChatBot(CoreConfig config, SaveServer saves)
    {
        this.saves = saves;
        config.Features.ChatbotFeatures.EnabledBots[ContactId] = true;
    }

    public UserDialogInfo GetChatBot() => new()
    {
        Id = ContactId,
        Aid = Nw11Contact.AccountId,
        Info = new UserDialogDetails
        {
            Nickname = Nw11Contact.Name,
            Side = "Usec",
            MemberCategory = (MemberCategory)0,
            SelectedMemberCategory = (MemberCategory)0
        }
    };

    public async ValueTask<string> HandleMessage(MongoId sessionId, SendMessageRequest request)
    {
        if (!Nw11Contact.IsContact(request.DialogId))
            return string.Empty;

        // DialogueController has already stored the outgoing text in the sender's own dialogue.
        await saves.SaveProfileAsync(sessionId);
        return Nw11Contact.Id;
    }
}

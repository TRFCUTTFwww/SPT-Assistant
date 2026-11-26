using System.Reflection;
using System.Runtime.CompilerServices;
using LocalGearMail;
using Microsoft.Extensions.DependencyInjection;
using SPTarkov.DI;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Dialogue;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

internal static class Nw11ChatBotChecks
{
    internal static async Task Run(string gameRoot, JsonUtil json)
    {
        var config = json.Deserialize<CoreConfig>(File.ReadAllText(System.IO.Path.Combine(gameRoot, "SPT_Runtime", "SPT_Data", "configs", "core.json")))!;
        var previous = config.Features.ChatbotFeatures.EnabledBots.ToArray();
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddSingleton((SaveServer)RuntimeHelpers.GetUninitializedObject(typeof(SaveServer)));
        var di = new DependencyInjectionHandler(services);
        di.AddInjectableTypesFromTypeList([typeof(Nw11ChatBot)]);
        di.InjectAll();
        using var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<IDialogueChatBot>().ToArray();
        var bot = registered.OfType<Nw11ChatBot>().Single();
        var controller = new DialogueController(null!, null!, null!, null!, null!, null!, null!, null!, null!, config, null!, registered);
        var contact = controller.GetActiveChatBots().Single();
        if (contact.Info?.Nickname != "Nw11" || contact.Id.ToString() != "6aba11000000000000000011"
            || previous.Any(entry => config.Features.ChatbotFeatures.EnabledBots[entry.Key] != entry.Value))
            throw new Exception("Native friend registration changed another chatbot or returned an unstable identity");

        var nativeOpen = typeof(DialogueController).GetMethod("GetDialogByIdFromProfile", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        var profile = new SptProfile { DialogueRecords = new() };
        var request = new GetMailDialogViewRequestData { DialogId = contact.Id, Type = MessageType.UserMessage };
        var dialogue = (Dialogue)nativeOpen.Invoke(controller, [profile, request])!;
        if (dialogue.Users?.Single().Id != contact.Id || dialogue.Type != MessageType.UserMessage || dialogue.Messages?.Count != 0)
            throw new Exception("Native chat could not create a private conversation for the profileless contact");

        if (await bot.HandleMessage(new MongoId(), new SendMessageRequest { DialogId = new MongoId().ToString(), Type = MessageType.UserMessage, Text = "test", ReplyTo = "" }) != "")
            throw new Exception("Nw11 handled a different conversation");
        Console.WriteLine("PASS native Nw11 friend discovery and profileless private dialogue");
    }
}

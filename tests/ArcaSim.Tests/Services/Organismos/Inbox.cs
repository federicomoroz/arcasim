using ArcaSim.Application.Contracts;
using ArcaSim.Application.Services.Organismos;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Puts communications in a CUIT's inbox the way an operator does: as documents, with the three default ones left out.</summary>
internal static class Inbox
{
    public static async Task<Communication> PublishAsync(IDocumentStore store, Communication communication)
    {
        await store.SkipSeedAsync(VentanillaInbox.Scope(communication.Cuit));
        if (communication.Id == 0) communication = communication with { Id = await store.NextAsync(VentanillaInbox.Communications) };
        await store.PutAsync(VentanillaInbox.Communications, VentanillaInbox.Key(communication.Id), communication);
        return communication;
    }
}

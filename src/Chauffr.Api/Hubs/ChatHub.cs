using Microsoft.AspNetCore.SignalR;
using Chauffr.Api.Services;
using Chauffr.Core.Models;

namespace Chauffr.Api.Hubs;

public class ChatHub : Hub
{
    private readonly QwenOrchestrationService _orchestration;

    public ChatHub(QwenOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }

    /// <summary>
    /// Invoked by the client widget when the user sends a query.
    /// </summary>
    public async Task SendMessage(string userMessage, ClientTelemetry telemetry)
    {
        Console.WriteLine($"[ChatHub] User: '{userMessage}' from '{telemetry.CurrentUrl}'");

        try
        {
            var response = await _orchestration.ProcessUserMessageAsync(
                userMessage,
                telemetry,
                Context.ConnectionAborted);

            // Emit the structured response with message and safe co-browsing actions
            await Clients.Caller.SendAsync("ReceiveAssistantMessage", response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHub] Error processing message: {ex.Message}");
            var errorResponse = new AssistantResponse(
                Message: "Sorry, I encountered an internal error while processing your request.",
                Action: null
            );
            await Clients.Caller.SendAsync("ReceiveAssistantMessage", errorResponse);
        }
    }
}

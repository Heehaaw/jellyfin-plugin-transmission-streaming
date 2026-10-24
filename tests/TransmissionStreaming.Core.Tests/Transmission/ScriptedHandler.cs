namespace TransmissionStreaming.Core.Tests.Transmission;

using System.Net;


public sealed class ScriptedHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond { get; set; } =
        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Respond(request);
}

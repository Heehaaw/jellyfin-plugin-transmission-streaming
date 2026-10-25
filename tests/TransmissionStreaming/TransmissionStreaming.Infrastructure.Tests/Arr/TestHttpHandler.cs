namespace TransmissionStreaming.Infrastructure.Tests.Arr;

using global::TransmissionStreaming.Core.Arr;

public sealed class TestHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => throw new InvalidOperationException("Unexpected request.");

    public List<Uri> Requests { get; } = [];

    public List<string> ApiKeys { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        ApiKeys.Add(request.Headers.GetValues("X-Api-Key").Single());
        return Task.FromResult(Respond(request));
    }
}

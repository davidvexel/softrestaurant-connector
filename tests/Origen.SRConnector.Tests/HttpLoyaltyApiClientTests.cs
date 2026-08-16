using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.Api;

namespace Origen.SRConnector.Tests;

public sealed class HttpLoyaltyApiClientTests
{
    [Fact]
    public async Task SendSale_PostsBearerAuthenticatedJson()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var client = CreateClient(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var result = await client.SendSaleAsync(TestSaleFactory.Create(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://app.origennatural.mx/api/v1/sales", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("orp_test", capturedRequest.Headers.Authorization.Parameter);
        Assert.Contains("\"ticket\": 1735", capturedBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TemporaryHttpErrors_AreRetryable(HttpStatusCode statusCode)
    {
        var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(statusCode)));

        var result = await client.SendSaleAsync(TestSaleFactory.Create(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ApiFailureKind.Retryable, result.FailureKind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task RejectedPayloadOrCredentials_ArePermanent(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("{\"message\":\"Rejected\"}")
        };
        var client = CreateClient(_ => Task.FromResult(response));

        var result = await client.SendSaleAsync(TestSaleFactory.Create(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ApiFailureKind.Permanent, result.FailureKind);
        Assert.Contains("Rejected", result.Error);
    }

    [Fact]
    public async Task TestConnection_UsesAuthenticatedConnectorEndpoint()
    {
        Uri? requestedUri = null;
        AuthenticationHeaderValue? authorization = null;
        var client = CreateClient(request =>
        {
            requestedUri = request.RequestUri;
            authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var result = await client.TestConnectionAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("https://app.origennatural.mx/api/v1/connector", requestedUri!.ToString());
        Assert.Equal("Bearer", authorization!.Scheme);
        Assert.Equal("orp_test", authorization.Parameter);
    }

    private static HttpLoyaltyApiClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("https://app.origennatural.mx/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
        return new HttpLoyaltyApiClient(
            httpClient,
            Options.Create(new ApiOptions
            {
                Mode = "Http",
                BaseUrl = "https://app.origennatural.mx/",
                ApiKey = "orp_test"
            }),
            NullLogger<HttpLoyaltyApiClient>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Domain;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Infrastructure.Api;

public sealed class HttpLoyaltyApiClient(
    HttpClient httpClient,
    IOptions<ApiOptions> options,
    ILogger<HttpLoyaltyApiClient> logger) : ILoyaltyApiClient
{
    private readonly ApiOptions _options = options.Value;

    public string Name => "HTTP";

    public async Task<ApiResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/connector");
            AddAuthorization(request);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Ok()
                : ClassifyFailure(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult.Retryable("API health check timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "API health check failed");
            return ApiResult.Retryable("API health check could not connect.");
        }
    }

    public async Task<ApiResult> SendSaleAsync(Sale sale, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/sales")
            {
                Content = new StringContent(
                    SaleJsonSerializer.Serialize(sale),
                    Encoding.UTF8,
                    "application/json")
            };
            AddAuthorization(request);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ApiResult.Ok();
            }

            var error = await ReadErrorAsync(response, cancellationToken);
            return ClassifyFailure(response.StatusCode, error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult.Retryable("API request timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "API request failed for sale {TicketNumber}", sale.TicketNumber);
            return ApiResult.Retryable("API connection failed.");
        }
    }

    private static ApiResult ClassifyFailure(HttpStatusCode statusCode, string? responseError)
    {
        var status = (int)statusCode;
        var error = string.IsNullOrWhiteSpace(responseError)
            ? $"API returned HTTP {status}."
            : $"API returned HTTP {status}: {responseError}";

        return ((statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests) || status >= 500)
                ? ApiResult.Retryable(error)
                : ApiResult.Permanent(error);
    }

    private static async Task<string?> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return Truncate(message.GetString() ?? string.Empty);
            }
        }
        catch (JsonException)
        {
            // Non-JSON proxy responses are reduced to a bounded single line below.
        }

        return Truncate(string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
    }

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];

    private void AddAuthorization(HttpRequestMessage request) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
}

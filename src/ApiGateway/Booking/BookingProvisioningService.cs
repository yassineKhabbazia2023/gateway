using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApiGateway.Booking.Models;

namespace ApiGateway.Booking;

public class BookingProvisioningService(
    IHttpClientFactory httpClientFactory,
    ILogger<BookingProvisioningService> logger) : IBookingProvisioningService
{
    public async Task<BookingApiResponse> CreateBusinessAsync(int contactId, string bearerToken)
    {
        var client = CreateClient(bearerToken, contactId);
        var url = "/api/booking/businesses";

        logger.LogInformation("Calling Booking API: POST {Url} for ContactId: {ContactId}", url, contactId);

        var response = await client.PostAsync(url, null);
        var result = await ToBookingApiResponse(response);

        if (!result.IsSuccessStatusCode)
        {
            logger.LogError("Booking business creation failed. Status: {StatusCode}, Response: {Response}",
                result.StatusCode, result.Content);
        }
        else
        {
            logger.LogInformation("Booking business creation returned {StatusCode} for ContactId: {ContactId}",
                result.StatusCode, contactId);
        }

        return result;
    }

    public async Task<BookingApiResponse> CreateServiceAsync(string bookingBusinessId, int contactId,
        BookingProvisioningRequest request, string bearerToken)
    {
        var client = CreateClient(bearerToken, contactId);
        var url = $"/api/booking/businesses/{bookingBusinessId}/services?ContactId={contactId}";

        logger.LogInformation("Calling Booking API: POST {Url} for ContactId: {ContactId}, BusinessId: {BusinessId}",
            url, contactId, bookingBusinessId);

        var response = await client.PostAsJsonAsync(url, request);
        var result = await ToBookingApiResponse(response);

        if (!result.IsSuccessStatusCode)
        {
            logger.LogError("Booking service creation failed. Status: {StatusCode}, Response: {Response}",
                result.StatusCode, result.Content);
        }
        else
        {
            logger.LogInformation("Booking service creation returned {StatusCode} for ContactId: {ContactId}, BusinessId: {BusinessId}",
                result.StatusCode, contactId, bookingBusinessId);
        }

        return result;
    }

    private HttpClient CreateClient(string bearerToken, int contactId)
    {
        var client = httpClientFactory.CreateClient("BookingClient");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        client.DefaultRequestHeaders.Add("CurrentUser", contactId.ToString());
        return client;
    }

    private static async Task<BookingApiResponse> ToBookingApiResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return new BookingApiResponse
        {
            StatusCode = response.StatusCode,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            Content = content
        };
    }
}

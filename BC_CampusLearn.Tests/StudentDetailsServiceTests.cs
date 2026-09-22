using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BC_CampusLearn.Services.Students;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BC_CampusLearn.Tests;

public class StudentDetailsServiceTests
{
    [Fact]
    public async Task SendsAuthenticatedRequestAndMapsValidResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new CallbackHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "StudentNumber": 600001,
                      "FirstName": "Lebo",
                      "PreferredName": "Lee",
                      "Surname": "Nkosi",
                      "Email": "600001@student.belgiumcampus.ac.za",
                      "Programme": "Bachelor of Computing",
                      "YearOfStudy": 2,
                      "Campus": "Pretoria Campus",
                      "EnrolmentYear": 2026
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://student-api.example/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        var options = Options.Create(new StudentDetailsApiOptions
        {
            Enabled = true,
            BaseUrl = "https://student-api.example/",
            Username = "api-user",
            Password = "api-password",
            Token = "api-token"
        });
        var service = new StudentDetailsService(
            client,
            options,
            NullLogger<StudentDetailsService>.Instance);

        StudentDetailsResult result = await service.GetAsync("600001");

        Assert.Equal(StudentDetailsStatus.Success, result.Status);
        Assert.Equal("Lee", result.Details!.PreferredName);
        Assert.Equal(
            "https://student-api.example/webhook/studentdetails?studentid=600001",
            capturedRequest!.RequestUri!.AbsoluteUri);
        Assert.Equal(
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes("api-user:api-password"))),
            capturedRequest.Headers.Authorization);
        Assert.Equal("wstoken=api-token", capturedBody);
    }

    [Fact]
    public async Task RejectsResponseForDifferentStudent()
    {
        var handler = new CallbackHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "StudentNumber": "999999",
                      "FirstName": "Wrong",
                      "Surname": "Student",
                      "Email": "wrong@example.test",
                      "Programme": "Bachelor of Computing",
                      "YearOfStudy": 2,
                      "Campus": "Pretoria Campus"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://student-api.example/")
        };
        var service = new StudentDetailsService(
            client,
            Options.Create(new StudentDetailsApiOptions
            {
                Enabled = true,
                BaseUrl = "https://student-api.example/",
                Username = "user",
                Password = "password",
                Token = "token"
            }),
            NullLogger<StudentDetailsService>.Instance);

        StudentDetailsResult result = await service.GetAsync("600001");

        Assert.Equal(StudentDetailsStatus.InvalidResponse, result.Status);
        Assert.Null(result.Details);
    }

    [Fact]
    public async Task AcceptsSingleItemN8nArrayResponse()
    {
        var handler = new CallbackHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [{
                      "StudentNumber": 600001,
                      "FirstName": "Lebo",
                      "PreferredName": "",
                      "Surname": "Nkosi",
                      "Email": "600001@student.belgiumcampus.ac.za",
                      "Programme": "Bachelor of Computing",
                      "YearOfStudy": "2",
                      "Campus": "Pretoria Campus"
                    }]
                    """,
                    Encoding.UTF8,
                    "application/json")
            }));
        var service = CreateService(handler);

        StudentDetailsResult result = await service.GetAsync("600001");

        Assert.Equal(StudentDetailsStatus.Success, result.Status);
        Assert.Equal(2, result.Details!.YearOfStudy);
    }

    private static StudentDetailsService CreateService(
        HttpMessageHandler handler) => new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://student-api.example/")
            },
            Options.Create(new StudentDetailsApiOptions
            {
                Enabled = true,
                BaseUrl = "https://student-api.example/",
                Username = "user",
                Password = "password",
                Token = "token"
            }),
            NullLogger<StudentDetailsService>.Instance);

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request);
    }
}

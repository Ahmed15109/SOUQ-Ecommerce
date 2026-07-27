using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EcommerceApp.Tests;

public sealed class ApplicationSmokeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApplicationSmokeTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task PrivacyPage_StartsAndUsesHardenedLocalAssets()
    {
        using var response = await _client.GetAsync("/Home/Privacy");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' 'nonce-", csp);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", csp);
        Assert.Contains("style-src 'self';", csp);
        Assert.DoesNotContain("style-src 'self' 'unsafe-inline'", csp);
        Assert.Contains("/lib/bootstrap-icons/font/bootstrap-icons.min.css", html);
        Assert.DoesNotContain("cdn.jsdelivr.net", html);
        Assert.DoesNotContain("fonts.googleapis.com", html);
        Assert.DoesNotContain("images.unsplash.com", html);
        Assert.DoesNotContain("EcommerceApp.styles.css", html);
        Assert.Contains("سياسة الخصوصية", html);
    }

    [Theory]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.rtl.min.css")]
    [InlineData("/lib/bootstrap/dist/js/bootstrap.bundle.min.js")]
    [InlineData("/lib/bootstrap-icons/font/bootstrap-icons.min.css")]
    [InlineData("/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff")]
    [InlineData("/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff2")]
    [InlineData("/lib/jquery/dist/jquery.min.js")]
    [InlineData("/lib/jquery-validation/dist/jquery.validate.min.js")]
    [InlineData("/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js")]
    [InlineData("/css/site.css")]
    [InlineData("/js/site.js")]
    public async Task RequiredLocalAsset_IsServed(string path)
    {
        using var response = await _client.GetAsync(path);
        var content = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task LegacyPublicPharmacyPath_IsNotServed()
    {
        using var response = await _client.GetAsync("/uploads/pharmacy/prescription.pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LocalCairoFont_IsServedWithoutExternalFontDependency()
    {
        using var response = await _client.GetAsync("/fonts/Cairo-Regular.ttf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "font",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Fact]
    public async Task GoogleLogin_IsHiddenWhenCredentialsAreAbsent()
    {
        using var response = await _client.GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("المتابعة باستخدام Google", html);
        Assert.DoesNotContain("Authentication/ExternalLogin", html);
    }

    [Theory]
    [InlineData("/Account/Login", "192.0.2.10", "192.0.2.11")]
    [InlineData("/PharmacyRequests/Create", "198.51.100.10", "198.51.100.11")]
    public async Task NamedRateLimit_IsIsolatedByClientIp(
        string path,
        string limitedClientIp,
        string otherClientIp)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var allowedRequest = CreatePost(path, limitedClientIp);
            using var allowedResponse = await _client.SendAsync(allowedRequest);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowedResponse.StatusCode);
        }

        using var rejectedRequest = CreatePost(path, limitedClientIp);
        using var rejectedResponse = await _client.SendAsync(rejectedRequest);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);

        using var independentRequest = CreatePost(path, otherClientIp);
        using var independentResponse = await _client.SendAsync(independentRequest);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, independentResponse.StatusCode);
    }

    private static HttpRequestMessage CreatePost(string path, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent([])
        };
        request.Headers.Add("X-Forwarded-For", clientIp);
        return request;
    }
}

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

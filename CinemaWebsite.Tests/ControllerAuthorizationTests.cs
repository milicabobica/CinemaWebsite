using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CinemaWebsite.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CinemaWebsite.Tests;

public class ControllerAuthorizationTests
{
    private static readonly Type[] Controllers = typeof(MoviesController).Assembly.GetTypes()
        .Where(type => !type.IsAbstract && typeof(Controller).IsAssignableFrom(type))
        .ToArray();

    [Theory]
    [InlineData(null)]
    [InlineData("Member")]
    [InlineData("Admin")]
    public async Task ActionPermissions_PreservePublicMemberAndAdminAccess(string? role)
    {
        using var services = CreateServices();
        var pipeline = CreatePipeline(services);

        foreach (var controller in Controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(method => !method.IsSpecialName && !method.IsDefined(typeof(NonActionAttribute))))
        {
            // Exercise the real authentication/authorization middleware, including
            // AllowAnonymous overriding the controller policy, for GET and POST overloads.
            var metadata = controller.GetCustomAttributes(true).Concat(action.GetCustomAttributes(true));
            var actual = await GetStatusAsync(services, pipeline, metadata, role);
            var allowed = IsPublic(controller, action.Name)
                || role == "Admin"
                || (role != null && controller == typeof(ReservationsController) && action.Name == "MyReservations");
            var expected = allowed ? 200 : role == null ? 401 : 403;

            Assert.True(actual == expected,
                $"{controller.Name}.{action} for {role ?? "anonymous"}: expected {expected}, got {actual}.");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Member")]
    [InlineData("Admin")]
    public async Task UnannotatedNewActions_InheritControllerProtection(string? role)
    {
        using var services = CreateServices();
        var pipeline = CreatePipeline(services);

        foreach (var controller in Controllers)
        {
            var actual = await GetStatusAsync(services, pipeline, controller.GetCustomAttributes(true), role);
            var allowed = role == "Admin" || (role != null && controller == typeof(ReservationsController));
            var expected = allowed ? 200 : role == null ? 401 : 403;

            Assert.True(actual == expected,
                $"Default for {controller.Name}, {role ?? "anonymous"}: expected {expected}, got {actual}.");
        }
    }

    private static bool IsPublic(Type controller, string action) => controller.Name switch
    {
        nameof(HomeController) => action is "Index" or "Privacy" or "Error" or "SetLanguage" or "SetCookieConsent",
        nameof(ContactMessagesController) => action == "Create",
        nameof(ReservationsController) => action is "Create" or "Payment" or "Details" or "Cancel" or "CancelConfirmed",
        _ => action is "Index" or "Details"
    };

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddAuthorization();
        services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        return services.BuildServiceProvider();
    }

    private static RequestDelegate CreatePipeline(IServiceProvider services)
    {
        var app = new ApplicationBuilder(services);
        app.UseAuthentication();
        app.UseAuthorization();
        app.Run(context =>
        {
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
        });
        return app.Build();
    }

    private static async Task<int> GetStatusAsync(ServiceProvider services, RequestDelegate pipeline,
        IEnumerable<object> metadata, string? role)
    {
        using var scope = services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        if (role != null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Role, role)], "Test"));
        }
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "Test action"));

        await pipeline(context);
        return context.Response.StatusCode;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Context.User.Identity?.IsAuthenticated == true
                ? AuthenticateResult.Success(new AuthenticationTicket(Context.User, Scheme.Name))
                : AuthenticateResult.NoResult());
    }
}

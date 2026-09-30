using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Shouldly;

namespace OrderProcessing.Api.Tests;

/// <summary>
/// Each named console formatter reads <c>Logging:Console:FormatterOptions</c>; <c>Logging:Console:IncludeScopes</c>
/// only applies when no formatter is named (plain local runs). Without scopes the JSON lines in Docker would lack the
/// request's trace id.
/// </summary>
public class LoggingConfigurationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // The formatters read CurrentValue (the default-named options), so the test does the same.
    private T OptionsFor<T>(string formatterName)
        where T : ConsoleFormatterOptions =>
        factory.WithWebHostBuilder(b => b.UseSetting("Logging:Console:FormatterName", formatterName))
            .Services.GetRequiredService<IOptionsMonitor<T>>().CurrentValue;

    [Fact]
    public void JsonFormatter_DefaultConfig_IncludesScopes() =>
        OptionsFor<JsonConsoleFormatterOptions>(ConsoleFormatterNames.Json).IncludeScopes.ShouldBeTrue();

    [Fact]
    public void SimpleFormatter_DefaultConfig_IncludesScopes() =>
        OptionsFor<SimpleConsoleFormatterOptions>(ConsoleFormatterNames.Simple).IncludeScopes.ShouldBeTrue();
}

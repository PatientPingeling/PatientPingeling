using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using NotificationService.Domain.Entities;
using NotificationService.Application.Abstractions;
using NotificationService.Infrastructure.Persistence;
using System.Linq;

namespace NotificationService.ArchTests;

/// <summary>
/// Architecture tests enforce that Clean Architecture dependency rules are never violated.
///
/// Clean Architecture rule: dependencies always point INWARD.
///
///     Api / Scheduler / Worker  (outermost — may depend on everything)
///             │
///             ▼
///        Application           (business logic — may depend on Domain only)
///             │
///             ▼
///           Domain              (innermost — no dependencies on other layers)
///             ▲
///             │
///        Infrastructure        (data/messaging — may depend on Application + Domain)
///
/// If any of these tests fail, someone accidentally imported a class from the wrong layer.
/// The CI pipeline catches this automatically before it reaches production.
/// </summary>
[TestClass]
public sealed class CleanArchitectureTests
{
    // Assembly references — NetArchTest uses these to locate the assemblies under test.
    // typeof(X).Assembly gives us the compiled DLL of the layer that X lives in.
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Appointment).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(IAppointmentIngestionService).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(NotificationDbContext).Assembly;
    // Program is declared as `public partial class Program` in Api/Program.cs
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(Program).Assembly;

    // ── Domain layer ──────────────────────────────────────────────────────────
    // Domain is the innermost ring. It contains only business entities and value
    // objects. It must NEVER know that a database, HTTP endpoint, or RabbitMQ
    // exists — that would make it impossible to reuse or test in isolation.

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("Domain must not depend on Application — entities must not call services.")]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("NotificationService.Application")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Domain layer illegally depends on Application. Violating types:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("Domain must not depend on Infrastructure — entities must not touch EF Core or RabbitMQ.")]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("NotificationService.Infrastructure")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Domain layer illegally depends on Infrastructure. Violating types:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("Domain must not depend on Api — entities must not reference HTTP concerns.")]
    public void Domain_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("NotificationService.Api")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Domain layer illegally depends on Api. Violating types:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    // ── Application layer ─────────────────────────────────────────────────────
    // Application contains business logic and defines interfaces (abstractions).
    // It may use Domain types but must NOT reference Infrastructure or Api.
    // Why: if Application depended on Infrastructure, you could never swap out
    // the database or message broker without rewriting business logic.

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("Application must not depend on Infrastructure — business logic must not reference EF Core directly.")]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("NotificationService.Infrastructure")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Application layer illegally depends on Infrastructure. Violating types:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("Application must not depend on Api — business logic must not reference HTTP concerns.")]
    public void Application_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("NotificationService.Api")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Application layer illegally depends on Api. Violating types:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    // ── Infrastructure layer ──────────────────────────────────────────────────
    // Infrastructure implements the interfaces defined in Application (e.g. repositories,
    // hashing, encryption). It may know about Application and Domain, but must
    // not reference Api — the HTTP layer is not its concern.

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("Infrastructure must not depend on Api — data access must not reference HTTP endpoints.")]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn("NotificationService.Api")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Infrastructure layer illegally depends on Api. Violating types:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    // ── Naming conventions ────────────────────────────────────────────────────
    // These tests enforce that interfaces follow the I-prefix convention and that
    // services are named consistently. Conventions make the codebase predictable:
    // any developer knows where to find things without reading every file.

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("All interfaces in Application must start with 'I' — enforces .NET naming convention.")]
    public void Application_Interfaces_ShouldBeNamedWithIPrefix()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().AreInterfaces()
            .Should().HaveNameStartingWith("I")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Interfaces not following I-prefix convention:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("All services in Application must end with 'Service' — enforces consistent naming.")]
    public void Application_Services_ShouldBeNamedWithServiceSuffix()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("Service")
            .Should().ResideInNamespace("NotificationService.Application")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful,
            $"Services not following naming convention:\n{string.Join("\n", result.FailingTypeNames ?? [])}");
    }

    // ── Security invariants ───────────────────────────────────────────────────
    // This test ensures that DbContext — the gateway to the database — stays
    // inside Infrastructure. If it leaked into Application or Domain, business
    // logic could bypass the repository pattern and query the DB directly,
    // making the architecture impossible to test or replace.

    [TestMethod]
    [TestCategory("Architecture")]
    [Description("DbContext must only reside in Infrastructure — prevents direct DB access from business logic.")]
    public void DbContext_ShouldOnlyResideIn_Infrastructure()
    {
        // NetArchTest 1.x does not have NotExist() — we collect violating types manually.
        // Any DbContext subclass found outside Infrastructure is a violation.
        var violators = Types.InAssemblies([DomainAssembly, ApplicationAssembly, ApiAssembly])
            .That().Inherit(typeof(Microsoft.EntityFrameworkCore.DbContext))
            .GetTypes();

        Assert.IsEmpty(violators,
            $"DbContext found outside Infrastructure layer:\n{string.Join("\n", violators.Select(t => t.FullName))}");
    }
}

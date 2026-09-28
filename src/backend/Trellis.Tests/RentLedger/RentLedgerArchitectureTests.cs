using System.Reflection;
using FluentAssertions;
using Trellis.Api.Modules.RentLedger.Application.Services;
using Xunit;

namespace Trellis.Tests.RentLedger;

/// <summary>
/// BDR-RENT-006 / rent_collection_design.md "What This Deliberately Does Not Do" — Rent Collection
/// records payments that happened elsewhere; it never processes, holds, or moves funds. These tests
/// make that structural, so a well-meaning future change can't quietly wire this module to Billing's
/// payment gateway or start using money-movement vocabulary without a test failing first.
/// </summary>
public sealed class RentLedgerArchitectureTests
{
    private static readonly Type[] RentLedgerTypes = typeof(RentScheduleService).Assembly.GetTypes()
        .Where(t => t.Namespace?.StartsWith("Trellis.Api.Modules.RentLedger", StringComparison.Ordinal) == true)
        .ToArray();

    private static IEnumerable<Type> DependenciesOf(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return type.GetConstructors(all).SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
            .Concat(type.GetFields(all).Select(f => f.FieldType))
            .Concat(type.GetProperties(all).Select(p => p.PropertyType))
            .SelectMany(t => t.IsGenericType ? t.GetGenericArguments().Append(t) : [t]);
    }

    [Fact]
    public void RentLedgerTypes_AreDiscovered()
    {
        RentLedgerTypes.Should().Contain(typeof(RentScheduleService));
    }

    [Fact]
    public void NothingInRentLedger_DependsOnBillingsPaymentGateway()
    {
        var offenders = RentLedgerTypes
            .SelectMany(t => DependenciesOf(t).Select(d => (Type: t, Dependency: d)))
            .Where(x => x.Dependency.Name.Contains("PaymentGateway", StringComparison.Ordinal))
            .Select(x => $"{x.Type.Name} -> {x.Dependency.Name}");

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void RentLedger_TouchesBillingOnly_ThroughTheEntitlementService()
    {
        var offenders = RentLedgerTypes
            .SelectMany(t => DependenciesOf(t).Select(d => (Type: t, Dependency: d)))
            .Where(x => x.Dependency.Namespace?.StartsWith("Trellis.Api.Modules.Billing", StringComparison.Ordinal) == true)
            .Where(x => x.Dependency.Name is not ("IModuleEntitlementService" or "RequiresModuleEntitlementAttribute"))
            .Select(x => $"{x.Type.Name} -> {x.Dependency.Name}");

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void RentLedgerPublicApi_UsesNoMoneyMovementVocabulary()
    {
        string[] forbidden = ["Charge", "Checkout", "Escrow", "Payout", "Disburse", "Settle", "Balance", "Refund", "Transfer", "Deposit", "Receipt"];

        var offenders = RentLedgerTypes
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => $"{t.Name}.{m.Name}").Append(t.Name))
            .Where(name => forbidden.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Distinct();

        offenders.Should().BeEmpty();
    }
}

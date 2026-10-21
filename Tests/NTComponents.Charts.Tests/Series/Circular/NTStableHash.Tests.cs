using System.Reflection;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>The hash behind fallback group colors and initial bubble placement must be identical in every process.</summary>
public class NTStableHash_Tests {
    private static readonly Type _hashType = typeof(NTChart<>).Assembly.GetType("NTComponents.Charts.Core.NTStableHash")!;

    private static int Combine(params object?[] args) => (int)_hashType.GetMethod("Combine", BindingFlags.Public | BindingFlags.Static, args.Select(a => a?.GetType() ?? typeof(string)).ToArray())!.Invoke(null, args)!;

    [Theory]
    [InlineData(1, "Alpha", 428263576)]
    [InlineData(2, "Beta", 1267800845)]
    [InlineData(0, "", 1268118805)]
    public void Combine_WithKey_ReturnsFixedValue(int value, string key, int expected) {
        Combine(value, key).Should().Be(expected);
    }

    [Fact]
    public void Combine_WithTwoIntegers_ReturnsFixedValue() {
        Combine(3, 1301).Should().Be(1211506244);
    }
}

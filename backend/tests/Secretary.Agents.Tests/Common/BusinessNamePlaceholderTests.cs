using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>Every instruction file writes "[tenant business name]" where the business's name
/// belongs, and for three modules nothing ever put a name there.
///
/// ⚠ It failed silently and then loudly. A model handed square brackets does not read them out —
/// it fills them in, from whatever proper noun is nearest. On a real survey call the nearest one
/// was the questionnaire, so Lamiya introduced herself as "Toyota Servis": a filing label the
/// customer had never heard, presented as the company ringing them.</summary>
public sealed class BusinessNamePlaceholderTests
{
    private const string Line = "Salam, [tenant business name] adından zəng edirəm.";

    [Fact]
    public void The_business_name_replaces_the_placeholder()
        => AgentInstructionContext.WithBusinessName(Line, "Salon Demo")
            .ShouldBe("Salam, Salon Demo adından zəng edirəm.");

    [Fact]
    public void Every_mention_is_replaced_not_only_the_first()
        => AgentInstructionContext.WithBusinessName(
                "# Lamiya — [tenant business name]\n\nSalam, [tenant business name] adından.", "Salon Demo")
            .ShouldNotContain("[tenant business name]");

    /// <summary>⚠ The case that caused it. With no tenant name the obvious move is to leave the
    /// placeholder alone — and leaving it alone is exactly what produced the wrong name, because
    /// the brackets are read as an instruction to supply something. So the brackets always go,
    /// and when there is nothing to put there the replacement says not to invent one.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void With_no_business_name_the_brackets_still_go(string? businessName)
    {
        var result = AgentInstructionContext.WithBusinessName(Line, businessName);

        result.ShouldNotContain("[tenant business name]");
        result.ShouldContain("never invent one");
    }
}

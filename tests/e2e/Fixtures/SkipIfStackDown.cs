namespace E2E.Fixtures;

/// <summary>
/// xUnit 2.9 has no native Skip-from-Fact support. We early-return when the
/// compose stack isn't up and log a console line so the suite reports the
/// situation clearly without failing. The <c>e2e.yml</c> workflow always
/// brings the stack up first, so CI exercises the real assertions.
/// </summary>
internal static class SkipIfStackDown
{
    public static bool SoftSkip(MicroservicesFixture fx)
    {
        if (fx.StackIsUp) return false;
        Console.WriteLine($"[E2E] Skipping: {fx.SkipReason}");
        return true;
    }
}

namespace Sample;

public static class Widget
{
    public static int Add(int a, int b) => a + b;

    // Deliberate coverage regression: a second instrumentable line with no test
    // exercising it. Base is 1/1 lines covered (100%); with this added, 1/2 (50%)
    // — a clean, deterministic swing well outside the baseline's tolerance band.
    public static int Subtract(int a, int b) => a - b;
}

using MathLib;

namespace MathLib;

/// <summary>
/// Provides basic math operations.
/// </summary>
public static class Calculator
{
    public static int Add(int a, int b) => a + b;
    public static int Subtract(int a, int b) => a - b;
    public static double Divide(double a, double b) => b != 0 ? a / b : throw new DivideByZeroException();
}

/// <summary>
/// Validates input parameters.
/// </summary>
public interface IValidator
{
    bool IsValid(string input);
}

public class StringValidator : IValidator
{
    public bool IsValid(string input) => !string.IsNullOrWhiteSpace(input);
}

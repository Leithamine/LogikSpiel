#nullable enable
using System;
using System.Linq;

namespace LogikSpiel.Services.NumberRain;

public static class NumberRainRuleLibrary
{
    public static bool IsEven(int n) => n % 2 == 0;
    public static bool IsOdd(int n) => n % 2 != 0;
    public static bool IsMultipleOf(int n, int k) => k != 0 && n % k == 0;
    public static bool EndsWithDigit(int n, int d) => Math.Abs(n) % 10 == Math.Abs(d) % 10;

    public static bool IsBetween(int n, int a, int b)
        => n >= Math.Min(a, b) && n <= Math.Max(a, b);

    public static int DigitCount(int n)
    {
        n = Math.Abs(n);
        if (n == 0) return 1;
        int count = 0;
        while (n > 0)
        {
            count++;
            n /= 10;
        }
        return count;
    }

    public static bool HasDigitCount(int n, int digits) => DigitCount(n) == digits;

    public static bool ContainsDigit(int n, int d)
    {
        n = Math.Abs(n);
        int target = Math.Abs(d) % 10;
        if (n == 0) return target == 0;
        while (n > 0)
        {
            if (n % 10 == target) return true;
            n /= 10;
        }
        return false;
    }

    public static int SumDigits(int n)
    {
        n = Math.Abs(n);
        int sum = 0;
        if (n == 0) return 0;
        while (n > 0)
        {
            sum += n % 10;
            n /= 10;
        }
        return sum;
    }

    public static bool IsPrime(int n)
    {
        if (n < 2) return false;
        if (n % 2 == 0) return n == 2;
        int limit = (int)Math.Sqrt(n);
        for (int i = 3; i <= limit; i += 2)
        {
            if (n % i == 0) return false;
        }
        return true;
    }

    public static bool IsComposite(int n) => n > 1 && !IsPrime(n);

    public static bool IsSquare(int n)
    {
        if (n < 0) return false;
        int r = (int)Math.Sqrt(n);
        return r * r == n;
    }

    public static bool IsPalindrome(int n)
    {
        var s = Math.Abs(n).ToString();
        int i = 0;
        int j = s.Length - 1;
        while (i < j)
        {
            if (s[i] != s[j]) return false;
            i++;
            j--;
        }
        return true;
    }

    public static bool IsSumDigitsPrime(int n) => IsPrime(SumDigits(n));

    public static bool HasAtLeastTwoEqualDigits(int n)
    {
        var digits = Math.Abs(n).ToString();
        return digits.Length != digits.Distinct().Count();
    }

    public static bool HasAllDigitsDifferent(int n)
    {
        var digits = Math.Abs(n).ToString();
        return digits.Length == digits.Distinct().Count();
    }

    public static bool IsCloserTo(int n, int a, int b)
        => Math.Abs(n - a) < Math.Abs(n - b);

    public static bool IsFibonacci(int n)
    {
        if (n < 0) return false;
        return IsPerfectSquare(5 * n * n + 4) || IsPerfectSquare(5 * n * n - 4);
    }

    private static bool IsPerfectSquare(int n)
    {
        if (n < 0) return false;
        int r = (int)Math.Sqrt(n);
        return r * r == n;
    }

    public static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    public static bool HasStrictlyIncreasingDigits(int n)
    {
        var digits = Math.Abs(n).ToString();
        for (int i = 1; i < digits.Length; i++)
        {
            if (digits[i] <= digits[i - 1]) return false;
        }
        return digits.Length > 1;
    }

    public static bool HasStrictlyDecreasingDigits(int n)
    {
        var digits = Math.Abs(n).ToString();
        for (int i = 1; i < digits.Length; i++)
        {
            if (digits[i] >= digits[i - 1]) return false;
        }
        return digits.Length > 1;
    }

    public static bool IsHarshad(int n)
    {
        if (n == 0) return false;
        int sum = SumDigits(n);
        return sum != 0 && n % sum == 0;
    }

    public static bool IsPronic(int n)
    {
        if (n < 0) return false;
        int k = (int)Math.Floor(Math.Sqrt(n));
        return k * (k + 1) == n || (k - 1) * k == n;
    }

    public static bool IsAutomorph(int n)
    {
        int sq = n * n;
        string ns = Math.Abs(n).ToString();
        string sqs = Math.Abs(sq).ToString();
        return sqs.EndsWith(ns, StringComparison.Ordinal);
    }

    public static bool IsSquareFree(int n)
    {
        if (n <= 0) return false;
        int temp = n;
        for (int p = 2; p * p <= temp; p++)
        {
            int count = 0;
            while (temp % p == 0)
            {
                temp /= p;
                count++;
                if (count > 1) return false;
            }
        }
        return true;
    }

    public static bool IsSemiPrime(int n)
    {
        if (n < 4) return false;
        int temp = n;
        int factors = 0;
        for (int p = 2; p * p <= temp && factors <= 2; p++)
        {
            while (temp % p == 0)
            {
                temp /= p;
                factors++;
                if (factors > 2) return false;
            }
        }
        if (temp > 1) factors++;
        return factors == 2;
    }
}

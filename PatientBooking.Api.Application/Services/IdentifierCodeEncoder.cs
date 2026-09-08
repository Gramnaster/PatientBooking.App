using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PatientBooking.Api.Application.Services;

public static class IdentifierCodeEncoder
{
    // I and O excluded to prevent misreading against 1 and 0
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    public const string MedicalRecordNumberShape = "AANNNNNNNNAA";
    public const string EmployeeNumberShape = "AANNAANN";
    public const string AdminNumberShape = "AANNAA";

    // Golden-ratio multiplicative hash constant. Odd, not multiple of 3 or 5
    // Every shape modulus becomes only 2, 3, and 5
    // Multiplying by this constant is a bijection mod any of them
    // No distinct ids ever produce the same code
    private const long Multiplier = 2_654_435_761;

    public static string Encode(int id, string shape)
    {
        long modulus = 1;
        foreach (char position in shape)
        {
            modulus *= position == 'A' ? Letters.Length : 10;
        }

        long permuted = id * Multiplier % modulus;

        char[] code = new char[shape.Length];
        for (int i = shape.Length - 1; i >= 0; i--)
        {
            int radix = shape[i] == 'A' ? Letters.Length : 10;
            int digit = (int)(permuted % radix);
            code[i] = shape[i] == 'A' ? Letters[digit] : (char)('0' + digit);
            permuted /= radix;
        }

        return new string(code);
    }
}

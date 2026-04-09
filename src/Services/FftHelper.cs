namespace AudioAnalyzer.Services;

/// <summary>
/// Shared FFT (Fast Fourier Transform) utility used by KeyDetector and WaveformAnalyzer.
/// Extracted to eliminate code duplication identified in the static audit.
/// </summary>
public static class FftHelper
{
    /// <summary>
    /// Computes an in-place Cooley-Tukey radix-2 FFT on the given complex array.
    /// The array length must be a power of 2.
    /// </summary>
    public static void FFT(System.Numerics.Complex[] data)
    {
        int n = data.Length;
        int bits = (int)Math.Log2(n);

        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
                (data[i], data[j]) = (data[j], data[i]);
        }

        for (int len = 2; len <= n; len *= 2)
        {
            double angle = -2 * Math.PI / len;
            var wLen = new System.Numerics.Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len)
            {
                var w = new System.Numerics.Complex(1, 0);
                for (int j = 0; j < len / 2; j++)
                {
                    var u = data[i + j];
                    var v = data[i + j + len / 2] * w;
                    data[i + j] = u + v;
                    data[i + j + len / 2] = u - v;
                    w *= wLen;
                }
            }
        }
    }

    /// <summary>
    /// Reverses the bits of a value given a specific bit width.
    /// Used during the bit-reversal permutation step of the FFT.
    /// </summary>
    public static int BitReverse(int value, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }
}

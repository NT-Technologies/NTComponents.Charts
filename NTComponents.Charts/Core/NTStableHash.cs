namespace NTComponents.Charts.Core;

/// <summary>
///     Deterministic hashes that, unlike <see cref="HashCode" />, are identical across processes.
/// </summary>
internal static class NTStableHash {
   private const uint _offsetBasis = 2166136261;
   private const uint _prime = 16777619;

   /// <summary>Returns a non-negative FNV-1a hash of <paramref name="value" /> and <paramref name="key" />.</summary>
   public static int Combine(int value, string? key) {
      var hash = Add(_offsetBasis, value);
      foreach (var c in key ?? string.Empty) {
         hash = (hash ^ c) * _prime;
      }
      return (int)(hash & 0x7FFFFFFF);
   }

   /// <summary>Returns a non-negative FNV-1a hash of two integers.</summary>
   public static int Combine(int first, int second) => (int)(Add(Add(_offsetBasis, first), second) & 0x7FFFFFFF);

   private static uint Add(uint hash, int value) {
      for (var shift = 0; shift < 32; shift += 8) {
         hash = (hash ^ (byte)(value >> shift)) * _prime;
      }
      return hash;
   }
}

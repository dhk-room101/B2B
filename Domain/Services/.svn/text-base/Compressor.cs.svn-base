namespace TESVSnip.Domain.Services
{
    using System;

    internal static class Compressor
    {
        private static readonly string[] AutoCompRecList = new string[0];

        /// <summary>
        /// UseDefaultRecordCompression: Test if record must be compressed
        /// </summary>
        /// <param name="name">Record name</param>
        /// <returns>True if record must be compressed</returns>
        public static bool CompressRecord(string name)
        {
            return Array.BinarySearch(AutoCompRecList, name) >= 0;
        }
    }
}
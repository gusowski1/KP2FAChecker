using System;
using System.Collections.Generic;
using System.Linq;
using KPPasskeyChecker.Shared.DomainMatching;

namespace KPPasskeyChecker.SelfCheck
{
    /// <summary>
    /// Self-check assertions shared between KPPasskeyChecker and KP2FAChecker. This file is
    /// compiled verbatim into both harnesses (same as src\Shared); do not add plugin-specific
    /// types here. Callers supply <paramref name="section"/> and <paramref name="assert"/>
    /// callbacks so this class has no dependency on SelfCheck's static state.
    /// </summary>
    internal static class SharedChecks
    {
        // --- PSL / eTLD+1 smoke test (DomainCandidateGenerator + PublicSuffixList) ---------------
        public static void CheckDomainCandidatesEtldPlusOne(
            Action<string> section,
            Action<string, bool> assert)
        {
            section("PSL / eTLD+1 smoke test");

            PublicSuffixList psl = PublicSuffixList.Parse(
                "// test fixture\n" +
                "com\n" +
                "co.uk\n" +
                "uk\n");

            assert("www.example.co.uk -> registrable example.co.uk",
                psl.GetRegistrableDomain("www.example.co.uk") == "example.co.uk");
            assert("mail.google.com -> registrable google.com",
                psl.GetRegistrableDomain("mail.google.com") == "google.com");

            var candidates = DomainCandidateGenerator.GetCandidates("mail.google.com").ToList();
            assert("generator yields full host first",
                candidates.Count > 0 && candidates[0] == "mail.google.com");
            assert("generator stops at 2-label fallback (contains google.com)",
                candidates.Contains("google.com"));
            assert("generator strips leading www.",
                DomainCandidateGenerator.GetCandidates("www.example.co.uk")
                    .First() == "example.co.uk");
        }

        // --- utilities --------------------------------------------------------------------------
        public static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}

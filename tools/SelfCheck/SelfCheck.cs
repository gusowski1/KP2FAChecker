using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KP2FAChecker.Data;
using KPPasskeyChecker.Shared.DomainMatching;
using KPPasskeyChecker.Shared.Pgp;

namespace KP2FAChecker.SelfCheck
{
    /// <summary>
    /// Pure-logic self-test harness for KP2FAChecker. Compiled together with the real plugin
    /// sources (Shared + KP2FAChecker) by run-selfcheck.ps1 using the in-box csc (C# 5), so it can
    /// reach internal members like TfaEntryMapper.Map and FormatEntry. It
    /// exercises only logic that has no dependency on a running KeePass process, the network, or
    /// the file system.
    ///
    /// Exit code 0 = all checks passed; exit code 1 = at least one check failed (stops at the
    /// first failure). Every assertion prints a single PASS/FAIL line.
    /// </summary>
    internal static class SelfCheck
    {
        private static int _failures;

        private static int Main()
        {
            Console.WriteLine("KP2FAChecker self-check");
            Console.WriteLine("=======================");

            CheckMethodTokenParsing();
            CheckScopeEndpointMapping();
            CheckSkipDisabledRule();
            CheckFormatEntry();
            CheckSignedCacheKeyDistinctness();
            CheckDomainCandidatesEtldPlusOne();
            CheckPgpPath();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("All checks passed.");
                return 0;
            }

            Console.WriteLine(_failures + " check(s) FAILED.");
            return 1;
        }

        // --- method-token parsing (TfaEntryMapper / TfaMethods.Parse) --------------------------
        private static void CheckMethodTokenParsing()
        {
            Section("method-token parsing");

            Assert("\"totp\" -> Totp",            TfaMethods.Parse("totp") == TfaMethod.Totp);
            Assert("\"u2f\" -> U2f (SecurityKey)", TfaMethods.Parse("u2f") == TfaMethod.U2f);
            Assert("\"sms\" -> Sms",              TfaMethods.Parse("sms") == TfaMethod.Sms);
            Assert("\"email\" -> Email",          TfaMethods.Parse("email") == TfaMethod.Email);
            Assert("\"call\" -> Call (PhoneCall)", TfaMethods.Parse("call") == TfaMethod.Call);
            Assert("\"custom-software\" -> CustomSoftware",
                TfaMethods.Parse("custom-software") == TfaMethod.CustomSoftware);
            Assert("\"custom-hardware\" -> CustomHardware",
                TfaMethods.Parse("custom-hardware") == TfaMethod.CustomHardware);
            Assert("\"TOTP\" case-insensitive -> Totp", TfaMethods.Parse("TOTP") == TfaMethod.Totp);
            Assert("unknown token -> Unknown", TfaMethods.Parse("carrier-pigeon") == TfaMethod.Unknown);

            // The mapper drops unknown tokens and de-duplicates; recognised ones survive in order.
            TfaEntry entry = Map("example.com", Methods("totp", "carrier-pigeon", "u2f", "totp"));
            Assert("mapper drops unknown tokens",
                !entry.Methods.Contains(TfaMethod.Unknown));
            Assert("mapper de-duplicates repeated tokens",
                entry.Methods.Count == 2);
            Assert("mapper preserves recognised tokens (totp + u2f)",
                entry.Methods.Contains(TfaMethod.Totp) && entry.Methods.Contains(TfaMethod.U2f));
        }

        // --- scope -> endpoint mapping (TfaEndpoints.ForScope) ---------------------------------
        private static void CheckScopeEndpointMapping()
        {
            Section("scope -> endpoint mapping");

            Assert("TotpOnly -> totp.json",
                TfaEndpoints.ForScope(TfaDataScope.TotpOnly).EndsWith("/totp.json", StringComparison.Ordinal));
            Assert("U2fOnly -> u2f.json",
                TfaEndpoints.ForScope(TfaDataScope.U2fOnly).EndsWith("/u2f.json", StringComparison.Ordinal));
            Assert("SmsOnly -> sms.json",
                TfaEndpoints.ForScope(TfaDataScope.SmsOnly).EndsWith("/sms.json", StringComparison.Ordinal));
            Assert("EmailOnly -> email.json",
                TfaEndpoints.ForScope(TfaDataScope.EmailOnly).EndsWith("/email.json", StringComparison.Ordinal));
            Assert("AnySupport -> all.json",
                TfaEndpoints.ForScope(TfaDataScope.AnySupport).EndsWith("/all.json", StringComparison.Ordinal));

            Assert("SignatureForScope appends .sig",
                TfaEndpoints.SignatureForScope(TfaDataScope.TotpOnly)
                    == TfaEndpoints.ForScope(TfaDataScope.TotpOnly) + ".sig");
        }

        // --- skip-disabled rule (empty methods / {} -> blank, not indexed) ---------------------
        private static void CheckSkipDisabledRule()
        {
            Section("skip-disabled rule");

            TfaEntry empty = Map("disabled.com", new Dictionary<string, object>());
            Assert("missing methods -> SupportsAny == false", !empty.SupportsAny);
            Assert("missing methods -> FormatEntry empty",
                FormatEntry(empty) == string.Empty);

            TfaEntry emptyArray = Map("disabled.com", Methods());
            Assert("empty methods array -> SupportsAny == false", !emptyArray.SupportsAny);

            // Only unknown tokens still means "no documented method".
            TfaEntry onlyUnknown = Map("disabled.com", Methods("carrier-pigeon"));
            Assert("only-unknown methods -> SupportsAny == false", !onlyUnknown.SupportsAny);
        }

        // --- FormatEntry / column value (FormatEntry) -------------------------
        private static void CheckFormatEntry()
        {
            Section("FormatEntry / column value");

            TfaEntry simple = Map("example.com", Methods("totp", "u2f", "sms"));
            Assert("totp+u2f+sms -> \"TOTP, Security Key, SMS\"",
                FormatEntry(simple) == "TOTP, Security Key, SMS");

            // custom-software with product names -> "Software (Duo)".
            var data = Methods("totp", "u2f", "custom-software");
            data["custom-software"] = ArrayOf("Duo");
            TfaEntry withSoftware = Map("example.com", data);
            Assert("totp+u2f+custom-software(Duo) -> \"TOTP, Security Key, Software (Duo)\"",
                FormatEntry(withSoftware) == "TOTP, Security Key, Software (Duo)");

            TfaEntry blank = Map("example.com", new Dictionary<string, object>());
            Assert("no methods -> empty string", FormatEntry(blank) == string.Empty);
        }

        // --- signed vs unsigned cache-key distinctness -----------------------------------------
        private static void CheckSignedCacheKeyDistinctness()
        {
            Section("signed / unsigned cache-key distinctness");

            foreach (TfaDataScope scope in (TfaDataScope[])Enum.GetValues(typeof(TfaDataScope)))
            {
                string plain  = TfaEndpoints.CacheKey(scope);
                string signed = TfaEndpoints.SignedCacheKey(scope);
                Assert("CacheKey != SignedCacheKey for " + scope, plain != signed);
                Assert("SignedCacheKey for " + scope + " carries _signed suffix",
                    signed.EndsWith("_signed", StringComparison.Ordinal));
                Assert("plain CacheKey for " + scope + " has no _signed suffix",
                    !plain.EndsWith("_signed", StringComparison.Ordinal));
            }
        }

        // --- PSL / eTLD+1 smoke test (Shared, identical to KPPasskeyChecker) --------------------
        private static void CheckDomainCandidatesEtldPlusOne()
        {
            Section("PSL / eTLD+1 smoke test");

            PublicSuffixList psl = PublicSuffixList.Parse(
                "// test fixture\n" +
                "com\n" +
                "co.uk\n" +
                "uk\n");

            Assert("www.example.co.uk -> registrable example.co.uk",
                psl.GetRegistrableDomain("www.example.co.uk") == "example.co.uk");
            Assert("mail.google.com -> registrable google.com",
                psl.GetRegistrableDomain("mail.google.com") == "google.com");

            var candidates = DomainCandidateGenerator.GetCandidates("mail.google.com").ToList();
            Assert("generator yields full host first",
                candidates.Count > 0 && candidates[0] == "mail.google.com");
            Assert("generator stops at 2-label fallback (contains google.com)",
                candidates.Contains("google.com"));
            Assert("generator strips leading www.",
                DomainCandidateGenerator.GetCandidates("www.example.co.uk").First() == "example.co.uk");
        }

        // --- PGP path --------------------------------------------------------------------------
        // Exercises the full offline verification path against a committed real ".sig" fixture
        // (an RSA-4096 / SHA-512 inline OpenPGP message captured from api.2fa.directory) using the
        // pinned TfaTrustAnchor key. No network access: the fixture is read from disk next to the
        // harness .exe. A second pass uses a deliberately corrupted key to prove a wrong key fails
        // closed.
        private static void CheckPgpPath()
        {
            Section("PGP signature path");

            string fixturePath = Path.Combine(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures"), "u2f.json.sig");
            byte[] sigBytes = File.ReadAllBytes(fixturePath);

            PgpVerificationResult result = TfaTrustAnchor.CreateVerifier().Verify(sigBytes);
            Assert("Verify(fixture) returns valid result with non-null JSON",
                result.IsValid && result.SignedContent != null);

            string json = result.SignedContent != null
                ? new string(result.SignedContent.Select(b => (char)b).ToArray()) : string.Empty;
            Assert("Extracted JSON is valid (non-empty, starts with '{')",
                json.Length > 0 && json.TrimStart()[0] == '{');

            PgpVerificationResult wrongKey =
                new OpenPgpSignatureVerifier(CorruptedKey()).Verify(sigBytes);
            Assert("Verify with wrong key returns invalid result", !wrongKey.IsValid);
        }

        // Builds an RSA public key from the pinned CERT RDATA with a single modulus byte flipped,
        // so it parses cleanly but can never match the real signature (fail-closed wrong-key case).
        private static OpenPgpRsaPublicKey CorruptedKey()
        {
            byte[] rdata = HexToBytes(CertRecordHex);
            rdata[rdata.Length - 8] ^= 0xFF; // flip a byte well inside the modulus
            return OpenPgpRsaPublicKey.FromCertRecord(rdata);
        }

        // Mirror of the pinned CERT RDATA (TfaTrustAnchor.CertRecordHex is private); used only to
        // derive a deliberately corrupted key for the wrong-key assertion.
        private const string CertRecordHex =
            "000300000099020d04604596b2011000d5291dc2ac2b30ffb2930604f90405214fd010630c5a03b9bddcee7af66a66640b703f38ab3ca1960898897f7ecc7bf7d6e65178e80642ffaf6f7cc85d1ec2cb0018ae9d4d898dead5b51ce4e0629d0fe2ce3d435bc33ffcc09a41874e08e867741d2181235450969f195c072fb933776cc3263a21438da92b240e74f26eb4bac5d4059f83eab007ce7d681233b9d36db0cbe98bf6a8d5fd91ad813651897f6f2ea2b35c071c898ccb3f900c70ba052c6708cd148dbde3000bc729eb4bb6e8b195545a81bd511e4cb6bcd734fcee73cecdd664b5c7559c66c637c333392a6969d6246faca4f5732151f3c05f25f66f6d0cd5867664c4b7366aa37a6c69bf8bd53e59615dc89a0a8953337af25d6c229ca1cdcff6418f07f5eb76da7dc867bbf4995fd4897e5e2030002e57503125c4681be608babde9cfcaa9c837c4ed1ec904bd5590de941d8c9c2c8c3903ed15aed08704eec0045137422017d3c6e25823cbd22f55e2fa7780348ddbf5205a55fb8f489c59c31047491f8b2f11ec4d31945739b98dad05493a3ba7659f43ff666088022981a0b1d99068a7345349355cb64a3b98a33b883fddc858ea159dc4205ce4591ec3359b0155efd597710d7eb2e5d0ebefb53c4753cd1f6fcf2f2f4a9da381986a056fe30efb91557709de01221a9459d97c25183ce0c80bdf0e4fa507649cff0739a170d95b8793491048604f00070011010001";

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        // --- mirror of TfaColumnProvider.FormatEntry / AppendNames -----------------------------
        // TfaColumnProvider derives from KeePass's ColumnProvider, so referencing it at runtime
        // would force the KeePass assembly to load (it is only a compile reference here). The
        // format logic is mirrored verbatim from the source so the harness stays KeePass-free.
        private static string FormatEntry(TfaEntry entry)
        {
            if (entry == null || !entry.SupportsAny) return string.Empty;

            var parts = new List<string>(entry.Methods.Count);
            foreach (TfaMethod method in entry.Methods)
            {
                string label = TfaMethods.Label(method);
                if (label.Length == 0) continue;

                if (method == TfaMethod.CustomSoftware)
                    label = AppendNames(label, entry.CustomSoftware);
                else if (method == TfaMethod.CustomHardware)
                    label = AppendNames(label, entry.CustomHardware);

                parts.Add(label);
            }

            return string.Join(", ", parts.ToArray());
        }

        private static string AppendNames(string label, IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0) return label;

            var sb = new StringBuilder(label);
            sb.Append(" (");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            sb.Append(")");
            return sb.ToString();
        }

        // --- helpers ---------------------------------------------------------------------------
        private static TfaEntry Map(string domain, Dictionary<string, object> data)
        {
            return TfaEntryMapper.Map(domain, data);
        }

        // Builds a data dict with a "methods" ArrayList (the shape the JSON deserialiser produces).
        private static Dictionary<string, object> Methods(params string[] tokens)
        {
            return new Dictionary<string, object> { { "methods", ArrayOf(tokens) } };
        }

        private static ArrayList ArrayOf(params string[] values)
        {
            var list = new ArrayList();
            foreach (string v in values) list.Add(v);
            return list;
        }

        private static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine("[" + title + "]");
        }

        private static void Assert(string description, bool condition)
        {
            if (condition)
            {
                Console.WriteLine("  PASS  " + description);
                return;
            }
            _failures++;
            Console.WriteLine("  FAIL  " + description);
        }
    }
}

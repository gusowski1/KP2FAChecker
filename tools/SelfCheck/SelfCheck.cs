using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeePassLib;
using KeePassLib.Security;
using KP2FAChecker.Data;
using KP2FAChecker.UI;
using KPPasskeyChecker.SelfCheck;
using KeeRadar.Shared.Pgp;

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
            CheckStoredOtpState();
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
                TfaColumnProvider.FormatEntry(empty) == string.Empty);

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
                TfaColumnProvider.FormatEntry(simple) == "TOTP, Security Key, SMS");

            // custom-software with product names -> "Software (Duo)".
            var data = Methods("totp", "u2f", "custom-software");
            data["custom-software"] = ArrayOf("Duo");
            TfaEntry withSoftware = Map("example.com", data);
            Assert("totp+u2f+custom-software(Duo) -> \"TOTP, Security Key, Software (Duo)\"",
                TfaColumnProvider.FormatEntry(withSoftware) == "TOTP, Security Key, Software (Duo)");

            TfaEntry blank = Map("example.com", new Dictionary<string, object>());
            Assert("no methods -> empty string", TfaColumnProvider.FormatEntry(blank) == string.Empty);
        }

        // --- stored-OTP state (story F-H) ------------------------------------------------------
        // Exercises the production HasStoredOtp field-name scan and the ComposeCellValue overlay
        // covering all seven story scenarios. Touching TfaColumnProvider JIT-loads KeePass, which
        // run-selfcheck.ps1 stages next to the harness .exe for this purpose.
        private static void CheckStoredOtpState()
        {
            Section("stored-OTP state (column overlay)");

            // ComposeCellValue truth table -----------------------------------------------------
            // 2: directory match + stored OTP -> "[Active] <value>" (one prefix only).
            Assert("dir value + stored -> \"[Active] ...\"",
                TfaColumnProvider.ComposeCellValue("TOTP, Security Key", true)
                    == "[Active] TOTP, Security Key");
            // 1: directory match + no stored OTP -> "[Inactive] <value>".
            Assert("dir value + not stored -> \"[Inactive] ...\"",
                TfaColumnProvider.ComposeCellValue("TOTP, Security Key", false)
                    == "[Inactive] TOTP, Security Key");
            // 3 + 5: no directory match + stored OTP -> "Active".
            Assert("no dir + stored -> \"Active\"",
                TfaColumnProvider.ComposeCellValue(string.Empty, true) == "Active");
            // 4 + 5: neither -> empty.
            Assert("no dir + not stored -> empty",
                TfaColumnProvider.ComposeCellValue(string.Empty, false) == string.Empty);
            Assert("null dir + stored -> \"Active\"",
                TfaColumnProvider.ComposeCellValue(null, true) == "Active");

            // HasStoredOtp field-name scan -----------------------------------------------------
            Assert("no fields -> not stored",
                !TfaColumnProvider.HasStoredOtp(EntryWith()));
            Assert("TimeOtp-Secret field -> stored",
                TfaColumnProvider.HasStoredOtp(EntryWith("TimeOtp-Secret")));
            Assert("HmacOtp-Secret field -> stored",
                TfaColumnProvider.HasStoredOtp(EntryWith("HmacOtp-Secret")));
            // 6: both OTP types present still count as one (and ComposeCellValue never doubles).
            Assert("both HmacOtp- and TimeOtp- fields -> stored (no double prefix)",
                TfaColumnProvider.HasStoredOtp(
                    EntryWith("HmacOtp-Secret", "TimeOtp-Secret", "TimeOtp-Period")));
            // Case-insensitive prefix match.
            Assert("lowercase timeotp- field -> stored",
                TfaColumnProvider.HasStoredOtp(EntryWith("timeotp-secret")));
            // Unrelated standard fields must not trigger.
            Assert("UserName/Password/Title -> not stored",
                !TfaColumnProvider.HasStoredOtp(
                    EntryWith(PwDefs.UserNameField, PwDefs.PasswordField, PwDefs.TitleField)));
        }

        // Builds a PwEntry carrying the given string-field names (irrelevant values; the production
        // code only reads names — scenario 7 forbids reading values).
        private static PwEntry EntryWith(params string[] fieldNames)
        {
            PwEntry pe = new PwEntry(true, true);
            foreach (string name in fieldNames)
                pe.Strings.Set(name, new ProtectedString(false, "x"));
            return pe;
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

        // --- PSL / eTLD+1 smoke test (shared with KPPasskeyChecker via SharedChecks.cs) ----------
        private static void CheckDomainCandidatesEtldPlusOne()
        {
            SharedChecks.CheckDomainCandidatesEtldPlusOne(Section, Assert);
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
            byte[] rdata = SharedChecks.HexToBytes(TfaTrustAnchor.CertRecordHex);
            rdata[rdata.Length - 8] ^= 0xFF; // flip a byte well inside the modulus
            return OpenPgpRsaPublicKey.FromCertRecord(rdata);
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
